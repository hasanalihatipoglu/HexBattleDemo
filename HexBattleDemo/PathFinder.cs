using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HexBattleDemo;

/// <summary>
/// Handles pathfinding and movement range calculations for hexagonal grids
/// </summary>
public class PathFinder
{
    private int gridWidth;
    private int gridHeight;

    public PathFinder(int gridWidth, int gridHeight)
    {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
    }

    /// <summary>
    /// Find all hexes within movement range from a starting position
    /// </summary>
    public List<Point> FindMovementRange(Point start, int movementRange, HashSet<Point> blockedPositions = null)
    {
        if (blockedPositions == null)
            blockedPositions = new HashSet<Point>();

        List<Point> reachableHexes = new List<Point>();
        Queue<HexNode> frontier = new Queue<HexNode>();
        Dictionary<Point, int> visited = new Dictionary<Point, int>();

        // Start position
        frontier.Enqueue(new HexNode(start, 0));
        visited[start] = 0;

        while (frontier.Count > 0)
        {
            HexNode current = frontier.Dequeue();

            // Add to reachable if not the starting position
            if (current.Distance > 0)
            {
                reachableHexes.Add(current.Position);
            }

            // Stop expanding if we've reached max range
            if (current.Distance >= movementRange)
                continue;

            // Check all neighbors
            List<Point> neighbors = GetNeighbors(current.Position);
            foreach (Point neighbor in neighbors)
            {
                // Skip if already visited with shorter or equal distance
                if (visited.ContainsKey(neighbor) && visited[neighbor] <= current.Distance + 1)
                    continue;

                // Skip if blocked by another unit
                if (blockedPositions.Contains(neighbor))
                    continue;

                // Add to frontier
                int newDistance = current.Distance + 1;
                frontier.Enqueue(new HexNode(neighbor, newDistance));
                visited[neighbor] = newDistance;
            }
        }

        return reachableHexes;
    }

    /// <summary>
    /// Get all valid neighbors of a hex (6 directions for pointy-top in rectangular layout)
    /// </summary>
    public List<Point> GetNeighbors(Point hex)
    {
        List<Point> neighbors = new List<Point>();
        int q = hex.X;
        int r = hex.Y;

        // For rectangular offset coordinates (pointy-top, odd rows offset right)
        // The 6 neighbors depend on whether we're on an even or odd row

        if (r % 2 == 0) // Even row
        {
            // NW, NE, E, SE, SW, W
            AddIfValid(neighbors, q - 1, r - 1); // NW
            AddIfValid(neighbors, q, r - 1);     // NE
            AddIfValid(neighbors, q + 1, r);     // E
            AddIfValid(neighbors, q, r + 1);     // SE
            AddIfValid(neighbors, q - 1, r + 1); // SW
            AddIfValid(neighbors, q - 1, r);     // W
        }
        else // Odd row (offset right)
        {
            // NW, NE, E, SE, SW, W
            AddIfValid(neighbors, q, r - 1);     // NW
            AddIfValid(neighbors, q + 1, r - 1); // NE
            AddIfValid(neighbors, q + 1, r);     // E
            AddIfValid(neighbors, q + 1, r + 1); // SE
            AddIfValid(neighbors, q, r + 1);     // SW
            AddIfValid(neighbors, q - 1, r);     // W
        }

        return neighbors;
    }

    /// <summary>
    /// Add point to list if it's within grid bounds
    /// </summary>
    private void AddIfValid(List<Point> list, int q, int r)
    {
        if (q >= 0 && q < gridWidth && r >= 0 && r < gridHeight)
        {
            list.Add(new Point(q, r));
        }
    }

    /// <summary>
    /// Calculate distance between two hexes
    /// </summary>
    public int GetDistance(Point from, Point to)
    {
        // Use BFS to find shortest path distance
        Queue<HexNode> frontier = new Queue<HexNode>();
        HashSet<Point> visited = new HashSet<Point>();

        frontier.Enqueue(new HexNode(from, 0));
        visited.Add(from);

        while (frontier.Count > 0)
        {
            HexNode current = frontier.Dequeue();

            if (current.Position == to)
                return current.Distance;

            List<Point> neighbors = GetNeighbors(current.Position);
            foreach (Point neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    frontier.Enqueue(new HexNode(neighbor, current.Distance + 1));
                }
            }
        }

        return -1; // Not reachable
    }

    /// <summary>
    /// Find shortest path between two hexes
    /// </summary>
    public List<Point> FindPath(Point start, Point goal, HashSet<Point> blockedPositions = null)
    {
        if (blockedPositions == null)
            blockedPositions = new HashSet<Point>();

        Queue<HexNode> frontier = new Queue<HexNode>();
        Dictionary<Point, Point> cameFrom = new Dictionary<Point, Point>();
        HashSet<Point> visited = new HashSet<Point>();

        frontier.Enqueue(new HexNode(start, 0));
        visited.Add(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            HexNode current = frontier.Dequeue();

            if (current.Position == goal)
            {
                // Reconstruct path
                return ReconstructPath(cameFrom, start, goal);
            }

            List<Point> neighbors = GetNeighbors(current.Position);
            foreach (Point neighbor in neighbors)
            {
                if (visited.Contains(neighbor))
                    continue;

                if (blockedPositions.Contains(neighbor) && neighbor != goal)
                    continue;

                visited.Add(neighbor);
                cameFrom[neighbor] = current.Position;
                frontier.Enqueue(new HexNode(neighbor, current.Distance + 1));
            }
        }

        return new List<Point>(); // No path found
    }

    /// <summary>
    /// Reconstruct path from came-from dictionary
    /// </summary>
    private List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point start, Point goal)
    {
        List<Point> path = new List<Point>();
        Point current = goal;

        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Check if a hex is within range of another hex
    /// </summary>
    public bool IsInRange(Point from, Point to, int range)
    {
        int distance = GetDistance(from, to);
        return distance >= 0 && distance <= range;
    }
}

/// <summary>
/// Helper class for pathfinding nodes
/// </summary>
internal class HexNode
{
    public Point Position { get; set; }
    public int Distance { get; set; }

    public HexNode(Point position, int distance)
    {
        Position = position;
        Distance = distance;
    }
}