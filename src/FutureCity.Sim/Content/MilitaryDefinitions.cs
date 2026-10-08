namespace FutureCity.Sim.Content;

/// <summary>A kind of soldier (military.json).</summary>
public sealed record UnitDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Condition that must hold to recruit it; empty for always.</summary>
    public required string Requires { get; init; }
    /// <summary>Goods taken from the public stores to arm one soldier, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Equipment { get; init; }
    /// <summary>Health on the citizens' scale.</summary>
    public required int Health { get; init; }
    /// <summary>Damage per hit before bonuses and armour.</summary>
    public required int Attack { get; init; }
    /// <summary>Damage taken off every hit received.</summary>
    public required int Armour { get; init; }
    /// <summary>Reach in tiles (1 = next to the target).</summary>
    public required int Range { get; init; }
    /// <summary>Ticks between hits.</summary>
    public required int AttackTicks { get; init; }
    /// <summary>How far an idle soldier notices enemies, in tiles.</summary>
    public required int SightRadius { get; init; }
    /// <summary>Ticks to walk one tile of open grassland.</summary>
    public required int TicksPerTile { get; init; }
    /// <summary>Starting morale, 0-100.</summary>
    public required int Morale { get; init; }
    /// <summary>Place in a formation: "front", "back" or "siege" (siege units attack only buildings).</summary>
    public required string Role { get; init; }
    /// <summary>Damage in percent against a unit id or "building" (100 = normal).</summary>
    public required IReadOnlyDictionary<string, int> Bonuses { get; init; }
    /// <summary>Codex text: what it was and why it appeared.</summary>
    public required string Codex { get; init; }
}

/// <summary>How a kind of service changes a soldier.</summary>
public sealed record ServiceDef
{
    /// <summary>Added to the unit's starting morale.</summary>
    public required int MoraleBonus { get; init; }
    /// <summary>Attack in percent of the unit's.</summary>
    public required int AttackPercent { get; init; }
    /// <summary>Wage per 100 ticks of service in percent of the public wage (paid service only).</summary>
    public int WagePercent { get; init; }
}

/// <summary>Levies and paid soldiers.</summary>
public sealed record ServiceRules
{
    /// <summary>Called up from the families, unpaid.</summary>
    public required ServiceDef Levy { get; init; }
    /// <summary>Serving for a wage (needs coins).</summary>
    public required ServiceDef Paid { get; init; }
}

/// <summary>Combat, morale and the costs of war.</summary>
public sealed record CombatRules
{
    /// <summary>Least damage a hit does.</summary>
    public required int MinDamage { get; init; }
    /// <summary>Damage against buildings in percent, unless a unit has a "building" bonus.</summary>
    public required int BuildingDamagePercent { get; init; }
    /// <summary>How often idle soldiers and towers look for enemies.</summary>
    public required int TargetIntervalTicks { get; init; }
    /// <summary>Morale lost per 100 damage taken.</summary>
    public required int MoraleLossPer100Damage { get; init; }
    /// <summary>Morale lost when a soldier of the same side dies nearby.</summary>
    public required int AllyDeathMoraleLoss { get; init; }
    /// <summary>How near (tiles) a death must be to shake morale.</summary>
    public required int AllyDeathRadius { get; init; }
    /// <summary>Below this morale a soldier flees home.</summary>
    public required int RoutBelow { get; init; }
    /// <summary>How long a routed soldier flees.</summary>
    public required int RoutTicks { get; init; }
    /// <summary>Morale regained per job check out of combat.</summary>
    public required int MoraleRecoveryPerCheck { get; init; }
    /// <summary>Morale lost per job check while hungry.</summary>
    public required int HungryMoraleLoss { get; init; }
    /// <summary>Morale lost per job check a paid soldier went unpaid.</summary>
    public required int UnpaidMoraleLoss { get; init; }
    /// <summary>Unpaid checks in a row after which a paid soldier deserts.</summary>
    public required int DesertAfterUnpaidChecks { get; init; }
    /// <summary>Share of a destroyed building's goods left in its ruins, in percent.</summary>
    public required int LootSharePercent { get; init; }
    /// <summary>Ticks before ruins and spilled cargo disappear.</summary>
    public required int LootDecayTicks { get; init; }
    /// <summary>No market day and no merchants while enemy soldiers are this close to the marketplace.</summary>
    public required int MarketSafetyRadius { get; init; }
    /// <summary>A soldier stopped by walls attacks enemy walls and gates within this many tiles.</summary>
    public required int BreachRadius { get; init; }
}

/// <summary>File shape of military.json.</summary>
public sealed record MilitaryRules
{
    /// <summary>Kinds of soldiers.</summary>
    public required IReadOnlyList<UnitDef> Units { get; init; }
    /// <summary>Levies and paid soldiers.</summary>
    public required ServiceRules Service { get; init; }
    /// <summary>Combat and morale.</summary>
    public required CombatRules Combat { get; init; }
}

/// <summary>How a tower shoots.</summary>
public sealed record DefenceDef
{
    /// <summary>Damage per hit before armour.</summary>
    public required int Attack { get; init; }
    /// <summary>Reach in tiles.</summary>
    public required int Range { get; init; }
    /// <summary>Ticks between shots.</summary>
    public required int AttackTicks { get; init; }
}

/// <summary>Formation role of a unit.</summary>
public enum UnitRole
{
    /// <summary>Melee troops that lead a formation.</summary>
    Front,
    /// <summary>Ranged troops behind the front.</summary>
    Back,
    /// <summary>Siege engines at the back; they attack only buildings.</summary>
    Siege,
}

/// <summary>A unit kind with its references resolved.</summary>
public sealed class UnitType
{
    internal UnitType(int index, UnitDef def, int[] equipment, Emergence.Condition requires, UnitRole role, int[] bonusVsUnit,
        int buildingBonus)
    {
        Index = index;
        Def = def;
        Equipment = equipment;
        Requires = requires;
        Role = role;
        BonusVsUnit = bonusVsUnit;
        BuildingBonus = buildingBonus;
    }

    /// <summary>Index in <see cref="ContentDatabase.Units"/>; soldier components store it.</summary>
    public int Index { get; }
    /// <summary>The definition.</summary>
    public UnitDef Def { get; }
    /// <summary>Equipment by good.</summary>
    public IReadOnlyList<int> Equipment { get; }
    /// <summary>Condition for recruiting it.</summary>
    public Emergence.Condition Requires { get; }
    /// <summary>Formation role.</summary>
    public UnitRole Role { get; }
    /// <summary>Damage in percent against each unit kind.</summary>
    public IReadOnlyList<int> BonusVsUnit { get; }
    /// <summary>Damage in percent against buildings.</summary>
    public int BuildingBonus { get; }
}
