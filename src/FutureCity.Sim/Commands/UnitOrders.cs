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
        var units = UnitOrders.Select(world, Player, Units);
        var spots = UnitOrders.SpreadAround(world, X, Y, units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            UnitOrders.Assign(unit, OrderKind.Move);
            Movement.SetGoal(world, ref unit.GetComponent<Mover>(), unit.GetComponent<TilePosition>(), spots[i].X, spots[i].Y);
        }
    }
}

/// <summary>Gather food from a plant or carcass, carrying it to camp until the source (and similar ones nearby) run out.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the plant or carcass.</param>
public sealed record Gather(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target) || !FoodSources.HasGatherableFood(target)) return;
        bool isPlant = target.TryGetComponent<Plant>(out var plant);
        int kind = isPlant ? plant.Kind : target.GetComponent<Carcass>().Kind;
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.Assign(unit, OrderKind.Gather, target, kind, isPlant);
    }
}

/// <summary>Chase and kill an animal, then butcher it and carry the meat to camp.</summary>
/// <param name="Units">Ids of the citizens.</param>
/// <param name="Target">Id of the animal (or of its carcass, which turns this into gathering).</param>
public sealed record Hunt(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target)) return;
        OrderKind kind;
        int targetKind;
        if (target.TryGetComponent<Animal>(out var animal)) (kind, targetKind) = (OrderKind.Hunt, animal.Kind);
        else if (target.TryGetComponent<Carcass>(out var carcass) && carcass.Food > 0) (kind, targetKind) = (OrderKind.Gather, carcass.Kind);
        else return;
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.Assign(unit, kind, target, targetKind, targetIsPlant: false);
    }
}

/// <summary>Walk back to camp, drop off any food carried, then wait there.</summary>
/// <param name="Units">Ids of the citizens.</param>
public sealed record ReturnToCamp(int[] Units) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        foreach (var unit in UnitOrders.Select(world, Player, Units))
            UnitOrders.Assign(unit, OrderKind.ReturnToCamp);
    }
}

/// <summary>Helpers shared by the unit order commands.</summary>
internal static class UnitOrders
{
    /// <summary>The listed units that exist, belong to <paramref name="player"/> and are adults, in id order without duplicates.</summary>
    public static List<Entity> Select(World world, int player, int[]? ids)
    {
        var result = new List<Entity>();
        if (ids == null) return result;
        foreach (int id in ids.Distinct().Order())
        {
            if (!world.TryGetEntity(id, out var unit)
                || !unit.TryGetComponent<Owner>(out var owner) || owner.Player != player
                || !unit.TryGetComponent<Citizen>(out var citizen) || !Bands.IsAdult(world, citizen)
                || !unit.HasComponent<Order>() || !unit.HasComponent<Mover>())
                continue;
            result.Add(unit);
        }
        return result;
    }

    /// <summary>Gives a unit an order without a target.</summary>
    public static void Assign(Entity unit, OrderKind kind) => unit.GetComponent<Order>() = new Order { Kind = kind };

    /// <summary>Gives a unit an order on a target. Travel starts on the next order update.</summary>
    public static void Assign(Entity unit, OrderKind kind, Entity target, int targetKind, bool targetIsPlant)
    {
        var pos = target.GetComponent<TilePosition>();
        unit.GetComponent<Order>() = new Order
        {
            Kind = kind, Target = target.Id, TargetKind = targetKind, TargetIsPlant = targetIsPlant, TargetX = pos.X, TargetY = pos.Y,
        };
    }

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
