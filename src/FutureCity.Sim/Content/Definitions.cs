namespace FutureCity.Sim.Content;

/// <summary>A map size option offered at game setup.</summary>
public sealed record MapSizeDef
{
    /// <summary>Unique id, e.g. "small".</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Width in tiles.</summary>
    public required int Width { get; init; }
    /// <summary>Height in tiles.</summary>
    public required int Height { get; init; }
}

/// <summary>Parameters for the seeded terrain generator.</summary>
public sealed record MapGenerationRules
{
    /// <summary>Terrain id used for lakes.</summary>
    public required string WaterTerrain { get; init; }
    /// <summary>Terrain id used for woods.</summary>
    public required string ForestTerrain { get; init; }
    /// <summary>Share of the map covered by water, in percent.</summary>
    public required int WaterPercent { get; init; }
    /// <summary>Share of the land covered by forest, in percent.</summary>
    public required int ForestPercent { get; init; }
    /// <summary>Typical size of lakes and woods in tiles (noise cell size).</summary>
    public required int FeatureSize { get; init; }
    /// <summary>Radius of open grassland around the starting camp.</summary>
    public required int StartClearRadius { get; init; }
}

/// <summary>Global game-setup rules (rules.json).</summary>
public sealed record GameRules
{
    /// <summary>Map sizes available at setup.</summary>
    public required IReadOnlyList<MapSizeDef> MapSizes { get; init; }
    /// <summary>Map size used when none is chosen.</summary>
    public required string DefaultMapSize { get; init; }
    /// <summary>Terrain that fills a new map before generation adds features.</summary>
    public required string DefaultTerrain { get; init; }
    /// <summary>Terrain generator parameters.</summary>
    public required MapGenerationRules MapGeneration { get; init; }
}

/// <summary>A terrain type (terrain.json).</summary>
public sealed record TerrainDef
{
    /// <summary>Unique id, e.g. "grass".</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Whether units can walk on it.</summary>
    public required bool Walkable { get; init; }
    /// <summary>Whether buildings can be placed on it.</summary>
    public required bool Buildable { get; init; }
    /// <summary>Walking cost in percent of open grassland.</summary>
    public required int MoveCost { get; init; }
}

/// <summary>How a new band starts.</summary>
public sealed record StartRules
{
    /// <summary>Number of starting citizens.</summary>
    public required int Citizens { get; init; }
    /// <summary>Food in the camp store at the start.</summary>
    public required int Food { get; init; }
    /// <summary>Youngest starting age in years.</summary>
    public required int MinAgeYears { get; init; }
    /// <summary>Oldest starting age in years.</summary>
    public required int MaxAgeYears { get; init; }
}

/// <summary>The band's camp.</summary>
public sealed record CampRules
{
    /// <summary>Number of people the camp can shelter.</summary>
    public required int Shelter { get; init; }
}

/// <summary>When children are born.</summary>
public sealed record GrowthRules
{
    /// <summary>How often each band checks for a birth.</summary>
    public required int CheckIntervalTicks { get; init; }
    /// <summary>Food the store must hold per person (on top of the birth cost) for a birth to happen.</summary>
    public required int FoodPerCapita { get; init; }
    /// <summary>Food taken from the store by a birth.</summary>
    public required int BirthFoodCost { get; init; }
    /// <summary>Chance of a birth per check when the conditions are met.</summary>
    public required int BirthChancePercent { get; init; }
    /// <summary>Maximum population per civilization.</summary>
    public required int PopulationCap { get; init; }
}

/// <summary>Citizen ageing, needs and work rates (citizens.json).</summary>
public sealed record CitizenRules
{
    /// <summary>Ticks in one game year.</summary>
    public required int TicksPerYear { get; init; }
    /// <summary>Age from which a citizen takes orders.</summary>
    public required int AdultAgeYears { get; init; }
    /// <summary>Age from which natural death becomes possible.</summary>
    public required int OldAgeYears { get; init; }
    /// <summary>Yearly death chance added per year past <see cref="OldAgeYears"/>, in percentage points.</summary>
    public required int OldAgeDeathPercentPerYear { get; init; }
    /// <summary>Full health.</summary>
    public required int MaxHealth { get; init; }
    /// <summary>Health regained per tick while not starving.</summary>
    public required int HealthRegenPerTick { get; init; }
    /// <summary>Hunger at which a citizen is starving.</summary>
    public required int MaxHunger { get; init; }
    /// <summary>Hunger gained per tick.</summary>
    public required int HungerPerTick { get; init; }
    /// <summary>Hunger at which a citizen eats.</summary>
    public required int EatAtHunger { get; init; }
    /// <summary>Food eaten per meal.</summary>
    public required int FoodPerMeal { get; init; }
    /// <summary>Hunger removed by a full meal.</summary>
    public required int HungerPerMeal { get; init; }
    /// <summary>Health lost per tick while starving.</summary>
    public required int StarvationDamagePerTick { get; init; }
    /// <summary>Ticks to walk one tile of open grassland.</summary>
    public required int MoveTicksPerTile { get; init; }
    /// <summary>Food a citizen can carry.</summary>
    public required int CarryCapacity { get; init; }
    /// <summary>Starting conditions.</summary>
    public required StartRules Start { get; init; }
    /// <summary>Camp rules.</summary>
    public required CampRules Camp { get; init; }
    /// <summary>Population growth rules.</summary>
    public required GrowthRules Growth { get; init; }
}

/// <summary>A wild food plant (nature.json).</summary>
public sealed record PlantDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Food on a fully grown plant.</summary>
    public required int MaxFood { get; init; }
    /// <summary>Ticks to gather one food.</summary>
    public required int TicksPerFood { get; init; }
    /// <summary>Ticks between regrowth steps.</summary>
    public required int RegrowIntervalTicks { get; init; }
    /// <summary>Food added per regrowth step.</summary>
    public required int RegrowAmount { get; init; }
    /// <summary>Ticks a stripped plant needs before it regrows.</summary>
    public required int DormantTicks { get; init; }
    /// <summary>Clusters placed per 10,000 map tiles.</summary>
    public required int ClustersPer10kTiles { get; init; }
    /// <summary>Smallest cluster.</summary>
    public required int MinClusterSize { get; init; }
    /// <summary>Largest cluster.</summary>
    public required int MaxClusterSize { get; init; }
    /// <summary>Clusters guaranteed near the starting camp.</summary>
    public required int StartClusters { get; init; }
}

/// <summary>A game animal (nature.json).</summary>
public sealed record AnimalDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Meat from one carcass.</summary>
    public required int Food { get; init; }
    /// <summary>Ticks for a hunter in reach to kill it.</summary>
    public required int KillTicks { get; init; }
    /// <summary>Ticks to butcher one food from the carcass.</summary>
    public required int ButcherTicksPerFood { get; init; }
    /// <summary>A carcass loses one food every this many ticks.</summary>
    public required int CarcassDecayIntervalTicks { get; init; }
    /// <summary>Ticks to walk one tile of open grassland.</summary>
    public required int MoveTicksPerTile { get; init; }
    /// <summary>How far from its home an animal wanders.</summary>
    public required int WanderRadius { get; init; }
    /// <summary>Chance per tick, in percent, that a resting animal starts wandering.</summary>
    public required int WanderChancePerTick { get; init; }
    /// <summary>Ticks between breeding attempts of one animal.</summary>
    public required int BreedIntervalTicks { get; init; }
    /// <summary>Chance of a birth per attempt below capacity.</summary>
    public required int BreedChancePercent { get; init; }
    /// <summary>Animals of the kind within <see cref="CapacityRadius"/> at which breeding stops.</summary>
    public required int LocalCapacity { get; init; }
    /// <summary>Radius in tiles used for <see cref="LocalCapacity"/>.</summary>
    public required int CapacityRadius { get; init; }
    /// <summary>Herds placed per 10,000 map tiles.</summary>
    public required int HerdsPer10kTiles { get; init; }
    /// <summary>Smallest herd.</summary>
    public required int MinHerdSize { get; init; }
    /// <summary>Largest herd.</summary>
    public required int MaxHerdSize { get; init; }
    /// <summary>Herds guaranteed within reach of the starting camp.</summary>
    public required int StartHerds { get; init; }
}

/// <summary>File shape of terrain.json.</summary>
internal sealed record TerrainFile
{
    public required IReadOnlyList<TerrainDef> Terrains { get; init; }
}

/// <summary>File shape of nature.json.</summary>
internal sealed record NatureFile
{
    public required IReadOnlyList<PlantDef> Plants { get; init; }
    public required IReadOnlyList<AnimalDef> Animals { get; init; }
}
