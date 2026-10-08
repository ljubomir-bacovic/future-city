using FutureCity.Sim.Content;
using FutureCity.Sim.Map;

namespace FutureCity.Sim.Navigation;

/// <summary>
/// Grid A* for individual units: 8 directions, no cutting corners past blocked tiles,
/// step costs from terrain <see cref="TerrainDef.MoveCost"/>. Finished walls block movement too, except a gate for
/// its owner's people (<c>gates</c> = the player whose gates a unit may pass; <see cref="IgnoreWalls"/> for questions
/// about the land itself). Fully deterministic: ties in the open list are broken by tile index, never by heap internals.
/// </summary>
public sealed class Pathfinder
{
    /// <summary>Passed as <c>gates</c> to ask about the land alone, as if no walls stood.</summary>
    public const int IgnoreWalls = -1;

    /// <summary>Column offset of each direction (0 = east, then clockwise in screen space).</summary>
    public static readonly int[] Dx = [1, 1, 0, -1, -1, -1, 0, 1];

    /// <summary>Row offset of each direction.</summary>
    public static readonly int[] Dy = [0, 1, 1, 1, 0, -1, -1, -1];

    private const int StraightCost = 10;
    private const int DiagonalCost = 14;
    private const int IndexBits = 18; // maps are at most 512 x 512 = 2^18 tiles

    private readonly TileMap _map;
    private readonly int[] _moveCost;     // per terrain index, percent; 0 = blocked
    private readonly int _minMoveCost;
    private readonly int[] _g;
    private readonly int[] _visited;      // generation stamp: node has a valid g
    private readonly int[] _closed;       // generation stamp: node is expanded
    private readonly byte[] _cameFrom;    // direction used to enter the node
    private readonly PriorityQueue<int, long> _open = new();
    private readonly List<int> _route = [];
    private int _generation;

    /// <summary>Creates a pathfinder for <paramref name="map"/>.</summary>
    public Pathfinder(TileMap map, ContentDatabase content)
    {
        _map = map;
        _moveCost = content.Terrains.Select(t => t.Walkable ? t.MoveCost : 0).ToArray();
        _minMoveCost = Math.Max(1, _moveCost.Where(c => c > 0).DefaultIfEmpty(100).Min());
        int size = map.Width * map.Height;
        _g = new int[size];
        _visited = new int[size];
        _closed = new int[size];
        _cameFrom = new byte[size];
    }

    /// <summary>Whether units can stand on (x, y).</summary>
    public bool IsWalkable(int x, int y) => _map.Contains(x, y) && _moveCost[_map.GetTerrain(x, y)] > 0;

    /// <summary>Terrain cost of entering (x, y), in percent of open grassland.</summary>
    public int MoveCost(int x, int y) => _moveCost[_map.GetTerrain(x, y)];

    /// <summary>Whether a unit that may pass the gates of player <paramref name="gates"/> can stand on (x, y).</summary>
    public bool IsPassable(int x, int y, int gates) =>
        IsWalkable(x, y) && (gates == IgnoreWalls || !_map.HasWalls || _map.WallLets(x, y, gates));

    /// <summary>Whether a unit on (x, y) may step in <paramref name="direction"/> (see <see cref="IsPassable"/>).</summary>
    public bool CanStep(int x, int y, int direction, int gates = IgnoreWalls)
    {
        int nx = x + Dx[direction], ny = y + Dy[direction];
        if (!IsPassable(nx, ny, gates)) return false;
        // Diagonal steps may not squeeze between two blocked tiles or clip a blocked corner.
        return (direction & 1) == 0 || (IsPassable(nx, y, gates) && IsPassable(x, ny, gates));
    }

    /// <summary>
    /// Plans a route from the start to the goal tile. If the goal cannot be reached, the route leads to the
    /// reachable tile closest to it. Returns the steps as directions (indexes into <see cref="Dx"/>/<see cref="Dy"/>);
    /// the list is empty when the start is already the best reachable tile. The list is reused by the next call.
    /// </summary>
    public IReadOnlyList<int> FindRoute(int startX, int startY, int goalX, int goalY, int gates = IgnoreWalls)
    {
        _route.Clear();
        if (!_map.Contains(startX, startY)) return _route;
        goalX = Math.Clamp(goalX, 0, _map.Width - 1);
        goalY = Math.Clamp(goalY, 0, _map.Height - 1);
        if (startX == goalX && startY == goalY) return _route;

        if (++_generation == int.MaxValue)
        {
            Array.Clear(_visited);
            Array.Clear(_closed);
            _generation = 1;
        }

        int width = _map.Width;
        int start = startY * width + startX;
        int goal = goalY * width + goalX;
        _open.Clear();
        _g[start] = 0;
        _visited[start] = _generation;
        _open.Enqueue(start, Key(Heuristic(startX, startY, goalX, goalY), start));

        int best = start, bestH = Heuristic(startX, startY, goalX, goalY), bestG = 0;
        while (_open.TryDequeue(out int node, out _))
        {
            if (_closed[node] == _generation) continue; // stale duplicate entry
            _closed[node] = _generation;

            int x = node % width, y = node / width;
            int h = Heuristic(x, y, goalX, goalY);
            if (h < bestH || (h == bestH && _g[node] < bestG))
            {
                best = node;
                bestH = h;
                bestG = _g[node];
            }
            if (node == goal) break;

            for (int dir = 0; dir < 8; dir++)
            {
                if (!CanStep(x, y, dir, gates)) continue;
                int nx = x + Dx[dir], ny = y + Dy[dir];
                int next = ny * width + nx;
                if (_closed[next] == _generation) continue;
                int g = _g[node] + ((dir & 1) == 0 ? StraightCost : DiagonalCost) * MoveCost(nx, ny);
                if (_visited[next] == _generation && g >= _g[next]) continue;
                _g[next] = g;
                _visited[next] = _generation;
                _cameFrom[next] = (byte)dir;
                _open.Enqueue(next, Key(g + Heuristic(nx, ny, goalX, goalY), next));
            }
        }

        for (int node = best; node != start;)
        {
            int dir = _cameFrom[node];
            _route.Add(dir);
            node -= Dy[dir] * width + Dx[dir];
        }
        _route.Reverse();
        return _route;
    }

    // Octile distance scaled by the cheapest terrain, so it never overestimates.
    private int Heuristic(int x, int y, int goalX, int goalY)
    {
        int dx = Math.Abs(x - goalX), dy = Math.Abs(y - goalY);
        int diagonal = Math.Min(dx, dy), straight = Math.Max(dx, dy) - diagonal;
        return (straight * StraightCost + diagonal * DiagonalCost) * _minMoveCost;
    }

    // Unique priority: f first, then tile index, so equal-cost ties never depend on the heap implementation.
    private static long Key(int f, int index) => ((long)f << IndexBits) | (uint)index;
}
