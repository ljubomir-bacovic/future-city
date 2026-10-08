namespace FutureCity.Sim;

/// <summary>Fixed simulation timing. One tick is the smallest step of game time.</summary>
public static class SimClock
{
    /// <summary>Ticks per real second at 1× speed.</summary>
    public const int TicksPerSecond = 10;

    /// <summary>Whole seconds of play time (at 1×) represented by a tick count.</summary>
    public static long ToSeconds(long ticks) => ticks / TicksPerSecond;

    /// <summary>Ticks in the given number of seconds of play time (at 1×).</summary>
    public static long FromSeconds(long seconds) => seconds * TicksPerSecond;
}
