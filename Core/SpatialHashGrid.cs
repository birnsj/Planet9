using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Planet9.Entities;

namespace Planet9.Core
{
    /// <summary>
    /// Spatial hash grid for efficient collision detection
    /// Divides space into cells and only checks collisions between entities in nearby cells
    /// Reduces collision detection from O(n²) to O(n)
    /// </summary>
    public class SpatialHashGrid
    {
        private readonly Dictionary<int, List<Entity>> _cells;
        private readonly float _cellSize;
        private readonly float _mapSize;
        private readonly int _gridWidth;
        private readonly int _gridHeight;

        public SpatialHashGrid(float mapSize, float cellSize = 256f)
        {
            _mapSize = mapSize;
            _cellSize = cellSize;
            _gridWidth = (int)Math.Ceiling(mapSize / cellSize);
            _gridHeight = (int)Math.Ceiling(mapSize / cellSize);
            _cells = new Dictionary<int, List<Entity>>();
        }

        /// <summary>
        /// Get cell index from world position
        /// </summary>
        private int GetCellIndex(Vector2 position)
        {
            int x = (int)Math.Floor(position.X / _cellSize);
            int y = (int)Math.Floor(position.Y / _cellSize);
            
            // Clamp to grid bounds
            x = MathHelper.Clamp(x, 0, _gridWidth - 1);
            y = MathHelper.Clamp(y, 0, _gridHeight - 1);
            
            return y * _gridWidth + x;
        }

        /// <summary>
        /// Get cell indices for a position with radius (for checking nearby cells)
        /// </summary>
        private HashSet<int> GetCellIndices(Vector2 position, float radius)
        {
            var indices = new HashSet<int>();
            
            // Calculate bounding box
            float minX = position.X - radius;
            float maxX = position.X + radius;
            float minY = position.Y - radius;
            float maxY = position.Y + radius;
            
            int startX = (int)Math.Floor(minX / _cellSize);
            int endX = (int)Math.Floor(maxX / _cellSize);
            int startY = (int)Math.Floor(minY / _cellSize);
            int endY = (int)Math.Floor(maxY / _cellSize);
            
            // Clamp to grid bounds
            startX = MathHelper.Clamp(startX, 0, _gridWidth - 1);
            endX = MathHelper.Clamp(endX, 0, _gridWidth - 1);
            startY = MathHelper.Clamp(startY, 0, _gridHeight - 1);
            endY = MathHelper.Clamp(endY, 0, _gridHeight - 1);
            
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    indices.Add(y * _gridWidth + x);
                }
            }
            
            return indices;
        }

        /// <summary>
        /// Clear all entities from the grid
        /// </summary>
        public void Clear()
        {
            _cells.Clear();
        }

        /// <summary>
        /// Add an entity to the grid
        /// </summary>
        public void Add(Entity entity)
        {
            if (entity == null || !entity.IsActive) return;
            
            int cellIndex = GetCellIndex(entity.Position);
            
            if (!_cells.TryGetValue(cellIndex, out var cellList))
            {
                cellList = new List<Entity>();
                _cells[cellIndex] = cellList;
            }
            
            if (!cellList.Contains(entity))
            {
                cellList.Add(entity);
            }
        }

        /// <summary>
        /// Get all entities near a position within a radius
        /// </summary>
        public IEnumerable<Entity> GetNearby(Vector2 position, float radius)
        {
            var nearbyEntities = new HashSet<Entity>();
            var cellIndices = GetCellIndices(position, radius);
            
            foreach (int cellIndex in cellIndices)
            {
                if (_cells.TryGetValue(cellIndex, out var cellList))
                {
                    foreach (var entity in cellList)
                    {
                        if (entity.IsActive && !nearbyEntities.Contains(entity))
                        {
                            // Verify entity is actually within radius
                            float distance = Vector2.Distance(position, entity.Position);
                            if (distance <= radius)
                            {
                                nearbyEntities.Add(entity);
                            }
                        }
                    }
                }
            }
            
            return nearbyEntities;
        }

        /// <summary>
        /// Get all entities in the same cell as the given position
        /// </summary>
        public IEnumerable<Entity> GetInCell(Vector2 position)
        {
            int cellIndex = GetCellIndex(position);
            
            if (_cells.TryGetValue(cellIndex, out var cellList))
            {
                foreach (var entity in cellList)
                {
                    if (entity.IsActive)
                    {
                        yield return entity;
                    }
                }
            }
        }
    }
}

