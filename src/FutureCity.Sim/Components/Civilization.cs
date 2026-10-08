using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>
/// A player's civilization as a whole: era, knowledge, institutions and the activity counters the emergence engine
/// reads. One entity per player (with an <see cref="Owner"/>). Arrays are indexed by tech, institution, good or
/// <see cref="WorkKind"/>.
/// </summary>
[ComponentKey("civilization")]
public struct Civilization : IComponent
{
    /// <summary>Index of the current era.</summary>
    public int Era;
    /// <summary>Technology the player chose to research at shrines, or -1.</summary>
    public int ResearchFocus;
    /// <summary>1 for each known technology.</summary>
    public int[] Techs;
    /// <summary>Progress toward each technology.</summary>
    public int[] TechProgress;
    /// <summary>1 for each established institution.</summary>
    public int[] Institutions;
    /// <summary>Units gathered from nature so far, by good.</summary>
    public int[] Gathered;
    /// <summary>Units made by farms and workshops so far, by good.</summary>
    public int[] Produced;
    /// <summary>Ticks of work done so far, by work kind.</summary>
    public int[] Work;
    /// <summary><see cref="Work"/> at the last emergence evaluation; the difference is the recent activity.</summary>
    public int[] LastWork;

    /// <summary>Silver content of newly struck coins, in percent; below 100 the coins are debased.</summary>
    public int CoinQuality;
    /// <summary>Share of what families bring home that they owe the treasury, in percent.</summary>
    public int TributePercent;
    /// <summary>Share of each market sale paid to the treasury, in percent.</summary>
    public int MarketTaxPercent;
    /// <summary>Share of each trade with foreign merchants paid to the treasury, in percent.</summary>
    public int TariffPercent;
    /// <summary>Wage per 100 ticks of public work, in coins (0 before coins: public workers get rations).</summary>
    public int PublicWage;
    /// <summary>Coins struck so far.</summary>
    public int Minted;
    /// <summary>Exchanges made at the marketplace so far.</summary>
    public int Trades;
    /// <summary>The treasury's accounts this year, by <see cref="LedgerEntry"/>.</summary>
    public int[] Ledger;
    /// <summary>The treasury's accounts last year, by <see cref="LedgerEntry"/>.</summary>
    public int[] LastLedger;
    /// <summary>People who starved to death this year.</summary>
    public int DeathsThisYear;
    /// <summary>People who starved to death last year.</summary>
    public int DeathsLastYear;
    /// <summary>Whether the people are in unrest.</summary>
    public bool Unrest;
    /// <summary>Tick at which the next merchant caravan sets out; 0 until there is a marketplace.</summary>
    public long NextMerchantTick;
    /// <summary>Under guilds: most workers each workshop kind may have this year, by building kind.</summary>
    public int[] GuildCap;
}

/// <summary>Lines of the treasury's accounts. All in coins except <see cref="Tribute"/>, in units of goods.</summary>
public enum LedgerEntry
{
    /// <summary>Goods received as tribute (units).</summary>
    Tribute,
    /// <summary>Market tax received.</summary>
    MarketTax,
    /// <summary>Tariffs received from merchants.</summary>
    Tariffs,
    /// <summary>Coins struck at the mint.</summary>
    Minted,
    /// <summary>Goods sold at the market.</summary>
    Sales,
    /// <summary>Wages paid to public workers.</summary>
    Wages,
    /// <summary>Goods bought at the market.</summary>
    Purchases,
}
