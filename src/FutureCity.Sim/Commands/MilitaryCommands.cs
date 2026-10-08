using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Commands;

/// <summary>
/// Calls up <paramref name="Count"/> adults as soldiers of a kind (fewer if not enough are free). Each first collects their
/// equipment from a public store, then goes to the rally point. Idle people are taken first, then people on jobs the chief
/// or their family chose. Refused when the unit's requirements are not met, or for paid service before there are coins.
/// </summary>
/// <param name="Unit">Unit id from military.json.</param>
/// <param name="Count">How many to recruit.</param>
/// <param name="Service">Levy (unpaid) or paid.</param>
public sealed record Recruit(string Unit, int Count, Service Service) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        int kind = world.Content.UnitIndex(Unit);
        if (Count <= 0 || !Enum.IsDefined(Service)
            || Military.CanRecruit(world, Player, kind, Service) != Recruitment.Ok)
            return;
        foreach (var unit in Military.Candidates(world, Player).Take(Count))
            Military.Enlist(world, unit, kind, Service);
    }
}

/// <summary>Sends soldiers home to their families and work. Their equipment is worn out and lost.</summary>
/// <param name="Units">Ids of the soldiers.</param>
public sealed record Disband(int[] Units) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        foreach (var unit in UnitOrders.Select(world, Player, Units, Who.Soldiers))
            Military.Discharge(world, unit);
    }
}

/// <summary>Sets where new soldiers gather once armed.</summary>
/// <param name="X">Tile column.</param>
/// <param name="Y">Tile row.</param>
public sealed record SetRallyPoint(int X, int Y) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.Map.Contains(X, Y) || !Civics.TryGet(world, Player, out var entity)) return;
        ref var civ = ref entity.GetComponent<Civilization>();
        civ.HasRally = true;
        civ.RallyX = X;
        civ.RallyY = Y;
    }
}

/// <summary>
/// Soldiers chase and fight an enemy unit or building. Only enemies of a civilization at war with the player can be
/// attacked (and caravans bound for their markets). Siege engines attack only buildings.
/// </summary>
/// <param name="Units">Ids of the soldiers.</param>
/// <param name="Target">Id of the enemy.</param>
public sealed record Attack(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target) || !Combat.IsEnemy(world, Player, target)) return;
        bool building = target.HasComponent<Building>();
        foreach (var unit in UnitOrders.Select(world, Player, Units, Who.Soldiers))
        {
            if (!building && Military.TypeOf(world, unit).Role == Content.UnitRole.Siege) continue;
            unit.GetComponent<Order>() = new Order
            {
                Kind = OrderKind.Attack, Target = target.Id, TargetType = building ? TargetType.Building : TargetType.Unit,
                Public = true,
            };
        }
    }
}

/// <summary>
/// Soldiers walk to a tile and fight every enemy they meet on the way; a group marches in <see cref="Formation"/>.
/// </summary>
/// <param name="Units">Ids of the soldiers.</param>
/// <param name="X">Target column.</param>
/// <param name="Y">Target row.</param>
public sealed record AttackMove(int[] Units, int X, int Y) : Command
{
    /// <summary>How the group lines up (Line unless given).</summary>
    public Formation Formation { get; init; }

    /// <inheritdoc />
    public override void Execute(World world)
    {
        var units = UnitOrders.Select(world, Player, Units, Who.Soldiers);
        int x = Math.Clamp(X, 0, world.Map.Width - 1), y = Math.Clamp(Y, 0, world.Map.Height - 1);
        var spots = UnitOrders.Send(world, units, x, y, Formation);
        for (int i = 0; i < units.Count; i++)
            units[i].GetComponent<Order>() = new Order { Kind = OrderKind.AttackMove, TargetX = spots[i].X, TargetY = spots[i].Y, Public = true };
    }
}

/// <summary>
/// Soldiers carry goods off from an enemy store or family home, or from ruins and spilled cargo, to their own public
/// stores, a load at a time, until nothing is left. Siege engines cannot carry anything.
/// </summary>
/// <param name="Units">Ids of the soldiers.</param>
/// <param name="Target">Id of the store, home or loot pile.</param>
public sealed record Loot(int[] Units, int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!world.TryGetEntity(Target, out var target) || !Combat.CanLoot(world, Player, target)) return;
        var type = target.HasComponent<LootPile>() ? TargetType.Loot : TargetType.Building;
        foreach (var unit in UnitOrders.Select(world, Player, Units, Who.Soldiers))
        {
            if (Military.TypeOf(world, unit).Role == Content.UnitRole.Siege) continue;
            unit.GetComponent<Order>() = new Order { Kind = OrderKind.Loot, Target = target.Id, TargetType = type, Public = true };
        }
    }
}
