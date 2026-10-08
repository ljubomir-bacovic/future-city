namespace FutureCity.Sim;

/// <summary>
/// Everything chosen on the new-game screen. Together with the content and the command log
/// it fully determines a game.
/// </summary>
public sealed record GameSetup
{
    /// <summary>Random seed for the whole game.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Map size id from rules.json.</summary>
    public required string MapSize { get; init; }
}
