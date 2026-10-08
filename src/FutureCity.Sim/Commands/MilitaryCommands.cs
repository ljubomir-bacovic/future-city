using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

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
