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
        private const int MaxPathPoints = 100; // Maximum number of path points to store per ship
        
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
        private PathfindingManager? _pathfindingManager;
        private CollisionManager? _collisionManager;
        private CombatManager? _combatManager;
        private WeaponsManager? _weaponsManager;
        
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
            PathfindingManager? pathfindingManager,
            CollisionManager? collisionManager,
            CombatManager? combatManager,
            WeaponsManager? weaponsManager,
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
            _pathfindingManager = pathfindingManager;
            _collisionManager = collisionManager;
            _combatManager = combatManager;
            _weaponsManager = weaponsManager;
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
        
        private Vector2 AvoidPlayerPosition(Vector2 position, FriendlyShip friendlyShip, float mapSize)
        {
            // Clamp position to map bounds (keep ships within galaxy map)
            const float mapBoundaryMargin = 200f;
            
            if (_playerShip == null) 
            {
                // Clamp position to map bounds
                return new Vector2(
                    MathHelper.Clamp(position.X, mapBoundaryMargin, mapSize - mapBoundaryMargin),
                    MathHelper.Clamp(position.Y, mapBoundaryMargin, mapSize - mapBoundaryMargin)
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
                    MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, mapSize - mapBoundaryMargin),
                    MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, mapSize - mapBoundaryMargin)
                );
                
                return adjustedPosition;
            }
            
            // Clamp original position to map bounds
            return new Vector2(
                MathHelper.Clamp(position.X, mapBoundaryMargin, mapSize - mapBoundaryMargin),
                MathHelper.Clamp(position.Y, mapBoundaryMargin, mapSize - mapBoundaryMargin)
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
        
        private Vector2 AvoidOtherShipsPosition(Vector2 position, FriendlyShip friendlyShip, float mapSize)
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
                MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, mapSize - mapBoundaryMargin),
                MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, mapSize - mapBoundaryMargin)
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
                    nextTarget = AvoidPlayerPosition(nextTarget, friendlyShip, MapSize);
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
                    nextTarget = AvoidOtherShipsPosition(nextTarget, friendlyShip, MapSize);
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
                        point = AvoidPlayerPosition(point, friendlyShip, MapSize);
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
                    point = AvoidOtherShipsPosition(point, friendlyShip, MapSize);
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
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip, MapSize);
                    // Re-clamp after adjustment to keep within map bounds
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                }
                
                // Avoid other ships' radius - adjust target if too close
                if (IsTooCloseToOtherShips(targetPos, friendlyShip))
                {
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip, MapSize);
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
                        fleeTarget = AvoidOtherShipsPosition(fleeTarget, friendlyShip, MapSize);
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
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip, MapSize);
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
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip, MapSize);
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
                    // Fire weapon at player
                    _weaponsManager?.FireEnemyWeapon(enemyShip);
                    enemyStateForAttack.AttackCooldown = attackCooldownTime;
                }
            }
            else
            {
                // Player out of range - clear aim target so ship can rotate normally
                enemyShip.SetAimTarget(null);
            }
        }
        
        /// <summary>
        /// Update all ships (friendly and enemy) - handles behavior, collision avoidance, and ship updates
        /// This is the main entry point for all ship updates (except player)
        /// </summary>
        public void UpdateAllShips(GameTime gameTime, float mapSize)
        {
            if (_friendlyShips == null || _enemyShips == null) return;
            if (_getOrCreateShipState == null || _getOrCreateEnemyShipState == null) return;

            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update all friendly ships
            foreach (var friendlyShip in _friendlyShips)
            {
                // Handle collision avoidance and movement
                HandleFriendlyShipCollisionAvoidance(friendlyShip, deltaTime, mapSize);
                
                // Update ship position and rotation
                friendlyShip.Update(gameTime);
                
                // Ship-to-ship collision detection and resolution
                _collisionManager?.HandleFriendlyShipCollisions(friendlyShip, _friendlyShips);
                _collisionManager?.HandleFriendlyPlayerCollision(friendlyShip, _playerShip);
                
                // Update behavior system
                UpdateFriendlyShipBehavior(friendlyShip, deltaTime);
                
                // Track last position and direction for smooth pathing
                UpdateShipTracking(friendlyShip);
            }

            // Update all enemy ships
            foreach (var enemyShip in _enemyShips)
            {
                // Handle collision avoidance and movement
                HandleEnemyShipCollisionAvoidance(enemyShip, deltaTime, mapSize);
                
                // Update ship position and rotation
                enemyShip.Update(gameTime);
                
                // Ship-to-ship collision detection and resolution
                _collisionManager?.HandleEnemyFriendlyCollision(enemyShip, _friendlyShips);
                _collisionManager?.HandleEnemyPlayerCollision(enemyShip, _playerShip);
                _collisionManager?.HandleEnemyShipCollisions(enemyShip, _enemyShips);
                
                // Update behavior system
                UpdateEnemyShipBehavior(enemyShip, deltaTime);
                
                // Clamp position AFTER behavior system
                _collisionManager?.ClampEnemyShipToMapBounds(enemyShip);
            }
        }

        private void HandleFriendlyShipCollisionAvoidance(FriendlyShip friendlyShip, float deltaTime, float mapSize)
        {
            if (_friendlyShips == null || _getOrCreateShipState == null) return;

            // Collision avoidance: steer away from player (ship-to-ship collision disabled)
            // Use each ship's own avoidance detection range
            float playerAvoidanceRadius = friendlyShip.AvoidanceDetectionRange * 1.33f; // 33% larger radius for player avoidance
            float playerAvoidanceForce = 300f; // Stronger avoidance for player
            Vector2 avoidanceVector = Vector2.Zero;
            
            // Orbit around player ship - create slow circular motion
            if (_playerShip != null)
            {
                Vector2 toPlayer = _playerShip.Position - friendlyShip.Position;
                float distance = toPlayer.Length();
                
                // Calculate look-ahead target for player avoidance check
                Vector2 lookAheadTargetForPlayer = friendlyShip.Position;
                if (friendlyShip.IsActivelyMoving())
                {
                    float shipRotation = friendlyShip.Rotation;
                    Vector2 lookAheadDirection = new Vector2(
                        (float)Math.Sin(shipRotation),
                        -(float)Math.Cos(shipRotation)
                    );
                    float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                    lookAheadTargetForPlayer = friendlyShip.Position + lookAheadDirection * lookAheadDist;
                }
                
                // Check if look-ahead target is within player's avoidance radius
                Vector2 toLookAheadFromPlayer = lookAheadTargetForPlayer - _playerShip.Position;
                float lookAheadDistanceFromPlayer = toLookAheadFromPlayer.Length();
                float playerAvoidanceRadiusForLookAhead = _playerShip.AvoidanceDetectionRange * 1.33f; // 33% larger radius for player avoidance
                bool lookAheadInPlayerRadius = lookAheadDistanceFromPlayer < playerAvoidanceRadiusForLookAhead;
                
                // Use a larger range for orbital behavior
                float orbitRadius = friendlyShip.AvoidanceDetectionRange * 1.5f; // Orbit at 1.5x avoidance range
                
                if ((distance < orbitRadius || lookAheadInPlayerRadius) && distance > 0.1f)
                {
                    // Calculate desired orbit distance (maintain distance from player)
                    float desiredDistance = friendlyShip.AvoidanceDetectionRange * 1.2f; // Orbit at 1.2x avoidance range
                    
                    // Calculate tangential direction (perpendicular to direction to player) for orbital motion
                    Vector2 tangential = new Vector2(-toPlayer.Y, toPlayer.X);
                    tangential.Normalize();
                    
                    // Calculate radial direction (toward/away from player to maintain orbit distance)
                    Vector2 radialDirection = toPlayer;
                    radialDirection.Normalize();
                    
                    // Calculate distance error (how far from desired orbit distance)
                    float distanceError = distance - desiredDistance;
                    
                    // Blend tangential (orbital) motion with radial (distance correction) motion
                    float tangentialWeight = 0.85f; // 85% tangential (orbital), 15% radial (distance correction)
                    Vector2 orbitalDirection = tangential * tangentialWeight + radialDirection * (1f - tangentialWeight) * Math.Sign(-distanceError);
                    orbitalDirection.Normalize();
                    
                    // Calculate orbital force (gentle, slow turning)
                    float orbitalStrength = MathHelper.Clamp(Math.Abs(distanceError) / desiredDistance, 0.3f, 1f);
                    float slowOrbitalForce = playerAvoidanceForce * 0.4f; // 40% of normal force for slow turning
                    
                    // Increase force if look-ahead is in player's radius (start turning immediately)
                    if (lookAheadInPlayerRadius)
                    {
                        float lookAheadPenetration = (playerAvoidanceRadiusForLookAhead - lookAheadDistanceFromPlayer) / playerAvoidanceRadiusForLookAhead;
                        orbitalStrength = MathHelper.Clamp(orbitalStrength + lookAheadPenetration * 0.5f, 0.5f, 1.5f);
                        slowOrbitalForce = playerAvoidanceForce * (0.4f + lookAheadPenetration * 0.3f); // Increase force up to 70% when look-ahead is in radius
                    }
                    
                    avoidanceVector += orbitalDirection * orbitalStrength * slowOrbitalForce;
                }
            }
            
            // Avoid other friendly ships with orbital motion (orbit around each other when navigating past)
            float avoidanceRadius = friendlyShip.AvoidanceDetectionRange; // Use ship's own setting
            float avoidanceForce = 300f; // Increased avoidance force for better steering
            
            // Calculate look-ahead target position (where the ship is looking ahead)
            Vector2 lookAheadTarget = friendlyShip.Position;
            if (friendlyShip.IsActivelyMoving())
            {
                // Calculate look-ahead target in the direction the ship is facing
                float shipRotation = friendlyShip.Rotation;
                Vector2 lookAheadDirection = new Vector2(
                    (float)Math.Sin(shipRotation),
                    -(float)Math.Cos(shipRotation)
                );
                float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                lookAheadTarget = friendlyShip.Position + lookAheadDirection * lookAheadDist;
            }
            
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                Vector2 toOtherShip = otherShip.Position - friendlyShip.Position;
                float distance = toOtherShip.Length();
                
                // Use the larger of the two ships' avoidance ranges to ensure ships never enter each other's radius
                float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherShip.AvoidanceDetectionRange);
                
                // Check if look-ahead target is within other ship's avoidance radius
                Vector2 toLookAheadFromOther = lookAheadTarget - otherShip.Position;
                float lookAheadDistanceFromOther = toLookAheadFromOther.Length();
                bool lookAheadInRadius = lookAheadDistanceFromOther < effectiveAvoidanceRadius;
                
                // Proactive avoidance: start veering away when approaching the avoidance radius (1.5x radius for early detection)
                float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                
                if ((distance < avoidanceDetectionRange || lookAheadInRadius) && distance > 0.1f)
                {
                    toOtherShip.Normalize();
                    
                    // Calculate radial direction (away from other ship)
                    Vector2 radialDirection = -toOtherShip;
                    
                    // Calculate tangential direction (perpendicular for orbital motion)
                    Vector2 tangentialDirection;
                    if (friendlyShip.IsActivelyMoving())
                    {
                        // Use ship's velocity to determine orbit direction
                        Vector2 shipVelocity = friendlyShip.Velocity;
                        if (shipVelocity.LengthSquared() < 1f)
                        {
                            // If not moving much, try to use last position
                            var shipStateForVel = _getOrCreateShipState(friendlyShip);
                            if (shipStateForVel.LastPosition != Vector2.Zero)
                            {
                                shipVelocity = friendlyShip.Position - shipStateForVel.LastPosition;
                            }
                            
                            if (shipVelocity.LengthSquared() < 1f)
                            {
                                // If still not moving, use a default orbit direction
                                tangentialDirection = new Vector2(-toOtherShip.Y, toOtherShip.X);
                            }
                            else
                            {
                                shipVelocity.Normalize();
                                // Choose tangential direction that's closer to ship's movement direction
                                Vector2 tan1 = new Vector2(-toOtherShip.Y, toOtherShip.X);
                                Vector2 tan2 = new Vector2(toOtherShip.Y, -toOtherShip.X);
                                
                                float dot1 = Vector2.Dot(shipVelocity, tan1);
                                float dot2 = Vector2.Dot(shipVelocity, tan2);
                                
                                tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                            }
                        }
                        else
                        {
                            shipVelocity.Normalize();
                            // Choose tangential direction that's closer to ship's movement direction
                            Vector2 tan1 = new Vector2(-toOtherShip.Y, toOtherShip.X);
                            Vector2 tan2 = new Vector2(toOtherShip.Y, -toOtherShip.X);
                            
                            float dot1 = Vector2.Dot(shipVelocity, tan1);
                            float dot2 = Vector2.Dot(shipVelocity, tan2);
                            
                            tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                        }
                    }
                    else
                    {
                        // Default to clockwise orbit
                        tangentialDirection = new Vector2(-toOtherShip.Y, toOtherShip.X);
                    }
                    tangentialDirection.Normalize();
                    
                    // Calculate base avoidance force for this collision
                    float currentAvoidanceForce = avoidanceForce;
                    
                    // Calculate avoidance strength based on distance from avoidance radius
                    float avoidanceStrength;
                    
                    if (lookAheadInRadius)
                    {
                        // Look-ahead target is in avoidance radius - start turning immediately
                        float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromOther) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.5f + lookAheadPenetration * 2f; // Scale from 1.5 to 3.5
                        currentAvoidanceForce = avoidanceForce * 1.5f; // Strong force to turn away
                    }
                    else if (distance < effectiveAvoidanceRadius)
                    {
                        // Inside avoidance radius - very strong avoidance
                        avoidanceStrength = 1f + ((effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius) * 3f;
                        currentAvoidanceForce = 500f; // Strong force to push away
                    }
                    else
                    {
                        // Approaching avoidance radius - moderate avoidance that increases as we get closer
                        float approachFactor = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                        avoidanceStrength = approachFactor * 1.5f; // Scale from 0 to 1.5
                        currentAvoidanceForce = avoidanceForce * (1f + approachFactor); // Increase force as approaching
                    }
                    
                    // Blend radial (push away) and tangential (orbit) forces
                    float radialWeight;
                    float tangentialWeight;
                    
                    if (distance < effectiveAvoidanceRadius)
                    {
                        // Inside avoidance radius - prioritize radial (push away strongly)
                        radialWeight = 0.9f; // 90% radial, 10% tangential
                        tangentialWeight = 0.1f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.1f)
                    {
                        // Very close to avoidance radius - strong radial
                        radialWeight = 0.7f; // 70% radial, 30% tangential
                        tangentialWeight = 0.3f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.3f)
                    {
                        // Approaching avoidance radius - balanced
                        radialWeight = 0.5f; // 50% radial, 50% tangential
                        tangentialWeight = 0.5f;
                    }
                    else
                    {
                        // At safe distance, more orbital motion (gentle steering)
                        radialWeight = 0.2f; // 20% radial, 80% tangential
                        tangentialWeight = 0.8f;
                    }
                    
                    // Combine radial and tangential forces for orbital motion
                    Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                    orbitalDirection.Normalize();
                    
                    avoidanceVector += orbitalDirection * avoidanceStrength * currentAvoidanceForce;
                }
            }
            
            // Use A* pathfinding for obstacle avoidance
            if (_pathfindingManager != null && friendlyShip.IsActivelyMoving() && _friendlyShips != null && _playerShip != null)
            {
                Vector2 currentTarget = friendlyShip.TargetPosition;
                Vector2 currentPos = friendlyShip.Position;
                float distanceToTarget = Vector2.Distance(currentPos, currentTarget);
                
                // Get ship state for this friendly ship
                var shipStateForPath = _getOrCreateShipState(friendlyShip);
                
                // Check if target has changed - if so, reset progress tracking
                if (shipStateForPath.LastTarget != Vector2.Zero)
                {
                    Vector2 lastTarget = shipStateForPath.LastTarget;
                    if (Vector2.Distance(lastTarget, currentTarget) > 50f) // Target changed significantly
                    {
                        // Target changed - reset progress tracking
                        shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                        shipStateForPath.NoProgressTimer = 0f;
                    }
                }
                shipStateForPath.LastTarget = currentTarget;
                
                // Track progress toward destination to detect if ship is trapped
                if (shipStateForPath.ClosestDistanceToTarget == float.MaxValue)
                {
                    shipStateForPath.ClosestDistanceToTarget = distanceToTarget;
                    shipStateForPath.NoProgressTimer = 0f;
                }
                
                // Check if ship is making progress (getting closer to target)
                float closestDistance = shipStateForPath.ClosestDistanceToTarget;
                bool isMakingProgress = distanceToTarget < closestDistance - 10f; // Must get at least 10 pixels closer
                
                if (isMakingProgress)
                {
                    // Ship is making progress - update closest distance and reset timer
                    shipStateForPath.ClosestDistanceToTarget = distanceToTarget;
                    shipStateForPath.NoProgressTimer = 0f;
                }
                else
                {
                    // Ship is not making progress - increment timer
                    shipStateForPath.NoProgressTimer += deltaTime;
                }
                
                // If ship hasn't made progress for 3 seconds, it's likely trapped - force new path
                bool isTrapped = shipStateForPath.NoProgressTimer > 3.0f;
                
                // Check if we need to recalculate path
                bool needsNewPath = false;
                if (isTrapped)
                {
                    needsNewPath = true;
                    shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                    shipStateForPath.NoProgressTimer = 0f;
                }
                if (shipStateForPath.AStarPath.Count == 0)
                {
                    needsNewPath = true;
                }
                else
                {
                    // Check if we've reached the current waypoint
                    int currentWaypointIndex = shipStateForPath.CurrentWaypointIndex;
                    
                    if (currentWaypointIndex < shipStateForPath.AStarPath.Count)
                    {
                        Vector2 currentWaypoint = shipStateForPath.AStarPath[currentWaypointIndex];
                        float distToWaypoint = Vector2.Distance(currentPos, currentWaypoint);
                        
                        if (distToWaypoint < 100f) // Reached waypoint
                        {
                            currentWaypointIndex++;
                            shipStateForPath.CurrentWaypointIndex = currentWaypointIndex;
                            
                            // If we've reached all waypoints, check if we need a new path to final destination
                            if (currentWaypointIndex >= shipStateForPath.AStarPath.Count)
                            {
                                float distToFinal = Vector2.Distance(currentPos, currentTarget);
                                if (distToFinal > 100f)
                                {
                                    needsNewPath = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        needsNewPath = true;
                    }
                }
                
                // Update pathfinding grid with current obstacles
                float obstacleRadius = friendlyShip.AvoidanceDetectionRange * 0.5f;
                _pathfindingManager.UpdateObstacles(_friendlyShips, _playerShip, obstacleRadius);
                
                // Calculate A* path if needed
                if (needsNewPath)
                {
                    var path = _pathfindingManager.FindPath(currentPos, currentTarget);
                    shipStateForPath.AStarPath.Clear();
                    shipStateForPath.AStarPath.AddRange(path);
                    shipStateForPath.CurrentWaypointIndex = 0;
                    
                    // Reset progress tracking when calculating new path
                    shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                    shipStateForPath.NoProgressTimer = 0f;
                }
                
                // Follow A* path waypoints with look-ahead for smoother turning
                if (shipStateForPath.AStarPath.Count > 0)
                {
                    int waypointIndex = shipStateForPath.CurrentWaypointIndex;
                    var path = shipStateForPath.AStarPath;
                    
                    if (waypointIndex < path.Count)
                    {
                        Vector2 currentWaypoint = path[waypointIndex];
                        float distToWaypoint = Vector2.Distance(currentPos, currentWaypoint);
                        
                        // Look ahead in the path to find a future waypoint to turn toward
                        Vector2 pathLookAheadTarget = currentWaypoint;
                        float lookAheadDistance = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                        
                        // Find the furthest waypoint we can see ahead
                        float accumulatedDist = distToWaypoint;
                        for (int i = waypointIndex + 1; i < path.Count; i++)
                        {
                            float segmentDist = Vector2.Distance(path[i - 1], path[i]);
                            if (accumulatedDist + segmentDist <= lookAheadDistance)
                            {
                                accumulatedDist += segmentDist;
                                pathLookAheadTarget = path[i];
                            }
                            else
                            {
                                // Interpolate to a point along this segment
                                float remainingDist = lookAheadDistance - accumulatedDist;
                                if (remainingDist > 0 && segmentDist > 0)
                                {
                                    Vector2 segmentDir = path[i] - path[i - 1];
                                    segmentDir.Normalize();
                                    pathLookAheadTarget = path[i - 1] + segmentDir * remainingDist;
                                }
                                break;
                            }
                        }
                        
                        // Ensure look-ahead target avoids player's radius
                        if (IsTooCloseToPlayer(pathLookAheadTarget, friendlyShip))
                        {
                            pathLookAheadTarget = AvoidPlayerPosition(pathLookAheadTarget, friendlyShip, mapSize);
                        }
                        
                        // Clamp look-ahead target to map bounds
                        const float lookAheadMargin = 200f;
                        pathLookAheadTarget = new Vector2(
                            MathHelper.Clamp(pathLookAheadTarget.X, lookAheadMargin, mapSize - lookAheadMargin),
                            MathHelper.Clamp(pathLookAheadTarget.Y, lookAheadMargin, mapSize - lookAheadMargin)
                        );
                        
                        // Store look-ahead target for debug line drawing
                        shipStateForPath.LookAheadTarget = pathLookAheadTarget;
                        
                        // Set target to look-ahead position for smoother turning
                        friendlyShip.SetTargetPosition(pathLookAheadTarget);
                    }
                    else
                    {
                        // Reached end of path, go to final destination (clamped to map bounds)
                        const float finalTargetMargin = 200f;
                        Vector2 clampedTarget = new Vector2(
                            MathHelper.Clamp(currentTarget.X, finalTargetMargin, mapSize - finalTargetMargin),
                            MathHelper.Clamp(currentTarget.Y, finalTargetMargin, mapSize - finalTargetMargin)
                        );
                        
                        // Calculate and store look-ahead target for debug line drawing
                        Vector2 direction = clampedTarget - currentPos;
                        if (direction.LengthSquared() > 0.1f)
                        {
                            direction.Normalize();
                            float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                            Vector2 endPathLookAheadTarget = currentPos + direction * lookAheadDist;
                            shipStateForPath.LookAheadTarget = endPathLookAheadTarget;
                        }
                        
                        friendlyShip.SetTargetPosition(clampedTarget);
                    }
                }
            }
            else if (avoidanceVector.LengthSquared() > 0.1f)
            {
                // Fallback: use simple avoidance if pathfinding grid not available
                avoidanceVector.Normalize();
                float lookAheadDistance = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance * 0.8f;
                Vector2 newAvoidanceTarget = friendlyShip.Position + avoidanceVector * lookAheadDistance;
                
                const float safeTargetMargin = 150f;
                newAvoidanceTarget = new Vector2(
                    MathHelper.Clamp(newAvoidanceTarget.X, safeTargetMargin, mapSize - safeTargetMargin),
                    MathHelper.Clamp(newAvoidanceTarget.Y, safeTargetMargin, mapSize - safeTargetMargin)
                );
                
                // Store look-ahead target for debug line drawing
                var shipStateForAvoidance = _getOrCreateShipState(friendlyShip);
                shipStateForAvoidance.LookAheadTarget = newAvoidanceTarget;
                
                friendlyShip.SetTargetPosition(newAvoidanceTarget);
            }
            
            // Handle stuck detection and unstuck logic
            HandleFriendlyShipStuckDetection(friendlyShip, deltaTime, mapSize);
        }

        private void HandleFriendlyShipStuckDetection(FriendlyShip friendlyShip, float deltaTime, float mapSize)
        {
            if (_getOrCreateShipState == null) return;

            // Keep ship within map bounds - clamp position to prevent leaving map
            const float shipMargin = 30f;
            float clampedX = MathHelper.Clamp(friendlyShip.Position.X, shipMargin, mapSize - shipMargin);
            float clampedY = MathHelper.Clamp(friendlyShip.Position.Y, shipMargin, mapSize - shipMargin);
            
            // Track ship position to detect if it's stuck
            Vector2 clampedPosition = new Vector2(clampedX, clampedY);
            bool isStuck = false;
            var shipState = _getOrCreateShipState(friendlyShip);
            
            // Check if ship is stuck (not moving much)
            if (shipState.LastPosition != Vector2.Zero)
            {
                Vector2 lastPos = shipState.LastPosition;
                float distanceMoved = Vector2.Distance(clampedPosition, lastPos);
                
                // If ship hasn't moved much (less than 5 pixels) in the last frame, it might be stuck
                if (distanceMoved < 5f)
                {
                    shipState.StuckTimer += deltaTime;
                    
                    // If stuck for more than 0.5 seconds, give it a new target
                    if (shipState.StuckTimer > 0.5f)
                    {
                        isStuck = true;
                    }
                }
                else
                {
                    // Ship is moving, reset stuck timer
                    shipState.StuckTimer = 0f;
                }
            }
            
            // Also check if ship is stuck due to other ships blocking its path (even if actively moving)
            // This is especially important for long distance behavior where ships can get stuck on each other
            bool isBlockedByOtherShips = false;
            if (friendlyShip.IsActivelyMoving() && _friendlyShips != null)
            {
                Vector2 currentTarget = friendlyShip.TargetPosition;
                Vector2 toTarget = currentTarget - friendlyShip.Position;
                float distanceToTarget = toTarget.Length();
                
                // Check if ship is very close to other ships and not making progress toward target
                foreach (var otherShip in _friendlyShips)
                {
                    if (otherShip == friendlyShip) continue;
                    
                    Vector2 toOtherShip = otherShip.Position - friendlyShip.Position;
                    float distanceToOther = toOtherShip.Length();
                    float minSafeDistance = Math.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                    
                    // If very close to another ship (within avoidance radius) and target is in similar direction
                    if (distanceToOther < minSafeDistance * 1.2f && distanceToTarget > 100f)
                    {
                        // Check if the other ship is blocking the path to target
                        Vector2 toTargetNormalized = toTarget;
                        toTargetNormalized.Normalize();
                        Vector2 toOtherNormalized = toOtherShip;
                        toOtherNormalized.Normalize();
                        
                        float dot = Vector2.Dot(toTargetNormalized, toOtherNormalized);
                        // If other ship is in front (blocking path) and very close
                        if (dot > 0.7f && distanceToOther < minSafeDistance)
                        {
                            // Check if we're making progress - if not, we're stuck
                            if (shipState.LastPosition != Vector2.Zero)
                            {
                                float progressTowardTarget = Vector2.Distance(shipState.LastPosition, currentTarget) - distanceToTarget;
                                // If we're not getting closer to target (or moving very slowly toward it)
                                if (progressTowardTarget < 10f)
                                {
                                    shipState.StuckTimer += deltaTime;
                                    if (shipState.StuckTimer > 1.0f) // Give it 1 second to try to get unstuck
                                    {
                                        isBlockedByOtherShips = true;
                                        isStuck = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            shipState.LastPosition = clampedPosition;
            
            // If ship was clamped or is stuck, give it a new target
            bool wasClamped = (friendlyShip.Position.X != clampedX || friendlyShip.Position.Y != clampedY);
            if ((wasClamped || isStuck) && (!friendlyShip.IsActivelyMoving() || isBlockedByOtherShips))
            {
                // Get current behavior to determine unstuck strategy
                var shipStateForBehavior = _getOrCreateShipState(friendlyShip);
                FriendlyShipBehavior currentBehavior = shipStateForBehavior.Behavior;
                
                Vector2 newTarget;
                float safeMargin = 300f;
                
                // If stuck due to other ships blocking during long distance behavior, find a direction away from blocking ships
                if (isBlockedByOtherShips && currentBehavior == FriendlyShipBehavior.LongDistance && _friendlyShips != null)
                {
                    // Find direction away from nearest blocking ship
                    Vector2 awayFromBlocking = Vector2.Zero;
                    float nearestBlockingDistance = float.MaxValue;
                    
                    foreach (var otherShip in _friendlyShips)
                    {
                        if (otherShip == friendlyShip) continue;
                        
                        Vector2 toOtherShip = friendlyShip.Position - otherShip.Position;
                        float distanceToOther = toOtherShip.Length();
                        float minSafeDistance = Math.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                        
                        // If very close to this ship
                        if (distanceToOther < minSafeDistance * 1.5f && distanceToOther < nearestBlockingDistance)
                        {
                            nearestBlockingDistance = distanceToOther;
                            if (distanceToOther > 0.1f)
                            {
                                toOtherShip.Normalize();
                                awayFromBlocking = toOtherShip; // Direction away from blocking ship
                            }
                        }
                    }
                    
                    // If we found a blocking ship, move away from it
                    if (awayFromBlocking.LengthSquared() > 0.1f)
                    {
                        // Add perpendicular component to avoid getting stuck in same pattern
                        Vector2 perpendicular = new Vector2(-awayFromBlocking.Y, awayFromBlocking.X);
                        if (_random.NextDouble() < 0.5f) perpendicular = -perpendicular;
                        
                        // Blend away direction with perpendicular for better avoidance
                        Vector2 escapeDirection = (awayFromBlocking * 0.7f + perpendicular * 0.3f);
                        escapeDirection.Normalize();
                        
                        // Set target far enough away to clear the blocking ships
                        float escapeDistance = (float)(_random.NextDouble() * 1000f + 1500f); // 1500-2500 pixels
                        newTarget = clampedPosition + escapeDirection * escapeDistance;
                    }
                    else
                    {
                        // Fallback: random direction
                        float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                        Vector2 direction = new Vector2(
                            (float)Math.Cos(randomAngle),
                            (float)Math.Sin(randomAngle)
                        );
                        float targetDistance = (float)(_random.NextDouble() * 1000f + 1500f);
                        newTarget = clampedPosition + direction * targetDistance;
                    }
                }
                else
                {
                    // Ship is stuck near edge - give it a new target well away from edge
                    Vector2 awayFromEdge = Vector2.Zero;
                    
                    // Determine direction away from nearest edge
                    float distToLeft = clampedX;
                    float distToRight = mapSize - clampedX;
                    float distToTop = clampedY;
                    float distToBottom = mapSize - clampedY;
                    
                    float minDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));
                    
                    if (minDist == distToLeft)
                        awayFromEdge = new Vector2(1f, 0f); // Move right
                    else if (minDist == distToRight)
                        awayFromEdge = new Vector2(-1f, 0f); // Move left
                    else if (minDist == distToTop)
                        awayFromEdge = new Vector2(0f, 1f); // Move down
                    else
                        awayFromEdge = new Vector2(0f, -1f); // Move up
                    
                    // Add some randomness to the direction
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.PiOver2 - MathHelper.PiOver4);
                    float cos = (float)Math.Cos(randomAngle);
                    float sin = (float)Math.Sin(randomAngle);
                    Vector2 rotatedDir = new Vector2(
                        awayFromEdge.X * cos - awayFromEdge.Y * sin,
                        awayFromEdge.X * sin + awayFromEdge.Y * cos
                    );
                    
                    // Set target 1000-1500 pixels away from current position
                    float targetDistance = (float)(_random.NextDouble() * 500f + 1000f);
                    newTarget = clampedPosition + rotatedDir * targetDistance;
                }
                
                // Clamp to map bounds
                float newTargetX = MathHelper.Clamp(newTarget.X, safeMargin, mapSize - safeMargin);
                float newTargetY = MathHelper.Clamp(newTarget.Y, safeMargin, mapSize - safeMargin);
                newTarget = new Vector2(newTargetX, newTargetY);
                
                // Avoid other ships' radius when setting unstuck target (try multiple times if needed)
                int avoidAttempts = 0;
                while (IsTooCloseToOtherShips(newTarget, friendlyShip) && avoidAttempts < 5)
                {
                    newTarget = AvoidOtherShipsPosition(newTarget, friendlyShip, mapSize);
                    newTargetX = MathHelper.Clamp(newTarget.X, safeMargin, mapSize - safeMargin);
                    newTargetY = MathHelper.Clamp(newTarget.Y, safeMargin, mapSize - safeMargin);
                    newTarget = new Vector2(newTargetX, newTargetY);
                    avoidAttempts++;
                }
                
                friendlyShip.SetTargetPosition(newTarget);
                
                // Reset stuck timer and progress tracking
                var shipStateForStuck = _getOrCreateShipState(friendlyShip);
                shipStateForStuck.StuckTimer = 0f;
                shipStateForStuck.ClosestDistanceToTarget = float.MaxValue;
                shipStateForStuck.NoProgressTimer = 0f;
            }
        }

        private void HandleEnemyShipCollisionAvoidance(EnemyShip enemyShip, float deltaTime, float mapSize)
        {
            if (_friendlyShips == null || _enemyShips == null) return;

            // Collision avoidance: use same orbital motion system as friendly ships
            float avoidanceRadius = enemyShip.AvoidanceDetectionRange;
            float avoidanceForce = 300f;
            Vector2 avoidanceVector = Vector2.Zero;
            
            // Calculate look-ahead target position
            Vector2 lookAheadTarget = enemyShip.Position;
            if (enemyShip.IsActivelyMoving())
            {
            float shipRotation = enemyShip.Rotation;
                Vector2 lookAheadDirection = new Vector2(
                    (float)Math.Sin(shipRotation),
                    -(float)Math.Cos(shipRotation)
                );
                float lookAheadDist = enemyShip.MoveSpeed * enemyShip.LookAheadDistance;
                lookAheadTarget = enemyShip.Position + lookAheadDirection * lookAheadDist;
            }
            
            // Avoid friendly ships with orbital motion
            foreach (var friendlyShip in _friendlyShips)
            {
                Vector2 toFriendly = friendlyShip.Position - enemyShip.Position;
                float distance = toFriendly.Length();
                float otherAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
                float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherAvoidanceRadius);
                float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                
                // Check if look-ahead target is within other ship's avoidance radius
                Vector2 toLookAheadFromFriendly = lookAheadTarget - friendlyShip.Position;
                float lookAheadDistanceFromFriendly = toLookAheadFromFriendly.Length();
                bool lookAheadInRadius = lookAheadDistanceFromFriendly < effectiveAvoidanceRadius;
                
                if ((distance < avoidanceDetectionRange || lookAheadInRadius) && distance > 0.1f)
                {
                    toFriendly.Normalize();
                    Vector2 radialDirection = -toFriendly;
                    
                    // Calculate tangential direction for orbital motion
                    Vector2 tangentialDirection;
                    if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                    {
                        Vector2 shipVelocity = enemyShip.Velocity;
                        shipVelocity.Normalize();
                        Vector2 tan1 = new Vector2(-toFriendly.Y, toFriendly.X);
                        Vector2 tan2 = new Vector2(toFriendly.Y, -toFriendly.X);
                        float dot1 = Vector2.Dot(shipVelocity, tan1);
                        float dot2 = Vector2.Dot(shipVelocity, tan2);
                        tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                    }
                    else
                    {
                        tangentialDirection = new Vector2(-toFriendly.Y, toFriendly.X);
                    }
                    tangentialDirection.Normalize();
                    
                    // Calculate avoidance strength
                    float avoidanceStrength;
                    if (lookAheadInRadius)
                    {
                        float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromFriendly) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.5f + lookAheadPenetration * 2f;
                    }
                    else if (distance < effectiveAvoidanceRadius)
                    {
                        float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.0f + penetration * 1.5f;
                    }
                    else
                    {
                        avoidanceStrength = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                    }
                    
                    // Blend radial and tangential forces for orbital motion
                    float radialWeight, tangentialWeight;
                    if (distance < effectiveAvoidanceRadius)
                    {
                        radialWeight = 0.9f;
                        tangentialWeight = 0.1f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.1f)
                    {
                        radialWeight = 0.7f;
                        tangentialWeight = 0.3f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.3f)
                    {
                        radialWeight = 0.5f;
                        tangentialWeight = 0.5f;
                    }
                    else
                    {
                        radialWeight = 0.2f;
                        tangentialWeight = 0.8f;
                    }
                    
                    Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                    orbitalDirection.Normalize();
                    avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce;
                }
            }
            
            // Avoid player ship with orbital motion (but still pursue for attack)
            if (_playerShip != null)
            {
                Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
                float distance = toPlayer.Length();
                float playerAvoidanceRadius = _playerShip.AvoidanceDetectionRange;
                float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, playerAvoidanceRadius);
                float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                
                // Check if look-ahead target is within player's avoidance radius
                Vector2 toLookAheadFromPlayer = lookAheadTarget - _playerShip.Position;
                float lookAheadDistanceFromPlayer = toLookAheadFromPlayer.Length();
                bool lookAheadInPlayerRadius = lookAheadDistanceFromPlayer < effectiveAvoidanceRadius;
                
                // Avoid when close or when look-ahead hits player's radius
                if ((distance < avoidanceDetectionRange || lookAheadInPlayerRadius) && distance > 0.1f)
                {
                    toPlayer.Normalize();
                    Vector2 radialDirection = -toPlayer;
                    
                    // Calculate tangential direction
                    Vector2 tangentialDirection;
                    if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                    {
                        Vector2 shipVelocity = enemyShip.Velocity;
                        shipVelocity.Normalize();
                        Vector2 tan1 = new Vector2(-toPlayer.Y, toPlayer.X);
                        Vector2 tan2 = new Vector2(toPlayer.Y, -toPlayer.X);
                        float dot1 = Vector2.Dot(shipVelocity, tan1);
                        float dot2 = Vector2.Dot(shipVelocity, tan2);
                        tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                    }
                    else
                    {
                        tangentialDirection = new Vector2(-toPlayer.Y, toPlayer.X);
                    }
                    tangentialDirection.Normalize();
                    
                    // Calculate avoidance strength - stronger when very close to prevent getting stuck
                    float avoidanceStrength;
                    if (distance < effectiveAvoidanceRadius)
                    {
                        float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.5f + penetration * 2f;
                    }
                    else if (lookAheadInPlayerRadius)
                    {
                        float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromPlayer) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.2f + lookAheadPenetration * 1.5f;
                    }
                    else
                    {
                        float approachFactor = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                        avoidanceStrength = 0.8f + approachFactor * 0.7f;
                    }
                    
                    // Blend radial and tangential - more radial when very close
                    float radialWeight, tangentialWeight;
                    if (distance < effectiveAvoidanceRadius)
                    {
                        radialWeight = 0.8f;
                        tangentialWeight = 0.2f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.1f)
                    {
                        radialWeight = 0.6f;
                        tangentialWeight = 0.4f;
                    }
                    else
                    {
                        radialWeight = 0.3f;
                        tangentialWeight = 0.7f;
                    }
                    
                    Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                    orbitalDirection.Normalize();
                    avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce;
                }
            }
            
            // Avoid other enemy ships with orbital motion
            foreach (var otherEnemyShip in _enemyShips)
            {
                if (otherEnemyShip == enemyShip) continue;
                
                Vector2 toOtherEnemy = otherEnemyShip.Position - enemyShip.Position;
                float distance = toOtherEnemy.Length();
                float otherAvoidanceRadius = otherEnemyShip.AvoidanceDetectionRange;
                float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherAvoidanceRadius);
                float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                
                // Check if look-ahead target is within other enemy's avoidance radius
                Vector2 toLookAheadFromOtherEnemy = lookAheadTarget - otherEnemyShip.Position;
                float lookAheadDistanceFromOtherEnemy = toLookAheadFromOtherEnemy.Length();
                bool lookAheadInOtherEnemyRadius = lookAheadDistanceFromOtherEnemy < effectiveAvoidanceRadius;
                
                if ((distance < avoidanceDetectionRange || lookAheadInOtherEnemyRadius) && distance > 0.1f)
                {
                    toOtherEnemy.Normalize();
                    Vector2 radialDirection = -toOtherEnemy;
                    
                    // Calculate tangential direction
                    Vector2 tangentialDirection;
                    if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                    {
                        Vector2 shipVelocity = enemyShip.Velocity;
                        shipVelocity.Normalize();
                        Vector2 tan1 = new Vector2(-toOtherEnemy.Y, toOtherEnemy.X);
                        Vector2 tan2 = new Vector2(toOtherEnemy.Y, -toOtherEnemy.X);
                        float dot1 = Vector2.Dot(shipVelocity, tan1);
                        float dot2 = Vector2.Dot(shipVelocity, tan2);
                        tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                    }
                    else
                    {
                        tangentialDirection = new Vector2(-toOtherEnemy.Y, toOtherEnemy.X);
                    }
                    tangentialDirection.Normalize();
                    
                    // Calculate avoidance strength
                    float avoidanceStrength;
                    if (lookAheadInOtherEnemyRadius)
                    {
                        float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromOtherEnemy) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.5f + lookAheadPenetration * 2f;
                    }
                    else if (distance < effectiveAvoidanceRadius)
                    {
                        float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                        avoidanceStrength = 1.0f + penetration * 1.5f;
                    }
                    else
                    {
                        avoidanceStrength = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                    }
                    
                    // Blend radial and tangential forces
                    float radialWeight, tangentialWeight;
                    if (distance < effectiveAvoidanceRadius)
                    {
                        radialWeight = 0.9f;
                        tangentialWeight = 0.1f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.1f)
                    {
                        radialWeight = 0.7f;
                        tangentialWeight = 0.3f;
                    }
                    else if (distance < effectiveAvoidanceRadius * 1.3f)
                    {
                        radialWeight = 0.5f;
                        tangentialWeight = 0.5f;
                    }
                    else
                    {
                        radialWeight = 0.2f;
                        tangentialWeight = 0.8f;
                    }
                    
                    Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                    orbitalDirection.Normalize();
                    avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce;
                }
            }
            
            // Apply avoidance vector if significant
            if (avoidanceVector.LengthSquared() > 100f)
            {
                Vector2 currentTarget = enemyShip.TargetPosition;
                Vector2 avoidanceTarget = enemyShip.Position + avoidanceVector * deltaTime;
                // Blend avoidance with current target (don't completely override pursuit)
                Vector2 blendedTarget = Vector2.Lerp(currentTarget, avoidanceTarget, 0.3f);
                enemyShip.SetTargetPosition(blendedTarget);
            }
        }

        private void UpdateShipTracking(FriendlyShip friendlyShip)
        {
            if (_getOrCreateShipState == null) return;

            // Update last direction for smooth pathing based on velocity
            if (friendlyShip.IsActivelyMoving())
            {
                // Use velocity direction if available, otherwise calculate from position change
                Vector2 velDir = Vector2.Zero;
                var shipStateForDir = _getOrCreateShipState(friendlyShip);
                if (shipStateForDir.LastPosition != Vector2.Zero)
                {
                    Vector2 posChange = friendlyShip.Position - shipStateForDir.LastPosition;
                    if (posChange.LengthSquared() > 1f)
                    {
                        posChange.Normalize();
                        velDir = posChange;
                    }
                }
                
                // Only update if we have a valid direction
                if (velDir.LengthSquared() > 0.1f)
                {
                    var shipStateForDirFinal = _getOrCreateShipState(friendlyShip);
                    shipStateForDirFinal.LastDirection = velDir;
                }
            }
        }

    }
}

