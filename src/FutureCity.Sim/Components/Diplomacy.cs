using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>How two civilizations stand toward each other.</summary>
public enum Relation
{
    /// <summary>No war: soldiers leave each other alone.</summary>
    Peace,
    /// <summary>A defensive pact: each joins the other's wars of defence.</summary>
    Alliance,
    /// <summary>Soldiers fight, raid and loot.</summary>
    War,
}

/// <summary>What one civilization proposes to another.</summary>
public enum ProposalKind
{
    /// <summary>No proposal.</summary>
    None,
    /// <summary>End the war.</summary>
    Peace,
    /// <summary>A defensive alliance.</summary>
    Alliance,
    /// <summary>A trade agreement: no tariffs, and yearly trade caravans between the treasuries.</summary>
    TradeAgreement,
    /// <summary>The proposer pays the other side tribute every year.</summary>
    OfferTribute,
    /// <summary>The proposer asks to be paid tribute every year.</summary>
    DemandTribute,
}

/// <summary>
/// A civilization's relations with the others, on its civilization entity. Arrays are indexed by player id
/// (0 to <see cref="GameSetup.MaxCivilizations"/>); both sides of a relation always hold the same value.
/// </summary>
[ComponentKey("diplomacy")]
public struct Diplomacy : IComponent
{
    /// <summary>Relation with each player, as <see cref="Components.Relation"/>.</summary>
    public int[] Relation;
    /// <summary>1 where a trade agreement holds.</summary>
    public int[] TradeAgreement;
    /// <summary>Good this civilization pays each player as tribute every year (-1 = coins).</summary>
    public int[] TributeGood;
    /// <summary>Units (or coins) of tribute this civilization pays each player every year; 0 = none.</summary>
    public int[] TributeAmount;
    /// <summary>Proposal each player has made to this civilization and that awaits an answer, as <see cref="ProposalKind"/>.</summary>
    public int[] Proposal;
    /// <summary>Good of each pending tribute proposal (-1 = coins).</summary>
    public int[] ProposalGood;
    /// <summary>Amount per year of each pending tribute proposal.</summary>
    public int[] ProposalAmount;
}
