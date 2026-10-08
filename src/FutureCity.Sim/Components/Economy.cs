using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>
/// A family home: a completed hut once Private property exists. Its <see cref="Inventory"/> holds what the family
/// owns at home, and its <see cref="Trader"/> its coins and goods at the marketplace. Members are the citizens whose
/// <see cref="Citizen.Home"/> is this entity.
/// </summary>
[ComponentKey("household")]
public struct Household : IComponent
{
    /// <summary>Tribute owed to the treasury by good, in hundredths of a unit; paid by delivering whole loads.</summary>
    public int[] TributeOwed;
    /// <summary>Whether the family could not burn enough firewood this season.</summary>
    public bool Cold;
}

/// <summary>
/// Someone who trades at a marketplace: a household, the treasury (on the civilization entity) or a merchant caravan.
/// Arrays are indexed by good.
/// </summary>
[ComponentKey("trader")]
public struct Trader : IComponent
{
    /// <summary>Coins held.</summary>
    public int Coins;
    /// <summary>Goods the trader has physically at the marketplace: carried there to sell, or bought and not yet fetched.</summary>
    public int[] AtMarket;
    /// <summary>What the trader believes each good is worth, in coins. Moved by what happens on market days.</summary>
    public int[] Beliefs;
}

/// <summary>A marketplace's prices and history (on the marketplace building). Arrays are indexed by good.</summary>
[ComponentKey("market")]
public struct Market : IComponent
{
    /// <summary>Market days held so far.</summary>
    public int Days;
    /// <summary>Last price of each good (in coins; before coins, the value goods were bartered at).</summary>
    public int[] Price;
    /// <summary>Units of each good traded on the last market day.</summary>
    public int[] Volume;
    /// <summary>Price history: <c>[day % historyLength * goods + good]</c>.</summary>
    public int[] PriceHistory;
    /// <summary>Consumer price index history (100 = prices on the first day of trade in coins; 0 before).</summary>
    public int[] CpiHistory;
    /// <summary>Money supply history.</summary>
    public int[] MoneyHistory;
    /// <summary>Prices on the first day of trade in coins, the base of the price index; all zero before.</summary>
    public int[] BasePrice;
    /// <summary>Wants brought to barter on the last market day.</summary>
    public int BarterWants;
    /// <summary>Of those, wants that found a partner.</summary>
    public int BarterMatched;
    /// <summary>Commission paid to the market traders on the last market day.</summary>
    public int Commission;
    /// <summary>When bartering: the good most wanted on the last market day, accepted as payment (-1 if none).</summary>
    public int Medium;
}

/// <summary>Where a merchant caravan is in its visit.</summary>
public enum MerchantStage
{
    /// <summary>Walking from the map edge to the marketplace.</summary>
    Arriving,
    /// <summary>Trading at the marketplace.</summary>
    Trading,
    /// <summary>Walking back to the map edge with what it bought.</summary>
    Leaving,
}

/// <summary>
/// A foreign merchant caravan visiting a player's marketplace. Its <see cref="Inventory"/> holds what it carries on
/// the road; at the marketplace its goods are in its <see cref="Trader.AtMarket"/>.
/// </summary>
[ComponentKey("merchant")]
public struct Merchant : IComponent
{
    /// <summary>Player whose marketplace it visits.</summary>
    public int Player;
    /// <summary>Entity id of the marketplace.</summary>
    public int Market;
    /// <summary>Progress of the visit.</summary>
    public MerchantStage Stage;
    /// <summary>Tick at which it leaves the marketplace.</summary>
    public long LeaveTick;
    /// <summary>Column of the map edge it came from and returns to.</summary>
    public int EdgeX;
    /// <summary>Row of the map edge it came from and returns to.</summary>
    public int EdgeY;
    /// <summary>Units bought so far, by good.</summary>
    public int[] Bought;
    /// <summary>
    /// The civilization that sent it, or 0 for foreign merchants. A caravan sent under a trade agreement trades for its
    /// treasury and brings the proceeds home to it; one carrying tribute delivers it to the camp it visits.
    /// </summary>
    public int From;
    /// <summary>Whether it carries tribute (to the camp <see cref="Market"/>) rather than coming to trade.</summary>
    public bool Tribute;
}
