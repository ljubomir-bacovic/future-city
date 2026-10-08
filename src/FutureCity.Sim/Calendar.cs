using FutureCity.Sim.Content;

namespace FutureCity.Sim;

/// <summary>Years and seasons, derived from the tick. Every game starts on the first day of the first season.</summary>
public static class Calendar
{
    /// <summary>Ticks in one season.</summary>
    public static int TicksPerSeason(ContentDatabase content) =>
        content.Calendar.TicksPerYear / content.Calendar.Seasons.Count;

    /// <summary>Index of the current season.</summary>
    public static int SeasonIndex(World world) =>
        (int)(world.Tick / TicksPerSeason(world.Content) % world.Content.Calendar.Seasons.Count);

    /// <summary>The current season.</summary>
    public static SeasonDef Season(World world) => world.Content.Calendar.Seasons[SeasonIndex(world)];

    /// <summary>The current year, starting at 1.</summary>
    public static long Year(World world) => world.Tick / world.Content.Calendar.TicksPerYear + 1;
}
