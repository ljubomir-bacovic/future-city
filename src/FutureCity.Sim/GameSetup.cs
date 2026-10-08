namespace FutureCity.Sim;

/// <summary>
/// Everything chosen on the new-game screen. Together with the content and the command log
/// it fully determines a game.
/// </summary>
public sealed record GameSetup
{
    /// <summary>Most civilizations a map can hold.</summary>
    public const int MaxCivilizations = 4;

    /// <summary>Random seed for the whole game.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Map size id from rules.json.</summary>
    public required string MapSize { get; init; }

    /// <summary>Civilizations on the map (1 to <see cref="MaxCivilizations"/>), played by players 1, 2, …</summary>
    public int Civilizations { get; init; } = 1;
}
