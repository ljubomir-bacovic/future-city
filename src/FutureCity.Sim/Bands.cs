using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>Read-only facts about a player's band and its people, for systems, the UI and statistics.</summary>
public static class Bands
{
    /// <summary>Finds the player's camp (the one with the lowest id if there are several).</summary>
    public static bool TryGetCamp(World world, int player, out Entity camp)
    {
        foreach (var entity in World.InIdOrder(world.Store.Query<Camp, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player != player) continue;
            camp = entity;
            return true;
        }
        camp = default;
        return false;
    }

    /// <summary>Age in whole years.</summary>
    public static int AgeInYears(World world, in Citizen citizen) =>
        (int)((world.Tick - citizen.BirthTick) / world.Content.Citizens.TicksPerYear);

    /// <summary>Whether the citizen is old enough to take orders.</summary>
    public static bool IsAdult(World world, in Citizen citizen) =>
        AgeInYears(world, citizen) >= world.Content.Citizens.AdultAgeYears;

    /// <summary>Counts the player's people.</summary>
    public static Census CensusOf(World world, int player)
    {
        int adults = 0, children = 0;
        foreach (var entity in world.Store.Query<Citizen, Owner>().Entities)
        {
            if (entity.GetComponent<Owner>().Player != player) continue;
            if (IsAdult(world, entity.GetComponent<Citizen>())) adults++;
            else children++;
        }
        return new Census(adults, children);
    }
}

/// <summary>Head count of a band.</summary>
/// <param name="Adults">People old enough to work.</param>
/// <param name="Children">People too young to work.</param>
public readonly record struct Census(int Adults, int Children)
{
    /// <summary>Everyone.</summary>
    public int Total => Adults + Children;
}
