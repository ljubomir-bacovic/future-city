using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim.Navigation;

/// <summary>How a group of soldiers arranges itself.</summary>
public enum Formation
{
    /// <summary>A broad front, a few ranks deep: melee in front, archers behind, siege engines last.</summary>
    Line,
    /// <summary>Two abreast, for marching through narrow ground.</summary>
    Column,
}

/// <summary>Places in a formation, and marching a group into them.</summary>
public static class Formations
{
    /// <summary>Groups with at least this many members, including a soldier, march in formation.</summary>
    public const int MinGroup = 4;

    /// <summary>
    /// Sends the units to their places in <paramref name="formation"/> around (x, y), facing the way they march: all of
    /// them follow one shared flow field there and walk at the pace of the slowest. Returns each unit's place, in the
    /// order of <paramref name="units"/>.
    /// </summary>
    public static List<(int X, int Y)> March(World world, IReadOnlyList<Entity> units, int x, int y, Formation formation)
    {
        var slots = Slots(world, units, x, y, formation);
        int pace = units.Max(u => u.GetComponent<Mover>().TicksPerTile);
        for (int i = 0; i < units.Count; i++)
        {
            ref var mover = ref units[i].GetComponent<Mover>();
            mover.TicksPerTile = pace; // restored by the combat system once they stop
            Movement.March(world, ref mover, units[i].GetComponent<TilePosition>(), slots[i].X, slots[i].Y, x, y);
        }
        return slots;
    }

    /// <summary>
    /// Each unit's place in the formation around (x, y). The front faces from the group's centre toward (x, y); ranks run
    /// back from there. Front-role units fill the first ranks, then back-role units, then siege engines (by id within a
    /// role); each role starts a new rank. Places on blocked ground move to the nearest free walkable tile.
    /// </summary>
    public static List<(int X, int Y)> Slots(World world, IReadOnlyList<Entity> units, int x, int y, Formation formation)
    {
        int n = units.Count;
        long sx = 0, sy = 0;
        foreach (var unit in units)
        {
            var pos = unit.GetComponent<TilePosition>();
            sx += pos.X;
            sy += pos.Y;
        }
        int fx = Math.Sign(x - (int)(sx / Math.Max(1, n))), fy = Math.Sign(y - (int)(sy / Math.Max(1, n)));
        if (fx == 0 && fy == 0) fy = 1;
        int px = -fy, py = fx; // along the front
        int width = formation == Formation.Column ? 2 : Math.Max(3, (n + 1) / 2);

        var order = Enumerable.Range(0, n)
            .OrderBy(i => RoleRank(world, units[i])).ThenBy(i => units[i].Id)
            .ToList();
        var slots = new (int X, int Y)[n];
        var taken = new HashSet<(int, int)>();
        var pathfinder = world.Pathfinder;
        int gates = n > 0 ? units[0].GetComponent<Mover>().Gates : 0;
        int rank = 0, file = 0, role = -1;
        foreach (int i in order)
        {
            int r = RoleRank(world, units[i]);
            if (file == width || (r != role && file > 0)) { rank++; file = 0; } // each role starts its own rank
            role = r;
            int offset = file - (width - 1) / 2;
            slots[i] = NearestFree(world, pathfinder, x + px * offset - fx * rank, y + py * offset - fy * rank, gates, taken);
            file++;
        }
        return slots.ToList();
    }

    private static int RoleRank(World world, Entity unit) =>
        unit.TryGetComponent<Soldier>(out var soldier) ? (int)world.Content.Units[soldier.Kind].Role : (int)UnitRole.Back;

    private static (int X, int Y) NearestFree(World world, Pathfinder pathfinder, int x, int y, int gates, HashSet<(int, int)> taken)
    {
        x = Math.Clamp(x, 0, world.Map.Width - 1);
        y = Math.Clamp(y, 0, world.Map.Height - 1);
        int maxRing = Math.Max(world.Map.Width, world.Map.Height);
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int ty = y - ring; ty <= y + ring; ty++)
            {
                for (int tx = x - ring; tx <= x + ring; tx++)
                {
                    if (Math.Max(Math.Abs(tx - x), Math.Abs(ty - y)) != ring || !pathfinder.IsPassable(tx, ty, gates)
                        || !taken.Add((tx, ty)))
                        continue;
                    return (tx, ty);
                }
            }
        }
        return (x, y);
    }
}
