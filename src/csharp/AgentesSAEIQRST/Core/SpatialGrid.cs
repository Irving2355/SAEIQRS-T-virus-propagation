using System;
using System.Collections.Generic;
using AgentesSAEIQRST.Core;
using AgentesSAEIQRST.Core.Compartments;

namespace AgentesSAEIQRST.Core
{
    public class SpatialGrid
    {
        private readonly double cellSize;
        private readonly Dictionary<(int, int), List<Agent>> grid = new();

        public SpatialGrid(double cellSize)
        {
            this.cellSize = cellSize;
        }

        private (int, int) GetCell(double x, double y)
        {
            return ((int)(x / cellSize), (int)(y / cellSize));
        }

        public void Clear()
        {
            grid.Clear();
        }

        public void Add(Agent agent)
        {
            var cell = GetCell(agent.X, agent.Y);
            if (!grid.ContainsKey(cell))
                grid[cell] = new List<Agent>();
            grid[cell].Add(agent);
        }

        public IEnumerable<Agent> GetNeighbors(Agent agent)
        {
            var (cx, cy) = GetCell(agent.X, agent.Y);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var neighborCell = (cx + dx, cy + dy);
                    if (grid.TryGetValue(neighborCell, out var vecinos))
                    {
                        foreach (var v in vecinos)
                        {
                            if (v != agent)
                                yield return v;
                        }
                    }
                }
            }
        }
    }
}
