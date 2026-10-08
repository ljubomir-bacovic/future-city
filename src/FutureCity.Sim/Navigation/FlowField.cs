using FutureCity.Sim.Map;

namespace FutureCity.Sim.Navigation;

/// <summary>
/// The cost of walking from every tile to one goal tile, for units that may pass one player's gates. A group marching to
/// the same place shares one field instead of each member planning its own route: every tile simply points to its
/// cheapest neighbour. Built with Dijkstra outward from the goal using the same step costs as <see cref="Pathfinder"/>;
/// ties go to the lower direction, so the result is deterministic. Derived from the map: never saved.
/// </summary>
public sealed class FlowField
{
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;
    private const int Unreachable = int.MaxValue;

    private readonly TileMap _map;
    private readonly Pathfinder _pathfinder;
    private readonly int[] _cost;

    private FlowField(TileMap map, Pathfinder pathfinder, int goalX, int goalY, int gates)
    {
        _map = map;
        _pathfinder = pathfinder;
        GoalX = goalX;
        GoalY = goalY;
        Gates = gates;
        Version = map.Version;
        _cost = new int[map.Width * map.Height];
        Array.Fill(_cost, Unreachable);
    }

    /// <summary>Goal column.</summary>
    public int GoalX { get; }
    /// <summary>Goal row.</summary>
    public int GoalY { get; }
    /// <summary>Player whose gates the field lets through.</summary>
    public int Gates { get; }
    /// <summary>The map version the field was built for.</summary>
    public int Version { get; }

    /// <summary>Builds the field toward (goalX, goalY).</summary>
    public static FlowField Build(TileMap map, Pathfinder pathfinder, int goalX, int goalY, int gates)
    {
        var field = new FlowField(map, pathfinder, goalX, goalY, gates);
        if (!pathfinder.IsPassable(goalX, goalY, gates)) return field; // nowhere to flow to
        int width = map.Width;
        var open = new PriorityQueue<int, long>();
        int goal = goalY * width + goalX;
        field._cost[goal] = 0;
        open.Enqueue(goal, 0);
        while (open.TryDequeue(out int node, out long key))
        {
            int cost = (int)(key >> 18);
            if (cost != field._cost[node]) continue; // stale entry
            int x = node % width, y = node / width;
            for (int dir = 0; dir < 8; dir++)
            {
                // The neighbour from which a step in `dir` arrives here.
                int px = x - Pathfinder.Dx[dir], py = y - Pathfinder.Dy[dir];
                if (!map.Contains(px, py) || !pathfinder.CanStep(px, py, dir, gates)) continue;
                int previous = py * width + px;
                int next = cost + StepCost(pathfinder, dir, x, y);
                if (next >= field._cost[previous]) continue;
                field._cost[previous] = next;
                open.Enqueue(previous, ((long)next << 18) | (uint)previous);
            }
        }
        return field;
    }

    private static int StepCost(Pathfinder pathfinder, int dir, int toX, int toY) =>
        ((dir & 1) == 0 ? StraightCost : DiagonalCost) * pathfinder.MoveCost(toX, toY);

    /// <summary>Whether the goal can be reached from (x, y).</summary>
    public bool Reaches(int x, int y) => _map.Contains(x, y) && _cost[y * _map.Width + x] != Unreachable;

    /// <summary>The direction to step from (x, y) toward the goal, or -1 at the goal or where it cannot be reached.</summary>
    public int Direction(int x, int y)
    {
        if (!Reaches(x, y) || (x == GoalX && y == GoalY)) return -1;
        int best = -1;
        long bestCost = long.MaxValue;
        for (int dir = 0; dir < 8; dir++)
        {
            if (!_pathfinder.CanStep(x, y, dir, Gates)) continue;
            int nx = x + Pathfinder.Dx[dir], ny = y + Pathfinder.Dy[dir];
            int there = _cost[ny * _map.Width + nx];
            if (there == Unreachable) continue;
            long cost = (long)there + StepCost(_pathfinder, dir, nx, ny);
            if (cost >= bestCost) continue;
            bestCost = cost;
            best = dir;
        }
        return best;
    }
}
