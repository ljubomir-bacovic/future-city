using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Map;

/// <summary>Connectivity of walkable land.</summary>
public static class Regions
{
    /// <summary>
    /// Labels each walkable tile with the id (1, 2, …) of the connected area it belongs to; blocked tiles get 0.
    /// Areas are numbered in scan order, so labels are deterministic.
    /// </summary>
    public static int[] Label(TileMap map, Pathfinder pathfinder, out int count)
    {
        var labels = new int[map.Width * map.Height];
        var queue = new Queue<int>();
        count = 0;
        for (int start = 0; start < labels.Length; start++)
        {
            if (labels[start] != 0 || !pathfinder.IsWalkable(start % map.Width, start / map.Width)) continue;
            int label = ++count;
            labels[start] = label;
            queue.Enqueue(start);
            while (queue.TryDequeue(out int node))
            {
                int x = node % map.Width, y = node / map.Width;
                for (int dir = 0; dir < 8; dir++)
                {
                    if (!pathfinder.CanStep(x, y, dir)) continue;
                    int next = (y + Pathfinder.Dy[dir]) * map.Width + x + Pathfinder.Dx[dir];
                    if (labels[next] != 0) continue;
                    labels[next] = label;
                    queue.Enqueue(next);
                }
            }
        }
        return labels;
    }
}
