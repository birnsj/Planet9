using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Planet9.Scenes;

namespace Planet9.Core
{
    /// <summary>
    /// Consolidates all state data for a friendly ship to reduce dictionary lookups and improve performance
    /// </summary>
    public class ShipState
    {
        // Behavior tracking
        public FriendlyShipBehavior Behavior { get; set; } = FriendlyShipBehavior.Idle;
        public float BehaviorTimer { get; set; } = 0f;

        // Pathfinding and movement
        public Vector2 LastAvoidanceTarget { get; set; } = Vector2.Zero;
        public Vector2 OriginalDestination { get; set; } = Vector2.Zero;
        public Vector2 LastPosition { get; set; } = Vector2.Zero;
        public Vector2 LastDirection { get; set; } = Vector2.Zero;
        public Vector2 LastTarget { get; set; } = Vector2.Zero;
        public Vector2 LookAheadTarget { get; set; } = Vector2.Zero;

        // Pathfinding data
        public List<Vector2> Path { get; set; } = new List<Vector2>();
        public List<Vector2> PatrolPoints { get; set; } = new List<Vector2>();
        public List<Vector2> AStarPath { get; set; } = new List<Vector2>();
        public int CurrentWaypointIndex { get; set; } = 0;

        // Stuck detection and progress tracking
        public float StuckTimer { get; set; } = 0f;
        public float ClosestDistanceToTarget { get; set; } = float.MaxValue;
        public float NoProgressTimer { get; set; } = 0f;

        /// <summary>
        /// Reset all state to default values
        /// </summary>
        public void Reset()
        {
            Behavior = FriendlyShipBehavior.Idle;
            BehaviorTimer = 0f;
            LastAvoidanceTarget = Vector2.Zero;
            OriginalDestination = Vector2.Zero;
            LastPosition = Vector2.Zero;
            LastDirection = Vector2.Zero;
            LastTarget = Vector2.Zero;
            LookAheadTarget = Vector2.Zero;
            Path.Clear();
            PatrolPoints.Clear();
            AStarPath.Clear();
            CurrentWaypointIndex = 0;
            StuckTimer = 0f;
            ClosestDistanceToTarget = float.MaxValue;
            NoProgressTimer = 0f;
        }
    }

    /// <summary>
    /// Consolidates all state data for an enemy ship
    /// </summary>
    public class EnemyShipState
    {
        // Behavior tracking
        public FriendlyShipBehavior Behavior { get; set; } = FriendlyShipBehavior.Idle;
        public float BehaviorTimer { get; set; } = 0f;
        public float AttackCooldown { get; set; } = 0f;

        // Pathfinding data
        public List<Vector2> PatrolPoints { get; set; } = new List<Vector2>();

        /// <summary>
        /// Reset all state to default values
        /// </summary>
        public void Reset()
        {
            Behavior = FriendlyShipBehavior.Idle;
            BehaviorTimer = 0f;
            AttackCooldown = 0f;
            PatrolPoints.Clear();
        }
    }
}

