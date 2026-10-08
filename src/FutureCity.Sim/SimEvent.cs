namespace FutureCity.Sim;

/// <summary>Kinds of notable things that happen during a tick.</summary>
public enum SimEventKind
{
    /// <summary>A child was born.</summary>
    Birth,
    /// <summary>A citizen starved to death.</summary>
    DiedOfStarvation,
    /// <summary>A citizen died of old age.</summary>
    DiedOfOldAge,
    /// <summary>A hunter killed an animal.</summary>
    AnimalKilled,
    /// <summary>A wild animal was born.</summary>
    AnimalBorn,
}

/// <summary>
/// A notable thing that happened during the last tick, for the UI, the codex and statistics.
/// Events are output only: they are cleared every tick, never saved, and must never feed back into game rules.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Player">The player it happened to (<see cref="Players.Nature"/> for wildlife).</param>
/// <param name="Entity">The entity concerned (it may no longer exist).</param>
/// <param name="X">Tile column where it happened.</param>
/// <param name="Y">Tile row where it happened.</param>
public readonly record struct SimEvent(SimEventKind Kind, int Player, int Entity, int X, int Y);
