using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// One tick of a citizen's work: how much gets done (tools help) and what it adds to the civilization's activity,
/// which the emergence engine turns into discoveries.
/// </summary>
public static class Labor
{
    /// <summary>Work done in one tick without tools. Work amounts are in these units (hundredths of a tick).</summary>
    public const int PerTick = 100;

    /// <summary>
    /// Does one tick of <paramref name="kind"/> work: records the activity, picks up nothing, wears the tool down.
    /// Returns the work done (<see cref="PerTick"/>, more with a tool).
    /// </summary>
    internal static int Work(World world, Entity unit, WorkKind kind)
    {
        Civics.RecordWork(world, unit.GetComponent<Owner>().Player, kind);
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (citizen.ToolWear <= 0) return PerTick;
        citizen.ToolWear--;
        return PerTick * (100 + world.Content.Tools.SpeedBonusPercent) / 100;
    }

    /// <summary>A citizen without a tool takes one from <paramref name="store"/> if it has any.</summary>
    internal static void PickUpTool(World world, Entity unit, Entity store)
    {
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (citizen.ToolWear > 0) return;
        var amounts = store.GetComponent<Inventory>().Amounts;
        if (amounts[world.Content.ToolGood] <= 0) return;
        amounts[world.Content.ToolGood]--;
        citizen.ToolWear = world.Content.Tools.Durability;
    }
}
