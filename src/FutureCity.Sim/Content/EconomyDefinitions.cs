namespace FutureCity.Sim.Content;

/// <summary>What households keep and offer.</summary>
public sealed record HouseholdRules
{
    /// <summary>Meals a family wants to keep per member.</summary>
    public required int FoodTargetPerMember { get; init; }
    /// <summary>A family offers what it holds beyond its target times this percentage.</summary>
    public required int SurplusPercent { get; init; }
    /// <summary>A crafter's family keeps inputs for this many batches.</summary>
    public required int InputBatches { get; init; }
    /// <summary>Id of the good families burn for warmth.</summary>
    public required string Firewood { get; init; }
}

/// <summary>What the treasury keeps.</summary>
public sealed record TreasuryRules
{
    /// <summary>Meals per person the treasury keeps as a reserve.</summary>
    public required int FoodTargetPerCapita { get; init; }
    /// <summary>Silver the treasury keeps for the mint.</summary>
    public required int SilverTarget { get; init; }
    /// <summary>Goods only public workers may take from nature (ids), such as silver from the crown's mines.</summary>
    public required IReadOnlyList<string> Regalia { get; init; }
}

/// <summary>A tax rate the player sets.</summary>
public sealed record TaxRule
{
    /// <summary>Rate at the start, in percent.</summary>
    public required int Default { get; init; }
    /// <summary>Highest rate allowed, in percent.</summary>
    public required int Max { get; init; }
}

/// <summary>The player's tax rates.</summary>
public sealed record TaxRules
{
    /// <summary>Share of what households bring home, paid in goods.</summary>
    public required TaxRule Tribute { get; init; }
    /// <summary>Share of every market sale, in coins.</summary>
    public required TaxRule MarketTax { get; init; }
    /// <summary>Share of every trade with foreign merchants, in coins.</summary>
    public required TaxRule Tariff { get; init; }
}

/// <summary>How the marketplace works.</summary>
public sealed record MarketRules
{
    /// <summary>Ticks between market days.</summary>
    public required int IntervalTicks { get; init; }
    /// <summary>Market days of history kept.</summary>
    public required int HistoryLength { get; init; }
    /// <summary>A buyer who got nothing raises its belief by this percentage; a seller who sold nothing lowers it.</summary>
    public required int BeliefStepPercent { get; init; }
    /// <summary>A trader who traded moves its belief this far (percent) toward the price.</summary>
    public required int BeliefPullPercent { get; init; }
    /// <summary>A trader short of a good bids up to this much above its belief.</summary>
    public required int NeedPremiumPercent { get; init; }
    /// <summary>A trader with a large surplus asks up to this much below its belief.</summary>
    public required int SurplusDiscountPercent { get; init; }
    /// <summary>Share of each sale paid to the market traders working that day.</summary>
    public required int CommissionPercent { get; init; }
    /// <summary>Beliefs never exceed a good's starting value times this.</summary>
    public required int MaxPriceMultiple { get; init; }
}

/// <summary>Coins.</summary>
public sealed record MoneyRules
{
    /// <summary>Coins struck from one silver at full quality.</summary>
    public required int CoinsPerSilver { get; init; }
    /// <summary>Lowest coin quality (silver content in percent) the player can set.</summary>
    public required int MinQuality { get; init; }
}

/// <summary>Wages and job choice.</summary>
public sealed record WageRules
{
    /// <summary>Before coins, the treasury feeds one public worker per this many public meals.</summary>
    public required int RationMeals { get; init; }
    /// <summary>After coins, the treasury hires as many public workers as it can pay for this many job checks.</summary>
    public required int ReserveChecks { get; init; }
    /// <summary>Most of the adults living in family homes who work for the treasury, in percent.</summary>
    public required int MaxPublicPercent { get; init; }
    /// <summary>Public wage = typical private income + this percentage.</summary>
    public required int PublicPremiumPercent { get; init; }
    /// <summary>People change jobs only for this much more income, in percent.</summary>
    public required int SwitchMarginPercent { get; init; }
    /// <summary>Most people who change jobs per check.</summary>
    public required int SwitchesPerCheck { get; init; }
    /// <summary>Share of a gatherer's time spent working rather than walking, for income estimates.</summary>
    public required int TravelPercent { get; init; }
    /// <summary>Expected income from a job falls by this percentage for each person already doing it.</summary>
    public required int CrowdingPercent { get; init; }
    /// <summary>Extra value of food to a family with less than half its target, in percent.</summary>
    public required int HungerPremiumPercent { get; init; }
}

/// <summary>Visiting foreign merchants.</summary>
public sealed record MerchantRules
{
    /// <summary>Ticks between caravans.</summary>
    public required int VisitIntervalTicks { get; init; }
    /// <summary>Ticks a caravan trades before it leaves.</summary>
    public required int StayTicks { get; init; }
    /// <summary>People in a caravan.</summary>
    public required int Porters { get; init; }
    /// <summary>Coins a caravan brings.</summary>
    public required int Coins { get; init; }
    /// <summary>Goods a caravan brings to sell, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Cargo { get; init; }
    /// <summary>Most of each good a caravan buys, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Wants { get; init; }
    /// <summary>They sell no cheaper than world price plus this percentage.</summary>
    public required int SellMarkupPercent { get; init; }
    /// <summary>They buy no dearer than world price minus this percentage.</summary>
    public required int BuyDiscountPercent { get; init; }
}

/// <summary>Guild regulation of crafts.</summary>
public sealed record GuildRules
{
    /// <summary>Extra output per batch, in percent.</summary>
    public required int OutputBonusPercent { get; init; }
    /// <summary>Members never sell below input cost plus this percentage.</summary>
    public required int MarginPercent { get; init; }
    /// <summary>New members a craft admits per year.</summary>
    public required int NewMembersPerYear { get; init; }
}

/// <summary>Happiness targets and effects.</summary>
public sealed record HappinessRules
{
    /// <summary>How often happiness moves.</summary>
    public required int CheckIntervalTicks { get; init; }
    /// <summary>Points it moves per check toward the target.</summary>
    public required int Step { get; init; }
    /// <summary>Target before any effects.</summary>
    public required int Base { get; init; }
    /// <summary>Not hungry.</summary>
    public required int Fed { get; init; }
    /// <summary>Close to starving.</summary>
    public required int Hungry { get; init; }
    /// <summary>Lives in a family home.</summary>
    public required int Home { get; init; }
    /// <summary>Lives at the camp.</summary>
    public required int Homeless { get; init; }
    /// <summary>The family ran out of firewood.</summary>
    public required int Cold { get; init; }
    /// <summary>Penalty per percentage point of tribute plus market tax, in percent.</summary>
    public required int TaxPercent { get; init; }
    /// <summary>Health below half.</summary>
    public required int Sick { get; init; }
    /// <summary>Per starvation death last year.</summary>
    public required int DeathPenalty { get; init; }
    /// <summary>Largest total death penalty.</summary>
    public required int MaxDeathPenalty { get; init; }
    /// <summary>Family wealth below a third of the average.</summary>
    public required int Poor { get; init; }
    /// <summary>Noble family.</summary>
    public required int Rich { get; init; }
    /// <summary>Work speed in percent at happiness 0.</summary>
    public required int ProductivityAtZero { get; init; }
    /// <summary>Work speed in percent at happiness 100.</summary>
    public required int ProductivityAtHundred { get; init; }
    /// <summary>Average happiness below this causes unrest.</summary>
    public required int UnrestBelow { get; init; }
}

/// <summary>Social classes.</summary>
public sealed record ClassRules
{
    /// <summary>A family with at least this share (percent) of average wealth is noble.</summary>
    public required int NobleWealthPercent { get; init; }
}

/// <summary>Economy rules (economy.json).</summary>
public sealed record EconomyRules
{
    /// <summary>Households.</summary>
    public required HouseholdRules Households { get; init; }
    /// <summary>The treasury.</summary>
    public required TreasuryRules Treasury { get; init; }
    /// <summary>Tax rates.</summary>
    public required TaxRules Taxes { get; init; }
    /// <summary>The marketplace.</summary>
    public required MarketRules Market { get; init; }
    /// <summary>Coins.</summary>
    public required MoneyRules Money { get; init; }
    /// <summary>Wages and job choice.</summary>
    public required WageRules Wages { get; init; }
    /// <summary>Foreign merchants.</summary>
    public required MerchantRules Merchants { get; init; }
    /// <summary>Guilds.</summary>
    public required GuildRules Guilds { get; init; }
    /// <summary>Happiness.</summary>
    public required HappinessRules Happiness { get; init; }
    /// <summary>Social classes.</summary>
    public required ClassRules Classes { get; init; }
}
