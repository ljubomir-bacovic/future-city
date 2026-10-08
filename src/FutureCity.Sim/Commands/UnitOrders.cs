using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Commands;

/// <summary>Walk to a tile. A group spreads out over the nearest free tiles around it.</summary>
/// <param name="Units">Ids of the citizens to move.</param>
/// <param name="X">Target column.</param>
/// <param name="Y">Target row.</param>
public sealed record MoveUnits(int[] Units, int X, int Y) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        var units = UnitOrders.Select(world, Player, Units, Who.Everyone);
        int x = Math.Clamp(X, 0, world.Map.Width - 1), y = Math.Clamp(Y, 0, world.Map.Height - 1);
        var spots = UnitOrders.SpreadAround(world, x, y, units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            UnitOrders.Assign(unit, OrderKind.Move);
            Movement.SetGoal(world, ref unit.GetComponent<Mover>(), unit.GetComponent<TilePosition>(), spots[i].X, spots[i].Y);
        }
    }
}

/// <summary>
/// Take goods from a plant, carcass or deposit and carry them to the nearest store until it (and similar sources
/// nearby) run out.
/// </summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the plant, carcass or deposit.</param>
public sealed record Gather(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target) || !Sources.HasGoods(target)) return;
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.Assign(unit, OrderKind.Gather, target, Sources.TypeOf(target), Sources.KindOf(target));
    }
}

/// <summary>Take a terrain resource (wood from a forest tile) and carry it to the nearest store, moving on to nearby tiles of the same terrain.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="X">Tile column.</param>
/// <param name="Y">Tile row.</param>
public sealed record GatherTile(int[] Units, int X, int Y) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (Sources.TileInfo(world, X, Y) == null) return;
        int terrain = world.Map.GetTerrain(X, Y);
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.AssignTile(unit, X, Y, terrain);
    }
}

/// <summary>Chase and kill an animal, then butcher it and carry the meat to a store.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the animal (or of its carcass, which turns this into gathering).</param>
public sealed record Hunt(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target)) return;
        if (target.TryGetComponent<Animal>(out var animal))
        {
            foreach (var unit in UnitOrders.Select(world, Player, Units))
                UnitOrders.Assign(unit, OrderKind.Hunt, target, TargetType.Animal, animal.Kind);
        }
        else if (target.TryGetComponent<Carcass>(out var carcass) && carcass.Food > 0)
        {
            foreach (var unit in UnitOrders.Select(world, Player, Units))
                UnitOrders.Assign(unit, OrderKind.Gather, target, TargetType.Carcass, carcass.Kind);
        }
    }
}

/// <summary>Walk back to camp, drop off anything carried, then wait there.</summary>
/// <param name="Units">Ids of the citizens.</param>
public sealed record ReturnToCamp(int[] Units) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        foreach (var unit in UnitOrders.Select(world, Player, Units, Who.Everyone))
            UnitOrders.Assign(unit, OrderKind.ReturnToCamp);
    }
}

/// <summary>Bring materials from the stores to one of the player's construction sites and build it, or repair a damaged building.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the construction site.</param>
public sealed record Build(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!UnitOrders.TryGetOwnBuilding(world, Player, Target, out var site)
            || (Buildings.IsComplete(site) && site.GetComponent<Building>().Damage == 0))
            return;
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.Assign(unit, OrderKind.Build, site, TargetType.Building, site.GetComponent<Building>().Kind);
    }
}

/// <summary>Work at one of the player's farms, workshops or shrines, up to its number of jobs.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the building.</param>
public sealed record AssignWork(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!UnitOrders.TryGetOwnBuilding(world, Player, Target, out var building) || !Buildings.IsComplete(building)) return;
        var type = Buildings.TypeOf(world, building);
        int free = type.Def.Workers - Buildings.WorkersAt(world, building.Id);
        foreach (var unit in UnitOrders.Select(world, Player, Units))
        {
            var order = unit.GetComponent<Order>();
            if (order.Kind == OrderKind.Work && order.Target == building.Id) continue; // already works here
            if (free-- <= 0) break;
            UnitOrders.Assign(unit, OrderKind.Work, building, TargetType.Building, type.Index);
        }
    }
}

/// <summary>Which of the selected people an order applies to.</summary>
internal enum Who
{
    /// <summary>Civilians only: soldiers do not gather, build or work.</summary>
    Civilians,
    /// <summary>Soldiers only.</summary>
    Soldiers,
    /// <summary>Everyone.</summary>
    Everyone,
}

/// <summary>Helpers shared by the unit order commands (and automatic job assignment).</summary>
internal static class UnitOrders
{
    /// <summary>
    /// The listed units that exist, belong to <paramref name="player"/>, are adults and are among <paramref name="who"/>,
    /// in id order without duplicates.
    /// </summary>
    public static List<Entity> Select(World world, int player, int[]? ids, Who who = Who.Civilians)
    {
        var result = new List<Entity>();
        if (ids == null) return result;
        foreach (int id in ids.Distinct().Order())
        {
            if (!world.TryGetEntity(id, out var unit)
                || !unit.TryGetComponent<Owner>(out var owner) || owner.Player != player
                || !unit.TryGetComponent<Citizen>(out var citizen) || !Bands.IsAdult(world, citizen)
                || !unit.HasComponent<Order>() || !unit.HasComponent<Mover>()
                || (who == Who.Civilians && unit.HasComponent<Soldier>()) || (who == Who.Soldiers && !unit.HasComponent<Soldier>()))
                continue;
            result.Add(unit);
        }
        return result;
    }

    /// <summary>Finds a building of the player's.</summary>
    public static bool TryGetOwnBuilding(World world, int player, int id, out Entity building) =>
        world.TryGetEntity(id, out building) && building.HasComponent<Building>()
        && building.GetComponent<Owner>().Player == player;

    /// <summary>Gives a unit an order without a target.</summary>
    public static void Assign(Entity unit, OrderKind kind) => unit.GetComponent<Order>() = new Order { Kind = kind, Public = true };

    /// <summary>Gives a unit an order on a target entity. Travel starts on the next order update.</summary>
    public static void Assign(Entity unit, OrderKind kind, Entity target, TargetType type, int targetKind)
    {
        var pos = target.GetComponent<TilePosition>();
        unit.GetComponent<Order>() = new Order
        {
            Kind = kind, Target = target.Id, TargetType = type, TargetKind = targetKind, TargetX = pos.X, TargetY = pos.Y, Public = true,
        };
    }

    /// <summary>Gives a unit an order to gather from the terrain tile (x, y).</summary>
    public static void AssignTile(Entity unit, int x, int y, int terrain) =>
        unit.GetComponent<Order>() = new Order
        {
            Kind = OrderKind.Gather, TargetType = TargetType.Tile, TargetKind = terrain, TargetX = x, TargetY = y, Public = true,
        };

    /// <summary>The <paramref name="count"/> walkable tiles nearest (x, y), ring by ring in scan order.</summary>
    public static List<(int X, int Y)> SpreadAround(World world, int x, int y, int count)
    {
        var spots = new List<(int X, int Y)>(count);
        var pathfinder = world.Pathfinder;
        int maxRing = Math.Max(world.Map.Width, world.Map.Height);
        for (int ring = 0; spots.Count < count && ring <= maxRing; ring++)
        {
            for (int ty = y - ring; ty <= y + ring && spots.Count < count; ty++)
            {
                for (int tx = x - ring; tx <= x + ring && spots.Count < count; tx++)
                {
                    if (Math.Max(Math.Abs(tx - x), Math.Abs(ty - y)) == ring && pathfinder.IsWalkable(tx, ty))
                        spots.Add((tx, ty));
                }
            }
        }
        while (spots.Count < count) spots.Add((x, y)); // nowhere walkable: units walk as close as they can
        return spots;
    }
}
