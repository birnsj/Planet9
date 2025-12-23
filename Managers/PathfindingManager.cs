using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Planet9.Managers
{
    /// <summary>
    /// A* Pathfinding System
    /// Manages pathfinding grid and path calculation
    /// </summary>
    public class PathfindingManager
    {
        private PathfindingGrid? _pathfindingGrid;
        private const float MapSize = 8192f;
        private const float DefaultCellSize = 128f;
        
        public PathfindingGrid? Grid => _pathfindingGrid;
        
        /// <summary>
        /// Initialize the pathfinding grid
        /// </summary>
        public void Initialize(float mapSize = 8192f, float cellSize = 128f)
        {
            _pathfindingGrid = new PathfindingGrid(mapSize, cellSize);
        }
        
        /// <summary>
        /// Update obstacles in the pathfinding grid based on ship positions
        /// </summary>
        public void UpdateObstacles(
            List<Planet9.Entities.FriendlyShip> friendlyShips,
            Planet9.Entities.PlayerShip? playerShip,
            float obstacleRadius = 150f)
        {
            if (_pathfindingGrid == null) return;
            
            _pathfindingGrid.ClearObstacles();
            
            // Mark all friendly ships as obstacles
            foreach (var ship in friendlyShips)
            {
                _pathfindingGrid.SetObstacle(ship.Position, obstacleRadius, true);
            }
            
            // Mark player ship as obstacle
            if (playerShip != null)
            {
                _pathfindingGrid.SetObstacle(playerShip.Position, obstacleRadius, true);
            }
        }
        
        /// <summary>
        /// Find a path from start to end position
        /// </summary>
        public List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            if (_pathfindingGrid == null)
            {
                return new List<Vector2> { end };
            }
            
            return _pathfindingGrid.FindPath(start, end);
        }
    }
    
    /// <summary>
    /// Represents a single node in the pathfinding grid
    /// </summary>
    public class PathNode
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Walkable { get; set; } = true;
        public float GCost { get; set; } = 0f; // Distance from start
        public float HCost { get; set; } = 0f; // Heuristic distance to goal
        public float FCost => GCost + HCost; // Total cost
        public PathNode? Parent { get; set; }
        
        public PathNode(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public Vector2 ToWorldPosition(float cellSize)
        {
            return new Vector2(X * cellSize + cellSize / 2f, Y * cellSize + cellSize / 2f);
        }
    }
    
    /// <summary>
    /// Grid-based pathfinding system using A* algorithm
    /// </summary>
    public class PathfindingGrid
    {
        private PathNode[,] _grid;
        private int _gridWidth;
        private int _gridHeight;
        private float _cellSize;
        private float _mapSize;
        
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        
        public PathfindingGrid(float mapSize, float cellSize = 128f)
        {
            _mapSize = mapSize;
            _cellSize = cellSize;
            _gridWidth = (int)Math.Ceiling(mapSize / cellSize);
            _gridHeight = (int)Math.Ceiling(mapSize / cellSize);
            _grid = new PathNode[_gridWidth, _gridHeight];
            
            // Initialize all nodes as walkable, except edge cells
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    // Mark edge cells as unwalkable to keep ships within map bounds
                    Vector2 worldPos = new Vector2(x * cellSize + cellSize / 2f, y * cellSize + cellSize / 2f);
                    bool isEdge = worldPos.X < 100f || worldPos.X > mapSize - 100f || 
                                  worldPos.Y < 100f || worldPos.Y > mapSize - 100f;
                    _grid[x, y] = new PathNode(x, y) { Walkable = !isEdge };
                }
            }
        }
        
        public PathNode? GetNode(int x, int y)
        {
            if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight)
                return null;
            return _grid[x, y];
        }
        
        public PathNode? GetNodeAt(int x, int y)
        {
            return GetNode(x, y);
        }
        
        public PathNode? GetNodeFromWorld(Vector2 worldPos)
        {
            int x = (int)(worldPos.X / _cellSize);
            int y = (int)(worldPos.Y / _cellSize);
            return GetNode(x, y);
        }
        
        public void SetObstacle(Vector2 worldPos, float radius, bool isObstacle)
        {
            int centerX = (int)(worldPos.X / _cellSize);
            int centerY = (int)(worldPos.Y / _cellSize);
            int radiusInCells = (int)Math.Ceiling(radius / _cellSize) + 1;
            
            for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
            {
                for (int y = centerY - radiusInCells; y <= centerY + radiusInCells; y++)
                {
                    var node = GetNode(x, y);
                    if (node != null)
                    {
                        Vector2 nodeWorldPos = node.ToWorldPosition(_cellSize);
                        float dist = Vector2.Distance(worldPos, nodeWorldPos);
                        if (dist <= radius)
                        {
                            node.Walkable = !isObstacle;
                        }
                    }
                }
            }
        }
        
        public void ClearObstacles()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    // Preserve edge obstacles - only clear non-edge cells
                    Vector2 worldPos = new Vector2(x * _cellSize + _cellSize / 2f, y * _cellSize + _cellSize / 2f);
                    bool isEdge = worldPos.X < 100f || worldPos.X > _mapSize - 100f || 
                                  worldPos.Y < 100f || worldPos.Y > _mapSize - 100f;
                    if (!isEdge)
                    {
                        _grid[x, y].Walkable = true;
                    }
                }
            }
        }
        
        public List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            var path = new List<Vector2>();
            
            // Clamp start and end positions to map bounds
            start = new Vector2(MathHelper.Clamp(start.X, 100f, _mapSize - 100f), MathHelper.Clamp(start.Y, 100f, _mapSize - 100f));
            end = new Vector2(MathHelper.Clamp(end.X, 100f, _mapSize - 100f), MathHelper.Clamp(end.Y, 100f, _mapSize - 100f));
            
            PathNode? startNode = GetNodeFromWorld(start);
            PathNode? endNode = GetNodeFromWorld(end);
            
            if (startNode == null || endNode == null || !startNode.Walkable || !endNode.Walkable)
            {
                // If start or end is invalid, return clamped direct path
                path.Add(end);
                return path;
            }
            
            var openSet = new HashSet<PathNode>();
            var closedSet = new HashSet<PathNode>();
            
            // Reset all nodes
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _grid[x, y].GCost = 0f;
                    _grid[x, y].HCost = 0f;
                    _grid[x, y].Parent = null;
                }
            }
            
            openSet.Add(startNode);
            
            while (openSet.Count > 0)
            {
                // Find node with lowest F cost
                PathNode? currentNode = null;
                float lowestFCost = float.MaxValue;
                foreach (var node in openSet)
                {
                    if (currentNode == null || node.FCost < lowestFCost || (node.FCost == lowestFCost && node.HCost < currentNode.HCost))
                    {
                        lowestFCost = node.FCost;
                        currentNode = node;
                    }
                }
                
                if (currentNode == null) break;
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);
                
                // Check if we reached the goal
                if (currentNode == endNode)
                {
                    // Reconstruct path
                    var nodePath = new List<PathNode>();
                    PathNode? current = currentNode;
                    while (current != null)
                    {
                        nodePath.Add(current);
                        current = current.Parent;
                    }
                    nodePath.Reverse();
                    
                    // Convert to world positions and simplify path (less granular)
                    if (nodePath.Count > 0)
                    {
                        path.Add(start); // Always start with actual start position
                        
                        // Simplified path: only add waypoints when direction changes significantly
                        // Use a larger angle threshold to reduce granularity
                        const float minAngleChange = 0.3f; // ~17 degrees minimum change to add waypoint
                        const float minDistance = 256f; // Minimum distance between waypoints
                        
                        for (int i = 1; i < nodePath.Count - 1; i++)
                        {
                            if (i > 0 && i < nodePath.Count - 1)
                            {
                                Vector2 prev = nodePath[i - 1].ToWorldPosition(_cellSize);
                                Vector2 curr = nodePath[i].ToWorldPosition(_cellSize);
                                Vector2 next = nodePath[i + 1].ToWorldPosition(_cellSize);
                                
                                Vector2 dir1 = curr - prev;
                                Vector2 dir2 = next - curr;
                                
                                // Check distance from last waypoint
                                float distFromLast = path.Count > 0 ? Vector2.Distance(path[path.Count - 1], curr) : float.MaxValue;
                                
                                if (dir1.LengthSquared() > 0.1f && dir2.LengthSquared() > 0.1f)
                                {
                                    dir1.Normalize();
                                    dir2.Normalize();
                                    
                                    float dot = Vector2.Dot(dir1, dir2);
                                    float angleChange = (float)Math.Acos(MathHelper.Clamp(dot, -1f, 1f));
                                    
                                    // Add waypoint if angle changes significantly AND it's far enough from last waypoint
                                    if (angleChange > minAngleChange && distFromLast > minDistance)
                                    {
                                        path.Add(curr);
                                    }
                                }
                                else if (distFromLast > minDistance * 2f)
                                {
                                    // Add waypoint if it's very far from last one (even if direction doesn't change much)
                                    path.Add(curr);
                                }
                            }
                        }
                        
                        path.Add(end); // Always end with actual end position
                    }
                    else
                    {
                        path.Add(end);
                    }
                    
                    return path;
                }
                
                // Check neighbors (8-directional)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int neighborX = currentNode.X + dx;
                        int neighborY = currentNode.Y + dy;
                        var neighbor = GetNode(neighborX, neighborY);
                        
                        if (neighbor == null || !neighbor.Walkable || closedSet.Contains(neighbor))
                            continue;
                        
                        // Calculate movement cost (diagonal costs more)
                        float movementCost = (dx != 0 && dy != 0) ? 1.414f : 1f;
                        float newGCost = currentNode.GCost + movementCost;
                        
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                        else if (newGCost >= neighbor.GCost)
                        {
                            continue; // This path is not better
                        }
                        
                        // This path is better
                        neighbor.Parent = currentNode;
                        neighbor.GCost = newGCost;
                        neighbor.HCost = Heuristic(neighbor, endNode);
                    }
                }
            }
            
            // No path found, return direct path
            path.Add(end);
            return path;
        }
        
        private float Heuristic(PathNode a, PathNode b)
        {
            // Manhattan distance (can use Euclidean for more accurate)
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return dx + dy; // Manhattan
            // return (float)Math.Sqrt(dx * dx + dy * dy); // Euclidean (more accurate but slower)
        }
    }
}

