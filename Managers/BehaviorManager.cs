using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Planet9.Core;
using Planet9.Entities;
using Planet9.Scenes;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages ship behavior logic for friendly and enemy ships
    /// </summary>
    public class BehaviorManager
    {
        private const float MapSize = 8192f;
        private const float EnemyPlayerDetectionRange = 1500f; // Range at which enemy detects and switches to aggressive behavior
        
        // Behavior duration ranges (in seconds)
        private const float IdleMinDuration = 8f;
        private const float IdleMaxDuration = 20f;
        private const float PatrolMinDuration = 20f;
        private const float PatrolMaxDuration = 50f;
        private const float LongDistanceMinDuration = 40f;
        private const float LongDistanceMaxDuration = 120f;
        private const float WanderMinDuration = 10f;
        private const float WanderMaxDuration = 30f;
        
        private readonly Random _random;
        private float _shipIdleRate = 0.3f; // Default: 30% chance to idle
        
        // Dependencies
        private PlayerShip? _playerShip;
        private List<FriendlyShip>? _friendlyShips;
        private List<EnemyShip>? _enemyShips;
        private Dictionary<FriendlyShip, ShipState>? _friendlyShipStates;
        private Dictionary<EnemyShip, EnemyShipState>? _enemyShipStates;
        private PathfindingGrid? _pathfindingGrid;
        private CombatManager? _combatManager;
        
        // Callbacks for ship state management
        private Func<FriendlyShip, ShipState>? _getOrCreateShipState;
        private Func<EnemyShip, EnemyShipState>? _getOrCreateEnemyShipState;
        
        public BehaviorManager(Random random)
        {
            _random = random;
        }
        
        /// <summary>
        /// Set ship collections and dependencies
        /// </summary>
        public void SetDependencies(
            PlayerShip? playerShip,
            List<FriendlyShip> friendlyShips,
            List<EnemyShip> enemyShips,
            Dictionary<FriendlyShip, ShipState> friendlyShipStates,
            Dictionary<EnemyShip, EnemyShipState> enemyShipStates,
            PathfindingGrid? pathfindingGrid,
            CombatManager? combatManager,
            Func<FriendlyShip, ShipState> getOrCreateShipState,
            Func<EnemyShip, EnemyShipState> getOrCreateEnemyShipState,
            float shipIdleRate = 0.3f)
        {
            _playerShip = playerShip;
            _friendlyShips = friendlyShips;
            _enemyShips = enemyShips;
            _friendlyShipStates = friendlyShipStates;
            _enemyShipStates = enemyShipStates;
            _pathfindingGrid = pathfindingGrid;
            _combatManager = combatManager;
            _getOrCreateShipState = getOrCreateShipState;
            _getOrCreateEnemyShipState = getOrCreateEnemyShipState;
            _shipIdleRate = shipIdleRate;
        }
        
        /// <summary>
        /// Set ship idle rate (probability of selecting idle behavior)
        /// </summary>
        public void SetShipIdleRate(float idleRate)
        {
            _shipIdleRate = idleRate;
        }
        
        /// <summary>
        /// Get a random behavior for friendly ships
        /// </summary>
        public FriendlyShipBehavior GetRandomBehavior()
        {
            // Weight behaviors: 30% Idle, 25% Patrol, 20% LongDistance, 25% Wander
            // Note: Aggressive behavior is not included here - it's only for enemy ships
            double roll = _random.NextDouble();
            if (roll < _shipIdleRate)
                return FriendlyShipBehavior.Idle;
            else if (roll < _shipIdleRate + 0.25f)
                return FriendlyShipBehavior.Patrol;
            else if (roll < _shipIdleRate + 0.45f)
                return FriendlyShipBehavior.LongDistance;
            else
                return FriendlyShipBehavior.Wander;
        }
        
        /// <summary>
        /// Get duration for a behavior
        /// </summary>
        public float GetBehaviorDuration(FriendlyShipBehavior behavior)
        {
            switch (behavior)
            {
                case FriendlyShipBehavior.Idle:
                    return (float)(_random.NextDouble() * (IdleMaxDuration - IdleMinDuration) + IdleMinDuration);
                case FriendlyShipBehavior.Patrol:
                    return (float)(_random.NextDouble() * (PatrolMaxDuration - PatrolMinDuration) + PatrolMinDuration);
                case FriendlyShipBehavior.LongDistance:
                    return (float)(_random.NextDouble() * (LongDistanceMaxDuration - LongDistanceMinDuration) + LongDistanceMinDuration);
                case FriendlyShipBehavior.Wander:
                    return (float)(_random.NextDouble() * (WanderMaxDuration - WanderMinDuration) + WanderMinDuration);
                default:
                    return 5f;
            }
        }
        
        /// <summary>
        /// Update friendly ship behavior
        /// </summary>
        public void UpdateFriendlyShipBehavior(FriendlyShip friendlyShip, float deltaTime)
        {
            if (_getOrCreateShipState == null || _friendlyShipStates == null) return;
            
            // Initialize behavior if not set
            var state = _getOrCreateShipState(friendlyShip);
            if (!_friendlyShipStates.ContainsKey(friendlyShip))
            {
                state.Behavior = GetRandomBehavior();
                state.BehaviorTimer = GetBehaviorDuration(state.Behavior);
            }
            
            // Check if currently fleeing - if health has regenerated, resume normal behavior immediately
            FriendlyShipBehavior friendlyCurrentBehavior = state.Behavior;
            if (friendlyCurrentBehavior == FriendlyShipBehavior.Flee)
            {
                // Exit if fully healed (health has regenerated)
                if (friendlyShip.Health >= friendlyShip.MaxHealth)
                {
                    // Health has regenerated - exit flee, switch back to random behavior
                    FriendlyShipBehavior newBehavior = GetRandomBehavior();
                    // Don't randomly select Flee
                    while (newBehavior == FriendlyShipBehavior.Flee)
                    {
                        newBehavior = GetRandomBehavior();
                    }
                    state.Behavior = newBehavior;
                    state.BehaviorTimer = GetBehaviorDuration(newBehavior);
                    friendlyShip.IsFleeing = false; // No longer fleeing, stop damage effect
                    
                    // Face the direction the ship is moving when resuming behavior
                    if (friendlyShip.Velocity.LengthSquared() > 1f)
                    {
                        float targetRotation = (float)Math.Atan2(friendlyShip.Velocity.Y, friendlyShip.Velocity.X) + MathHelper.PiOver2;
                        friendlyShip.Rotation = targetRotation;
                    }
                    
                    if (newBehavior == FriendlyShipBehavior.Idle)
                    {
                        friendlyShip.StopMoving();
                        friendlyShip.IsIdle = true;
                    }
                    else
                    {
                        friendlyShip.IsIdle = false;
                    }
                    
                    System.Console.WriteLine($"[FRIENDLY] Health regenerated! Exiting flee. Health: {friendlyShip.Health:F1}/{friendlyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
                }
            }
            // Decrement behavior timer (only if not fleeing or not fully healed)
            else
            {
                // Only decrement timer if not fleeing (flee timer is managed separately)
                if (friendlyCurrentBehavior != FriendlyShipBehavior.Flee)
                {
                    state.BehaviorTimer -= deltaTime;
                }
                else
                {
                    // Still fleeing and not fully healed, continue fleeing - reset timer if needed
                    if (state.BehaviorTimer <= 0f)
                    {
                        state.BehaviorTimer = 10.0f; // Continue fleeing
                    }
                    else
                    {
                        state.BehaviorTimer -= deltaTime;
                    }
                }
                
                // Check if behavior should transition (timer-based transitions)
                if (state.BehaviorTimer <= 0f)
                {
                    // If currently fleeing (but not ready to exit yet), continue fleeing
                    if (friendlyCurrentBehavior == FriendlyShipBehavior.Flee)
                    {
                        // Still damaged or threat nearby, continue fleeing - reset timer
                        state.BehaviorTimer = 10.0f; // Continue fleeing
                    }
                    else
                    {
                        // Transition to new behavior (but not Flee - that's only triggered by damage)
                        FriendlyShipBehavior newBehavior = GetRandomBehavior();
                        while (newBehavior == FriendlyShipBehavior.Flee)
                        {
                            newBehavior = GetRandomBehavior();
                        }
                        state.Behavior = newBehavior;
                        state.BehaviorTimer = GetBehaviorDuration(newBehavior);
                        
                        if (newBehavior == FriendlyShipBehavior.Idle)
                        {
                            friendlyShip.StopMoving();
                            friendlyShip.IsIdle = true;
                        }
                        else
                        {
                            friendlyShip.IsIdle = false;
                        }
                    }
                }
            }
            
            // Execute current behavior
            var stateForExecute = _getOrCreateShipState(friendlyShip);
            FriendlyShipBehavior currentBehavior = stateForExecute.Behavior;
            
            // Only execute behavior if ship is not actively moving (reached target) or if behavior requires immediate action
            if (!friendlyShip.IsActivelyMoving() || currentBehavior == FriendlyShipBehavior.LongDistance || currentBehavior == FriendlyShipBehavior.Idle || currentBehavior == FriendlyShipBehavior.Flee)
            {
                switch (currentBehavior)
                {
                    case FriendlyShipBehavior.Idle:
                        ExecuteIdleBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Patrol:
                        ExecutePatrolBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.LongDistance:
                        ExecuteLongDistanceBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Wander:
                        ExecuteWanderBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Aggressive:
                        // Aggressive behavior should not be used for friendly ships
                        break;
                    case FriendlyShipBehavior.Flee:
                        friendlyShip.IsFleeing = true; // Ensure flee flag is set while executing flee behavior
                        ExecuteFleeBehavior(friendlyShip);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Update enemy ship behavior
        /// </summary>
        public void UpdateEnemyShipBehavior(EnemyShip enemyShip, float deltaTime)
        {
            if (_getOrCreateEnemyShipState == null || _enemyShipStates == null) return;
            
            // Get or create enemy ship state
            var enemyState = _getOrCreateEnemyShipState(enemyShip);
            
            // Initialize behavior if not set
            if (!_enemyShipStates.ContainsKey(enemyShip))
            {
                enemyState.Behavior = GetRandomBehavior();
                enemyState.BehaviorTimer = GetBehaviorDuration(enemyState.Behavior);
                enemyState.AttackCooldown = 0f;
            }
            
            // Check if player is within detection range
            bool playerDetected = false;
            if (_playerShip != null)
            {
                Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
                float distanceToPlayer = toPlayer.Length();
                playerDetected = distanceToPlayer < EnemyPlayerDetectionRange;
            }
            
            // If player is detected and not fleeing, switch to Aggressive behavior
            if (playerDetected)
            {
                // Don't override Flee behavior - let it continue
                if (enemyState.Behavior != FriendlyShipBehavior.Aggressive && enemyState.Behavior != FriendlyShipBehavior.Flee)
                {
                    enemyState.Behavior = FriendlyShipBehavior.Aggressive;
                    enemyState.BehaviorTimer = float.MaxValue; // Aggressive is permanent until player leaves range
                }
            }
            else
            {
                // Player not detected - use normal behaviors with timers
                // If currently aggressive (and not fleeing), switch back to random behavior
                if (enemyState.Behavior == FriendlyShipBehavior.Aggressive)
                {
                    FriendlyShipBehavior newBehavior = GetRandomBehavior();
                    while (newBehavior == FriendlyShipBehavior.Flee)
                    {
                        newBehavior = GetRandomBehavior();
                    }
                    enemyState.Behavior = newBehavior;
                    enemyState.BehaviorTimer = GetBehaviorDuration(newBehavior);
                }
                
                // Decrement behavior timer for non-aggressive behaviors
                if (enemyState.Behavior != FriendlyShipBehavior.Aggressive)
                {
                    FriendlyShipBehavior enemyCurrentBehavior = enemyState.Behavior;
                    
                    // Only decrement timer if not fleeing (flee timer is managed separately)
                    if (enemyCurrentBehavior != FriendlyShipBehavior.Flee)
                    {
                        enemyState.BehaviorTimer -= deltaTime;
                    }
                    else
                    {
                        // For flee behavior, check if health has regenerated - exit immediately when fully healed
                        if (enemyShip.Health >= enemyShip.MaxHealth)
                        {
                            // Health has regenerated - exit flee immediately
                            FriendlyShipBehavior newBehavior = GetRandomBehavior();
                            while (newBehavior == FriendlyShipBehavior.Flee)
                            {
                                newBehavior = GetRandomBehavior();
                            }
                            enemyState.Behavior = newBehavior;
                            enemyState.BehaviorTimer = GetBehaviorDuration(newBehavior);
                            enemyShip.IsFleeing = false;
                            
                            // Face the direction the ship is moving when resuming behavior
                            if (enemyShip.Velocity.LengthSquared() > 1f)
                            {
                                float targetRotation = (float)Math.Atan2(enemyShip.Velocity.Y, enemyShip.Velocity.X) + MathHelper.PiOver2;
                                enemyShip.Rotation = targetRotation;
                            }
                            
                            if (newBehavior == FriendlyShipBehavior.Idle)
                            {
                                enemyShip.StopMoving();
                                enemyShip.IsIdle = true;
                            }
                            else
                            {
                                enemyShip.IsIdle = false;
                            }
                            
                            System.Console.WriteLine($"[ENEMY] Health regenerated! Exiting flee. Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
                        }
                        else
                        {
                            // Still damaged, continue fleeing and update timer
                            enemyState.BehaviorTimer -= deltaTime;
                        }
                    }
                    
                    // Check if behavior should transition
                    if (enemyState.BehaviorTimer <= 0f)
                    {
                        // If currently fleeing, check if health has regenerated
                        if (enemyCurrentBehavior == FriendlyShipBehavior.Flee)
                        {
                            // Exit if fully healed (health has regenerated)
                            if (enemyShip.Health >= enemyShip.MaxHealth)
                            {
                                // Health has regenerated - exit flee
                                FriendlyShipBehavior newBehavior = GetRandomBehavior();
                                while (newBehavior == FriendlyShipBehavior.Flee)
                                {
                                    newBehavior = GetRandomBehavior();
                                }
                                enemyState.Behavior = newBehavior;
                                enemyState.BehaviorTimer = GetBehaviorDuration(newBehavior);
                                enemyShip.IsFleeing = false;
                                
                                // Face the direction the ship is moving when resuming behavior
                                if (enemyShip.Velocity.LengthSquared() > 1f)
                                {
                                    float targetRotation = (float)Math.Atan2(enemyShip.Velocity.Y, enemyShip.Velocity.X) + MathHelper.PiOver2;
                                    enemyShip.Rotation = targetRotation;
                                }
                                
                                if (newBehavior == FriendlyShipBehavior.Idle)
                                {
                                    enemyShip.StopMoving();
                                    enemyShip.IsIdle = true;
                                }
                                else
                                {
                                    enemyShip.IsIdle = false;
                                }
                                
                                System.Console.WriteLine($"[ENEMY] Health regenerated! Exiting flee. Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
                            }
                            else
                            {
                                // Still damaged, continue fleeing - reset timer
                                enemyState.BehaviorTimer = 10.0f; // Continue fleeing
                                enemyShip.IsFleeing = true; // Keep damage effect active
                            }
                        }
                        else
                        {
                            // Transition to new behavior (but not Flee - that's only triggered by low health)
                            FriendlyShipBehavior newBehavior = GetRandomBehavior();
                            while (newBehavior == FriendlyShipBehavior.Flee)
                            {
                                newBehavior = GetRandomBehavior();
                            }
                            enemyState.Behavior = newBehavior;
                            enemyState.BehaviorTimer = GetBehaviorDuration(newBehavior);
                            
                            if (newBehavior == FriendlyShipBehavior.Idle)
                            {
                                enemyShip.StopMoving();
                                enemyShip.IsIdle = true;
                            }
                            else
                            {
                                enemyShip.IsIdle = false;
                            }
                        }
                    }
                }
            }
            
            // Update attack cooldown
            if (enemyState.AttackCooldown > 0f)
            {
                enemyState.AttackCooldown -= deltaTime;
            }
            
            // Execute current behavior
            FriendlyShipBehavior currentBehavior = enemyState.Behavior;
            
            if (currentBehavior == FriendlyShipBehavior.Aggressive)
            {
                // Execute aggressive behavior
                ExecuteAggressiveBehavior(enemyShip, deltaTime);
            }
            else
            {
                // Execute normal behaviors (same as friendly ships)
                // Only execute behavior if ship is not actively moving (reached target) or if behavior requires immediate action
                if (!enemyShip.IsActivelyMoving() || currentBehavior == FriendlyShipBehavior.LongDistance || currentBehavior == FriendlyShipBehavior.Idle || currentBehavior == FriendlyShipBehavior.Flee)
                {
                    switch (currentBehavior)
                    {
                        case FriendlyShipBehavior.Idle:
                            ExecuteIdleBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Patrol:
                            ExecutePatrolBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.LongDistance:
                            ExecuteLongDistanceBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Wander:
                            ExecuteWanderBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Flee:
                            enemyShip.IsFleeing = true; // Ensure flee flag is set while executing flee behavior
                            ExecuteFleeBehavior((FriendlyShip)enemyShip);
                            break;
                    }
                }
            }
        }
        
        // Helper methods
        private bool IsTooCloseToPlayer(Vector2 position, FriendlyShip friendlyShip)
        {
            if (_playerShip == null) return false;
            
            float distToPlayer = Vector2.Distance(position, _playerShip.Position);
            float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f; // 1.5x player's avoidance range
            
            return distToPlayer < minSafeDistance;
        }
        
        private Vector2 AvoidPlayerPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            // Clamp position to map bounds (keep ships within galaxy map)
            const float mapBoundaryMargin = 200f;
            
            if (_playerShip == null) 
            {
                // Clamp position to map bounds
                return new Vector2(
                    MathHelper.Clamp(position.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                    MathHelper.Clamp(position.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
                );
            }
            
            Vector2 toPosition = position - _playerShip.Position;
            float distToPlayer = toPosition.Length();
            float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f;
            
            if (distToPlayer < minSafeDistance && distToPlayer > 0.1f)
            {
                // Push position away from player
                toPosition.Normalize();
                Vector2 adjustedPosition = _playerShip.Position + toPosition * minSafeDistance;
                
                // Clamp adjusted position to map bounds
                adjustedPosition = new Vector2(
                    MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                    MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
                );
                
                return adjustedPosition;
            }
            
            // Clamp original position to map bounds
            return new Vector2(
                MathHelper.Clamp(position.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(position.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
        }
        
        private bool IsTooCloseToOtherShips(Vector2 position, FriendlyShip friendlyShip)
        {
            if (_friendlyShips == null) return false;
            
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                float distToOtherShip = Vector2.Distance(position, otherShip.Position);
                // Use the larger of the two ships' avoidance ranges for minimum safe distance (no multiplier - must stay outside radius)
                float minSafeDistance = MathHelper.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                
                if (distToOtherShip < minSafeDistance)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private Vector2 AvoidOtherShipsPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            if (_friendlyShips == null) return position;
            
            const float mapBoundaryMargin = 200f;
            Vector2 adjustedPosition = position;
            
            // Check each other ship and adjust position if needed
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                Vector2 toPosition = adjustedPosition - otherShip.Position;
                float distToOtherShip = toPosition.Length();
                // Use the larger of the two ships' avoidance ranges for minimum safe distance (no multiplier - must stay outside radius)
                float minSafeDistance = MathHelper.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                
                if (distToOtherShip < minSafeDistance && distToOtherShip > 0.1f)
                {
                    // Push position away from other ship
                    toPosition.Normalize();
                    adjustedPosition = otherShip.Position + toPosition * minSafeDistance;
                }
            }
            
            // Clamp to map bounds
            adjustedPosition = new Vector2(
                MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
            
            return adjustedPosition;
        }
        
        // Execute behavior methods
        private void ExecuteIdleBehavior(FriendlyShip friendlyShip)
        {
            // Idle: Ship stops and uses drift
            // Stop the ship by setting target to current position
            friendlyShip.StopMoving();
            // Ship will now use drift if Drift > 0 (handled in FriendlyShip.Update)
        }
        
        private void ExecutePatrolBehavior(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null) return;
            
            // Patrol: Ship moves between waypoints in a small area
            var shipState = _getOrCreateShipState(friendlyShip);
            if (shipState.PatrolPoints.Count == 0)
            {
                // Initialize patrol points around current position
                InitializePatrolPoints(friendlyShip);
            }
            
            var patrolPoints = shipState.PatrolPoints;
            
            // If ship reached current target, move to next patrol point
            if (!friendlyShip.IsActivelyMoving())
            {
                // Find next patrol point (cycle through them)
                Vector2 currentPos = friendlyShip.Position;
                int closestIndex = 0;
                float closestDist = float.MaxValue;
                
                for (int i = 0; i < patrolPoints.Count; i++)
                {
                    float dist = Vector2.Distance(currentPos, patrolPoints[i]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestIndex = i;
                    }
                }
                
                // Move to next point in sequence
                int nextIndex = (closestIndex + 1) % patrolPoints.Count;
                Vector2 nextTarget = patrolPoints[nextIndex];
                
                // Ensure target is far enough for smooth turning (minimum 500 pixels)
                float distanceToTarget = Vector2.Distance(currentPos, nextTarget);
                if (distanceToTarget < 500f)
                {
                    // Target too close - skip to next point or extend direction
                    int skipIndex = (nextIndex + 1) % patrolPoints.Count;
                    Vector2 skipTarget = patrolPoints[skipIndex];
                    float skipDistance = Vector2.Distance(currentPos, skipTarget);
                    
                    if (skipDistance >= 500f)
                    {
                        nextTarget = skipTarget;
                    }
                    else
                    {
                        // Extend direction to minimum distance
                        Vector2 direction = nextTarget - currentPos;
                        if (direction.LengthSquared() > 0.1f)
                        {
                            direction.Normalize();
                            nextTarget = currentPos + direction * 500f;
                            // Clamp to map bounds (keep ships within galaxy map)
                            const float margin = 200f;
                            nextTarget = new Vector2(
                                MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                                MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                            );
                        }
                    }
                }
                
                // Avoid player's radius - adjust target if too close
                if (IsTooCloseToPlayer(nextTarget, friendlyShip))
                {
                    nextTarget = AvoidPlayerPosition(nextTarget, friendlyShip);
                    // Clamp to map bounds after adjustment
                    const float margin = 200f;
                    nextTarget = new Vector2(
                        MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                    );
                }
                
                // Avoid other ships' radius - adjust target if too close
                if (IsTooCloseToOtherShips(nextTarget, friendlyShip))
                {
                    nextTarget = AvoidOtherShipsPosition(nextTarget, friendlyShip);
                    // Clamp to map bounds after adjustment
                    const float margin = 200f;
                    nextTarget = new Vector2(
                        MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                var shipStateForPatrol = _getOrCreateShipState(friendlyShip);
                shipStateForPatrol.OriginalDestination = Vector2.Zero;
                shipStateForPatrol.AStarPath.Clear();
                shipStateForPatrol.CurrentWaypointIndex = 0;
                friendlyShip.SetTargetPosition(nextTarget);
            }
        }
        
        private void InitializePatrolPoints(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null) return;
            
            // Create 3-5 patrol waypoints in a circular pattern around current position
            // Minimum distance: ship needs ~400-500 pixels to turn smoothly (180° turn at 300px/s speed and 3 rad/s rotation)
            Vector2 center = friendlyShip.Position;
            int numPoints = _random.Next(3, 6); // 3-5 points
            float patrolRadius = (float)(_random.NextDouble() * 600f + 600f); // 600-1200 pixel radius (ensures ships can turn smoothly)
            
            var points = new List<Vector2>();
            for (int i = 0; i < numPoints; i++)
            {
                float angle = (float)(i * MathHelper.TwoPi / numPoints + _random.NextDouble() * 0.5f); // Add some randomness
                Vector2 point = center + new Vector2(
                    (float)Math.Cos(angle) * patrolRadius,
                    (float)Math.Sin(angle) * patrolRadius
                );
                
                // Clamp to map bounds
                const float margin = 200f;
                point = new Vector2(
                    MathHelper.Clamp(point.X, margin, MapSize - margin),
                    MathHelper.Clamp(point.Y, margin, MapSize - margin)
                );
                
                // Ensure minimum distance from center (at least 500 pixels for smooth turning)
                float distanceFromCenter = Vector2.Distance(point, center);
                if (distanceFromCenter < 500f)
                {
                    // Extend point to minimum distance
                    Vector2 direction = point - center;
                    if (direction.LengthSquared() > 0.1f)
                    {
                        direction.Normalize();
                        point = center + direction * 500f;
                        // Re-clamp after extension
                        point = new Vector2(
                            MathHelper.Clamp(point.X, margin, MapSize - margin),
                            MathHelper.Clamp(point.Y, margin, MapSize - margin)
                        );
                    }
                }
                
                // Avoid player's radius when creating patrol points
                if (_playerShip != null)
                {
                    if (IsTooCloseToPlayer(point, friendlyShip))
                    {
                        point = AvoidPlayerPosition(point, friendlyShip);
                        // Re-clamp after adjustment
                        point = new Vector2(
                            MathHelper.Clamp(point.X, margin, MapSize - margin),
                            MathHelper.Clamp(point.Y, margin, MapSize - margin)
                        );
                    }
                }
                
                // Avoid other ships' radius when creating patrol points
                if (IsTooCloseToOtherShips(point, friendlyShip))
                {
                    point = AvoidOtherShipsPosition(point, friendlyShip);
                    // Re-clamp after adjustment
                    point = new Vector2(
                        MathHelper.Clamp(point.X, margin, MapSize - margin),
                        MathHelper.Clamp(point.Y, margin, MapSize - margin)
                    );
                }
                
                points.Add(point);
            }
            
            var shipState = _getOrCreateShipState(friendlyShip);
            shipState.PatrolPoints.Clear();
            shipState.PatrolPoints.AddRange(points);
        }
        
        private void ExecuteLongDistanceBehavior(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null) return;
            
            // LongDistance: Ship flies one long path across most of the map
            if (!friendlyShip.IsActivelyMoving())
            {
                Vector2 currentPos = friendlyShip.Position;
                const float minDistance = MapSize * 0.75f; // At least 75% of the map (6144 pixels) - longer paths
                const float maxDistance = MapSize * 1.5f; // Up to 1.5x map size for very long edge-to-edge paths
                Vector2 targetPos;
                int attempts = 0;
                const int maxAttempts = 20;
                
                do
                {
                    // Pick a random direction
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    // Pick a random distance between min and max
                    float distance = (float)(_random.NextDouble() * (maxDistance - minDistance) + minDistance);
                    
                    // Calculate target position
                    targetPos = currentPos + direction * distance;
                    
                    // Check if target is within map bounds (keep ships within galaxy map)
                    const float margin = 200f;
                    bool isValid = targetPos.X >= margin && targetPos.X <= MapSize - margin &&
                                   targetPos.Y >= margin && targetPos.Y <= MapSize - margin;
                    
                    if (isValid)
                    {
                        // Verify the distance is at least half the map
                        float actualDistance = Vector2.Distance(currentPos, targetPos);
                        if (actualDistance >= minDistance)
                        {
                            // Check if target is too close to player or other ships
                            if (!IsTooCloseToPlayer(targetPos, friendlyShip) && !IsTooCloseToOtherShips(targetPos, friendlyShip))
                            {
                                break;
                            }
                        }
                    }
                    
                    attempts++;
                } while (attempts < maxAttempts);
                
                // If we couldn't find a valid target after max attempts, use a guaranteed long path
                if (attempts >= maxAttempts)
                {
                    // Pick a random direction and ensure it's at least half the map
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    // Calculate target that extends to map edge or beyond
                    float distance = minDistance;
                    targetPos = currentPos + direction * distance;
                    
                    // Ensure path stays within map bounds while being long
                    // Calculate maximum distance we can travel in this direction while staying in bounds
                    float maxDistX = direction.X > 0 ? (MapSize - 200f - currentPos.X) / Math.Max(direction.X, 0.001f) : (currentPos.X - 200f) / Math.Min(direction.X, -0.001f);
                    float maxDistY = direction.Y > 0 ? (MapSize - 200f - currentPos.Y) / Math.Max(direction.Y, 0.001f) : (currentPos.Y - 200f) / Math.Min(direction.Y, -0.001f);
                    float maxDist = Math.Min(maxDistX, maxDistY);
                    
                    // Use the minimum of desired distance and maximum safe distance
                    distance = Math.Min(distance, Math.Max(maxDist, minDistance));
                    targetPos = currentPos + direction * distance;
                }
                
                // Clamp target to map bounds (keep ships within galaxy map)
                const float targetMargin = 200f;
                targetPos = new Vector2(
                    MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                    MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                );
                
                // Avoid player's radius - adjust target if too close
                if (IsTooCloseToPlayer(targetPos, friendlyShip))
                {
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip);
                    // Re-clamp after adjustment to keep within map bounds
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                }
                
                // Avoid other ships' radius - adjust target if too close
                if (IsTooCloseToOtherShips(targetPos, friendlyShip))
                {
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip);
                    // Re-clamp after adjustment to keep within map bounds
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                var shipStateForLongDistClear2 = _getOrCreateShipState(friendlyShip);
                shipStateForLongDistClear2.OriginalDestination = Vector2.Zero;
                shipStateForLongDistClear2.AStarPath.Clear();
                shipStateForLongDistClear2.CurrentWaypointIndex = 0;
                friendlyShip.SetTargetPosition(targetPos);
            }
        }
        
        private void ExecuteFleeBehavior(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null || _enemyShips == null) return;
            
            // Check if should exit flee BEFORE executing flee behavior
            // Exit if fully healed (100% health) - health has regenerated
            if (friendlyShip.Health >= friendlyShip.MaxHealth)
            {
                // Don't execute flee - let the timer update handle the exit
                return;
            }
            
            // Flee: Ship continuously tries to escape from nearest threat (player or enemy)
            Vector2? nearestThreatPos = null;
            float nearestThreatDistance = float.MaxValue;
            
            // Check player as threat
            if (_playerShip != null && _playerShip.IsActive)
            {
                float distanceToPlayer = Vector2.Distance(friendlyShip.Position, _playerShip.Position);
                if (distanceToPlayer < nearestThreatDistance)
                {
                    nearestThreatDistance = distanceToPlayer;
                    nearestThreatPos = _playerShip.Position;
                }
            }
            
            // Check enemy ships as threats (for friendly ships)
            if (friendlyShip is not EnemyShip)
            {
                foreach (var enemyShip in _enemyShips)
                {
                    if (!enemyShip.IsActive) continue;
                    float distanceToEnemy = Vector2.Distance(friendlyShip.Position, enemyShip.Position);
                    if (distanceToEnemy < nearestThreatDistance)
                    {
                        nearestThreatDistance = distanceToEnemy;
                        nearestThreatPos = enemyShip.Position;
                    }
                }
            }
            
            // If no threat found, stop fleeing
            if (!nearestThreatPos.HasValue) return;
            
            // Calculate direction away from nearest threat
            Vector2 awayFromThreat = friendlyShip.Position - nearestThreatPos.Value;
            float distanceToThreat = awayFromThreat.Length();
            
            if (distanceToThreat > 0.1f)
            {
                awayFromThreat.Normalize();
                
                // Set aim target in the flee direction so ship immediately turns away from threat
                Vector2 fleeAimTarget = friendlyShip.Position + awayFromThreat * 1000f; // Aim point far in flee direction
                friendlyShip.SetAimTarget(fleeAimTarget);
                
                // Only update target if ship has reached current target or threat is getting closer
                // This prevents constant target recalculation that causes erratic movement
                var shipStateForFlee = _getOrCreateShipState(friendlyShip);
                bool needsNewTarget = false;
                
                if (shipStateForFlee.OriginalDestination == Vector2.Zero)
                {
                    // First time fleeing - set initial target
                    needsNewTarget = true;
                }
                else
                {
                    // Check if ship has reached its current flee target
                    float distanceToCurrentTarget = Vector2.Distance(friendlyShip.Position, shipStateForFlee.OriginalDestination);
                    if (distanceToCurrentTarget < 200f) // Reached target
                    {
                        needsNewTarget = true;
                    }
                    // Or if threat is getting closer (within 500 pixels of current position)
                    else if (distanceToThreat < 500f)
                    {
                        // Check if threat moved closer since last update
                        float distanceToThreatFromTarget = Vector2.Distance(shipStateForFlee.OriginalDestination, nearestThreatPos.Value);
                        if (distanceToThreatFromTarget < distanceToThreat + 300f) // Threat is getting closer to our target
                        {
                            needsNewTarget = true;
                        }
                    }
                }
                
                if (needsNewTarget)
                {
                    // Calculate stable flee target - use fixed distance for smoother movement
                    float fleeDistance = 2000f; // Fixed distance for consistent movement
                    Vector2 fleeTarget = friendlyShip.Position + awayFromThreat * fleeDistance;
                    
                    // Clamp to map bounds
                    const float margin = 200f;
                    fleeTarget = new Vector2(
                        MathHelper.Clamp(fleeTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(fleeTarget.Y, margin, MapSize - margin)
                    );
                    
                    // Avoid other ships' radius when setting flee target (but don't recalculate if already avoiding)
                    if (IsTooCloseToOtherShips(fleeTarget, friendlyShip))
                    {
                        fleeTarget = AvoidOtherShipsPosition(fleeTarget, friendlyShip);
                        fleeTarget = new Vector2(
                            MathHelper.Clamp(fleeTarget.X, margin, MapSize - margin),
                            MathHelper.Clamp(fleeTarget.Y, margin, MapSize - margin)
                        );
                    }
                    
                    // Store the target so we don't recalculate every frame
                    shipStateForFlee.OriginalDestination = fleeTarget;
                    
                    // Clear A* pathfinding when fleeing to prevent getting stuck
                    shipStateForFlee.AStarPath.Clear();
                    shipStateForFlee.CurrentWaypointIndex = 0;
                    
                    // Update target position
                    friendlyShip.SetTargetPosition(fleeTarget);
                    friendlyShip.IsIdle = false; // Ensure ship is moving
                }
            }
        }
        
        private void ExecuteWanderBehavior(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null) return;
            
            // Wander: Ship moves randomly within map bounds, avoiding center and player
            if (!friendlyShip.IsActivelyMoving())
            {
                Vector2 currentPos = friendlyShip.Position;
                Vector2 mapCenter = new Vector2(MapSize / 2f, MapSize / 2f);
                Vector2 toCenter = mapCenter - currentPos;
                float distanceToCenter = toCenter.Length();
                toCenter.Normalize();
                
                Vector2 targetPos;
                int attempts = 0;
                const int maxAttempts = 10;
                float minDistanceFromPlayer = friendlyShip.AvoidanceDetectionRange * 3f;
                const float minDistanceFromCenter = 1000f;
                
                do
                {
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 randomDirection = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    float centerBiasStrength = MathHelper.Clamp(1f - (distanceToCenter / (MapSize * 0.3f)), 0.3f, 0.8f);
                    Vector2 awayFromCenter = -toCenter;
                    Vector2 blendedDirection = randomDirection * (1f - centerBiasStrength) + awayFromCenter * centerBiasStrength;
                    
                    if (_playerShip != null)
                    {
                        Vector2 awayFromPlayer = currentPos - _playerShip.Position;
                        float distToPlayer = awayFromPlayer.Length();
                        if (distToPlayer > 0.1f)
                        {
                            awayFromPlayer.Normalize();
                            float playerBias = MathHelper.Clamp((minDistanceFromPlayer - distToPlayer) / minDistanceFromPlayer, 0f, 0.8f);
                            blendedDirection = blendedDirection * (1f - playerBias) + awayFromPlayer * playerBias;
                        }
                    }
                    
                    blendedDirection.Normalize();
                    
                    // Make paths longer and smoother by considering current direction
                    var shipState = _getOrCreateShipState(friendlyShip);
                    Vector2 currentDirection = Vector2.Zero;
                    if (shipState.LastDirection != Vector2.Zero)
                    {
                        currentDirection = shipState.LastDirection;
                        // Blend new direction with current direction for smoother paths (70% new, 30% current)
                        blendedDirection = blendedDirection * 0.7f + currentDirection * 0.3f;
                        blendedDirection.Normalize();
                    }
                    
                    // Longer paths: 1000-2000 pixels (minimum 1000 to ensure ships can turn smoothly)
                    // Ship needs ~400-500 pixels minimum to complete a 180° turn while moving
                    float targetDistance = (float)(_random.NextDouble() * 1000f + 1000f);
                    Vector2 targetOffset = blendedDirection * targetDistance;
                    targetPos = currentPos + targetOffset;
                    
                    // Store direction for next path calculation
                    shipState.LastDirection = blendedDirection;
                    
                    const float targetMargin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                    
                    attempts++;
                    
                    float distToCenter = Vector2.Distance(targetPos, mapCenter);
                    if (distToCenter < minDistanceFromCenter)
                    {
                        if (attempts < maxAttempts)
                            continue;
                    }
                    
                    // Check if target is safe from player and other ships
                    bool safeFromPlayer = true;
                    if (_playerShip != null)
                    {
                        float distToPlayer = Vector2.Distance(targetPos, _playerShip.Position);
                        float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f; // 1.5x player's avoidance range
                        safeFromPlayer = distToPlayer >= minSafeDistance;
                    }
                    
                    bool safeFromOtherShips = !IsTooCloseToOtherShips(targetPos, friendlyShip);
                    
                    if (safeFromPlayer && safeFromOtherShips && distToCenter >= minDistanceFromCenter)
                    {
                        break;
                    }
                    else if (attempts >= maxAttempts)
                    {
                        break;
                    }
                } while (attempts < maxAttempts);
                
                // Ensure target avoids player's radius
                if (IsTooCloseToPlayer(targetPos, friendlyShip))
                {
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip);
                    // Re-clamp to map bounds
                    const float margin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, margin, MapSize - margin),
                        MathHelper.Clamp(targetPos.Y, margin, MapSize - margin)
                    );
                }
                
                // Ensure target avoids other ships' radius
                if (IsTooCloseToOtherShips(targetPos, friendlyShip))
                {
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip);
                    // Re-clamp to map bounds
                    const float margin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, margin, MapSize - margin),
                        MathHelper.Clamp(targetPos.Y, margin, MapSize - margin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                var shipStateForWanderClear = _getOrCreateShipState(friendlyShip);
                shipStateForWanderClear.OriginalDestination = Vector2.Zero;
                shipStateForWanderClear.AStarPath.Clear();
                shipStateForWanderClear.CurrentWaypointIndex = 0;
                friendlyShip.SetTargetPosition(targetPos);
            }
        }
        
        private void ExecuteAggressiveBehavior(EnemyShip enemyShip, float deltaTime)
        {
            if (_playerShip == null || _combatManager == null || _getOrCreateEnemyShipState == null) return;
            
            // Calculate distance to player
            Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
            float distanceToPlayer = toPlayer.Length();
            
            // Attack range: if within range, attack the player
            const float attackRange = 800f; // Range at which enemy will attack
            const float attackCooldownTime = 1.5f; // Time between attacks (seconds)
            const float pursuitRange = 2000f; // Range at which enemy will pursue player
            
            if (distanceToPlayer < pursuitRange && distanceToPlayer > 0.1f)
            {
                toPlayer.Normalize();
                Vector2 targetPosition = _playerShip.Position;
                
                // Calculate safe distances
                float playerAvoidanceRadius = _playerShip.AvoidanceDetectionRange;
                float enemyAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
                float effectiveAvoidanceRadius = MathHelper.Max(playerAvoidanceRadius, enemyAvoidanceRadius);
                const float preferredAttackDistance = 600f; // Preferred distance for attacking (outside avoidance radius but close enough to attack)
                float tooCloseDistance = effectiveAvoidanceRadius * 1.5f; // If closer than this, fly away
                float minAttackDistance = effectiveAvoidanceRadius * 1.2f; // Minimum safe distance
                
                if (distanceToPlayer < tooCloseDistance)
                {
                    // Too close - fly away from player
                    float backAwayDistance = (tooCloseDistance - distanceToPlayer) * 2f; // Back away 2x the overlap
                    targetPosition = enemyShip.Position - toPlayer * backAwayDistance;
                    
                    // Ensure we back away to at least the preferred attack distance
                    Vector2 awayDirection = -toPlayer;
                    float distanceToPreferred = preferredAttackDistance - distanceToPlayer;
                    if (distanceToPreferred > 0)
                    {
                        targetPosition = enemyShip.Position + awayDirection * distanceToPreferred;
                    }
                }
                else if (distanceToPlayer > preferredAttackDistance)
                {
                    // Too far - move closer to preferred attack distance
                    Vector2 directionToPlayer = toPlayer;
                    float approachDistance = distanceToPlayer - preferredAttackDistance;
                    targetPosition = enemyShip.Position + directionToPlayer * Math.Min(approachDistance, 500f); // Approach gradually
                }
                else
                {
                    // At good distance - maintain position relative to player (orbit or strafe)
                    // Try to maintain preferred attack distance while staying mobile
                    Vector2 perpendicular = new Vector2(-toPlayer.Y, toPlayer.X); // Perpendicular direction
                    perpendicular.Normalize();
                    // Add some perpendicular movement to make enemies strafe around player
                    targetPosition = _playerShip.Position - toPlayer * preferredAttackDistance + perpendicular * 200f;
                }
                
                enemyShip.SetTargetPosition(targetPosition);
                enemyShip.IsIdle = false; // Ensure ship is not idle
                
                // Always face the player when in aggressive mode
                enemyShip.SetAimTarget(_playerShip.Position);
                
                // Attack if within range and cooldown is ready
                var enemyStateForAttack = _getOrCreateEnemyShipState(enemyShip);
                if (distanceToPlayer < attackRange && enemyStateForAttack.AttackCooldown <= 0f)
                {
                    // Fire laser at player
                    FireEnemyLaser(enemyShip);
                    enemyStateForAttack.AttackCooldown = attackCooldownTime;
                }
            }
            else
            {
                // Player out of range - clear aim target so ship can rotate normally
                enemyShip.SetAimTarget(null);
            }
        }
        
        private void FireEnemyLaser(EnemyShip enemyShip)
        {
            if (enemyShip == null || _combatManager == null) return;
            
            var shipTexture = enemyShip.GetTexture();
            if (shipTexture == null) return;
            
            float textureCenterX = shipTexture.Width / 2f;
            float textureCenterY = shipTexture.Height / 2f;
            float shipRotation = enemyShip.Rotation;
            float cos = (float)Math.Cos(shipRotation);
            float sin = (float)Math.Sin(shipRotation);
            
            // Fire laser from center of ship (front)
            float spriteX = textureCenterX;
            float spriteY = 20f; // Near the front of the ship
            
            // Convert sprite coordinates to offset from ship center
            float offsetX = spriteX - textureCenterX;
            float offsetY = spriteY - textureCenterY;
            
            // Rotate the offset by ship's rotation to get world-space offset
            float rotatedX = offsetX * cos - offsetY * sin;
            float rotatedY = offsetX * sin + offsetY * cos;
            
            // Calculate laser spawn position
            Vector2 laserSpawnPosition = enemyShip.Position + new Vector2(rotatedX, rotatedY);
            
            // Fire laser using combat manager
            _combatManager.FireLaser(laserSpawnPosition, shipRotation, enemyShip.Damage, enemyShip);
        }
    }
}

