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
    /// <summary>A building was finished (<see cref="SimEvent.Detail"/> = building kind).</summary>
    BuildingCompleted,
    /// <summary>A ripe crop was lost because nobody harvested it in time (<see cref="SimEvent.Entity"/> = the farm).</summary>
    CropsRotted,
    /// <summary>A technology was discovered (<see cref="SimEvent.Detail"/> = tech index).</summary>
    TechDiscovered,
    /// <summary>An institution was established (<see cref="SimEvent.Detail"/> = institution index).</summary>
    InstitutionEstablished,
    /// <summary>A civilization entered a new era (<see cref="SimEvent.Detail"/> = era index).</summary>
    EraReached,
    /// <summary>A merchant caravan arrived at a player's marketplace (<see cref="SimEvent.Entity"/> = the caravan).</summary>
    MerchantsArrived,
    /// <summary>A merchant caravan left a player's marketplace.</summary>
    MerchantsLeft,
    /// <summary>A civilization's people fell into unrest (average happiness too low).</summary>
    Unrest,
    /// <summary>A paid soldier went home unpaid for good.</summary>
    Deserted,
    /// <summary>A soldier or tower hit something (<see cref="SimEvent.Entity"/> = attacker, <see cref="SimEvent.Detail"/> = target id).</summary>
    Attacked,
    /// <summary>A person was killed by an enemy (<see cref="SimEvent.Player"/> = the side that lost them).</summary>
    DiedInBattle,
    /// <summary>A soldier broke and fled.</summary>
    Routed,
    /// <summary>A building was destroyed by enemies (<see cref="SimEvent.Detail"/> = building kind).</summary>
    BuildingDestroyed,
    /// <summary>Goods were carried off from a player's store (<see cref="SimEvent.Detail"/> = units).</summary>
    Plundered,
    /// <summary>A merchant caravan was attacked and its cargo spilled (<see cref="SimEvent.Player"/> = the market it was bound for).</summary>
    CaravanRaided,
    /// <summary>No market day: enemy soldiers are near the marketplace.</summary>
    MarketClosed,
    /// <summary>War was declared (<see cref="SimEvent.Player"/> = the declarer, <see cref="SimEvent.Detail"/> = the other side).</summary>
    WarDeclared,
    /// <summary>Another civilization made a proposal (<see cref="SimEvent.Player"/> = the receiver, <see cref="SimEvent.Detail"/> = the proposer).</summary>
    ProposalReceived,
    /// <summary>A proposal was accepted (<see cref="SimEvent.Player"/> = the proposer, <see cref="SimEvent.Detail"/> = the other side).</summary>
    ProposalAccepted,
    /// <summary>A proposal was declined (<see cref="SimEvent.Player"/> = the proposer, <see cref="SimEvent.Detail"/> = the other side).</summary>
    ProposalDeclined,
    /// <summary>Tribute could not be paid and the agreement lapsed (<see cref="SimEvent.Player"/> = the payer, <see cref="SimEvent.Detail"/> = the receiver).</summary>
    TributeLapsed,
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
/// <param name="Detail">Extra information depending on <paramref name="Kind"/> (e.g. which technology), or 0.</param>
public readonly record struct SimEvent(SimEventKind Kind, int Player, int Entity, int X, int Y, int Detail = 0);
