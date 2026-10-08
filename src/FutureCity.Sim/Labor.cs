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
    /// Does one tick of <paramref name="kind"/> work: records the activity and wears the tool down. Returns the work
    /// done: <see cref="PerTick"/>, more with a tool, and (once families own their goods) more or less with happiness.
    /// </summary>
    internal static int Work(World world, Entity unit, WorkKind kind)
    {
        int player = unit.GetComponent<Owner>().Player;
        Civics.RecordWork(world, player, kind);
        ref var citizen = ref unit.GetComponent<Citizen>();
        int work = PerTick;
        if (citizen.ToolWear > 0)
        {
            citizen.ToolWear--;
            work = work * (100 + world.Content.Tools.SpeedBonusPercent) / 100;
        }
        if (Economy.HasHouseholds(world, player)) work = work * Productivity(world, citizen.Happiness) / 100;
        return work;
    }

    /// <summary>Work speed in percent at a happiness level: normal at 50, slower when unhappy, faster when happy.</summary>
    public static int Productivity(World world, int happiness)
    {
        var rules = world.Content.Economy.Happiness;
        int h = Math.Clamp(happiness, 0, 100);
        return rules.ProductivityAtZero + (rules.ProductivityAtHundred - rules.ProductivityAtZero) * h / 100;
    }

    /// <summary>A citizen without a tool takes one from <paramref name="store"/> if it has any (soldiers do not work, so they do not).</summary>
    internal static void PickUpTool(World world, Entity unit, Entity store)
    {
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (citizen.ToolWear > 0 || unit.HasComponent<Soldier>()) return;
        var amounts = store.GetComponent<Inventory>().Amounts;
        if (amounts[world.Content.ToolGood] <= 0) return;
        amounts[world.Content.ToolGood]--;
        citizen.ToolWear = world.Content.Tools.Durability;
    }
}
