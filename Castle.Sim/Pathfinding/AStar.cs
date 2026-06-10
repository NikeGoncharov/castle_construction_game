using Castle.Sim.Geometry;
using Castle.Sim.World;

namespace Castle.Sim.Pathfinding;

/// <summary>
/// A* over the grid, following Millington's model (see game-ai/pathfinding.md).
/// </summary>
public static class AStar
{
    public static List<Cell>? Find(GridMap map, Cell start, System.Func<Cell, bool> isGoal, Cell heuristicTarget,
        HashSet<Cell>? avoid = null)
    {
        if (isGoal(start))
            return new List<Cell>();

        var open = new PriorityQueue<Cell, float>();
        var cameFrom = new Dictionary<Cell, Cell>();
        var gScore = new Dictionary<Cell, float> { [start] = 0f };
        var closed = new HashSet<Cell>();

        open.Enqueue(start, Heuristic(start, heuristicTarget));

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (isGoal(current))
                return Reconstruct(cameFrom, current);

            if (!closed.Add(current))
                continue;

            float g = gScore[current];
            foreach (var next in map.WalkableNeighbours(current))
            {
                if (closed.Contains(next))
                    continue;
                if (avoid != null && avoid.Contains(next) && !isGoal(next))
                    continue;

                float tentative = g + 1f;
                if (!gScore.TryGetValue(next, out float known) || tentative < known)
                {
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    open.Enqueue(next, tentative + Heuristic(next, heuristicTarget));
                }
            }
        }

        return null;
    }

    public static List<Cell>? FindTo(GridMap map, Cell start, Cell goal, HashSet<Cell>? avoid = null)
        => map.IsWalkable(goal) ? Find(map, start, c => c == goal, goal, avoid) : null;

    /// <summary>Path to the best adjacent cell around <paramref name="target"/>.</summary>
    public static List<Cell>? FindAdjacentTo(GridMap map, Cell start, Cell target, HashSet<Cell>? avoid = null)
    {
        var adjacent = new HashSet<Cell>(map.WalkableAdjacent(target));
        if (adjacent.Count == 0)
            return null;

        Cell? bestGoal = null;
        int bestDist = int.MaxValue;
        foreach (var adj in adjacent)
        {
            if (avoid != null && avoid.Contains(adj))
                continue;
            int d = start.ManhattanTo(adj);
            if (d < bestDist)
            {
                bestDist = d;
                bestGoal = adj;
            }
        }

        if (bestGoal == null)
        {
            // Fall back: allow standing on a cell another worker occupies if it's the only option.
            foreach (var adj in adjacent)
            {
                int d = start.ManhattanTo(adj);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestGoal = adj;
                }
            }
        }

        return bestGoal == null ? null : FindTo(map, start, bestGoal.Value, avoid);
    }

    private static float Heuristic(Cell a, Cell b) => a.ManhattanTo(b);

    private static List<Cell> Reconstruct(Dictionary<Cell, Cell> cameFrom, Cell goal)
    {
        var path = new List<Cell>();
        var c = goal;
        while (cameFrom.TryGetValue(c, out var prev))
        {
            path.Add(c);
            c = prev;
        }
        path.Reverse();
        return path;
    }
}
