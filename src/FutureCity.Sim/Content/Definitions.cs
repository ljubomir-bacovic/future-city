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

/// <summary>How soil fertility is spread over the map, in percent.</summary>
public sealed record FertilityRules
{
    /// <summary>Lowest fertility from noise.</summary>
    public required int Min { get; init; }
    /// <summary>Highest fertility from noise.</summary>
    public required int Max { get; init; }
    /// <summary>Extra fertility next to water, fading out over <see cref="WaterRadius"/>.</summary>
    public required int WaterBonus { get; init; }
    /// <summary>Distance in tiles over which water improves the soil.</summary>
    public required int WaterRadius { get; init; }
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
    /// <summary>Soil fertility generation.</summary>
    public required FertilityRules Fertility { get; init; }
}

/// <summary>A season of the year.</summary>
public sealed record SeasonDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Crop growth speed in percent.</summary>
    public required int CropGrowthPercent { get; init; }
    /// <summary>Wild plant regrowth speed in percent.</summary>
    public required int PlantRegrowPercent { get; init; }
    /// <summary>Whether fields can be sown.</summary>
    public required bool Sowing { get; init; }
    /// <summary>Whether ripe crops still in the field are lost.</summary>
    public required bool CropsRot { get; init; }
}

/// <summary>The calendar: year length and seasons.</summary>
public sealed record CalendarRules
{
    /// <summary>Ticks in one game year. Must be divisible by the number of seasons.</summary>
    public required int TicksPerYear { get; init; }
    /// <summary>Seasons in order; the game starts in the first.</summary>
    public required IReadOnlyList<SeasonDef> Seasons { get; init; }
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
    /// <summary>Year length and seasons.</summary>
    public required CalendarRules Calendar { get; init; }
}

/// <summary>A good that can be taken from a terrain tile (e.g. wood from forest).</summary>
public sealed record TerrainResource
{
    /// <summary>Id of the good.</summary>
    public required string Good { get; init; }
    /// <summary>Amount on a fresh tile.</summary>
    public required int Amount { get; init; }
    /// <summary>Ticks of work per unit.</summary>
    public required int TicksPerUnit { get; init; }
    /// <summary>Kind of work (a <see cref="WorkKind"/> name).</summary>
    public required string Work { get; init; }
    /// <summary>Terrain the tile becomes when the resource runs out.</summary>
    public required string DepletedTerrain { get; init; }
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
    /// <summary>A good that can be taken from tiles of this terrain, if any.</summary>
    public TerrainResource? Resource { get; init; }
}

/// <summary>A good (goods.json).</summary>
public sealed record GoodDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Food value per unit in percent of a ration; 0 if not food.</summary>
    public required int Nutrition { get; init; }
    /// <summary>Stock the band aims for per person; drives automatic job assignment.</summary>
    public required int TargetPerCapita { get; init; }
}

/// <summary>How tools work.</summary>
public sealed record ToolRules
{
    /// <summary>Id of the tool good.</summary>
    public required string Good { get; init; }
    /// <summary>Ticks of work before a tool is worn out.</summary>
    public required int Durability { get; init; }
    /// <summary>Extra work speed with a tool, in percent.</summary>
    public required int SpeedBonusPercent { get; init; }
}

/// <summary>How a new band starts.</summary>
public sealed record StartRules
{
    /// <summary>Number of starting citizens.</summary>
    public required int Citizens { get; init; }
    /// <summary>Goods in the camp store at the start, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Goods { get; init; }
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
    /// <summary>Meals the stores must hold per person (on top of the birth cost) for a birth to happen.</summary>
    public required int FoodPerCapita { get; init; }
    /// <summary>Meals taken from the stores by a birth.</summary>
    public required int BirthFoodCost { get; init; }
    /// <summary>Chance of a birth per check when the conditions are met.</summary>
    public required int BirthChancePercent { get; init; }
    /// <summary>Maximum population per civilization.</summary>
    public required int PopulationCap { get; init; }
}

/// <summary>Automatic job assignment.</summary>
public sealed record JobRules
{
    /// <summary>How often idle people look for work.</summary>
    public required int CheckIntervalTicks { get; init; }
    /// <summary>Meals in store per person the band aims for.</summary>
    public required int FoodTargetPerCapita { get; init; }
    /// <summary>Demand score of helping at a construction site.</summary>
    public required int BuildPriority { get; init; }
    /// <summary>Demand score of research while something can be discovered.</summary>
    public required int ResearchPriority { get; init; }
    /// <summary>Most builders sent to one site automatically.</summary>
    public required int MaxBuildersPerSite { get; init; }
    /// <summary>How far from camp, in tiles, automatic gatherers look for sources.</summary>
    public required int GatherRadius { get; init; }
}

/// <summary>Citizen ageing, needs and work rates (citizens.json).</summary>
public sealed record CitizenRules
{
    /// <summary>Age from which a citizen can work and take orders.</summary>
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
    /// <summary>Meals (rations) eaten per sitting.</summary>
    public required int FoodPerMeal { get; init; }
    /// <summary>Hunger removed by a full meal.</summary>
    public required int HungerPerMeal { get; init; }
    /// <summary>Health lost per tick while starving.</summary>
    public required int StarvationDamagePerTick { get; init; }
    /// <summary>Ticks to walk one tile of open grassland.</summary>
    public required int MoveTicksPerTile { get; init; }
    /// <summary>Units of a good a citizen can carry.</summary>
    public required int CarryCapacity { get; init; }
    /// <summary>Starting conditions.</summary>
    public required StartRules Start { get; init; }
    /// <summary>Camp rules.</summary>
    public required CampRules Camp { get; init; }
    /// <summary>Population growth rules.</summary>
    public required GrowthRules Growth { get; init; }
    /// <summary>Automatic job assignment rules.</summary>
    public required JobRules Jobs { get; init; }
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
    /// <summary>Id of the good it yields.</summary>
    public required string Good { get; init; }
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
    /// <summary>Id of the good a carcass yields.</summary>
    public required string Good { get; init; }
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

/// <summary>A finite raw material deposit such as a stone outcrop (nature.json).</summary>
public sealed record DepositDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Id of the good it yields.</summary>
    public required string Good { get; init; }
    /// <summary>Units in a fresh deposit.</summary>
    public required int Amount { get; init; }
    /// <summary>Ticks of work per unit.</summary>
    public required int TicksPerUnit { get; init; }
    /// <summary>Kind of work (a <see cref="WorkKind"/> name).</summary>
    public required string Work { get; init; }
    /// <summary>Clusters placed per 10,000 map tiles.</summary>
    public required int ClustersPer10kTiles { get; init; }
    /// <summary>Smallest cluster.</summary>
    public required int MinClusterSize { get; init; }
    /// <summary>Largest cluster.</summary>
    public required int MaxClusterSize { get; init; }
    /// <summary>Clusters guaranteed near the starting camp.</summary>
    public required int StartClusters { get; init; }
    /// <summary>Whether deposits are placed next to water.</summary>
    public required bool NearWater { get; init; }
}

/// <summary>A production recipe of a workshop.</summary>
public sealed record RecipeDef
{
    /// <summary>Goods used per batch, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Inputs { get; init; }
    /// <summary>Goods made per batch, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Outputs { get; init; }
    /// <summary>Ticks of work per batch without tools.</summary>
    public required int WorkTicks { get; init; }
}

/// <summary>How a farm field grows crops.</summary>
public sealed record FieldDef
{
    /// <summary>Id of the harvested good.</summary>
    public required string Good { get; init; }
    /// <summary>Ticks of work to sow the field.</summary>
    public required int SowWork { get; init; }
    /// <summary>Ticks of full-speed growth until ripe.</summary>
    public required int GrowTicks { get; init; }
    /// <summary>Harvest from a ripe field on perfect soil.</summary>
    public required int Yield { get; init; }
    /// <summary>Ticks of work to harvest one unit.</summary>
    public required int HarvestTicksPerUnit { get; init; }
    /// <summary>Fertility points lost per harvest.</summary>
    public required int FertilityPerHarvest { get; init; }
    /// <summary>A fallow field regains one fertility point this often.</summary>
    public required int FertilityRecoveryIntervalTicks { get; init; }
}

/// <summary>A building type (buildings.json).</summary>
public sealed record BuildingDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Placeholder color as "#rrggbb".</summary>
    public required string Color { get; init; }
    /// <summary>Footprint side length in tiles.</summary>
    public required int Size { get; init; }
    /// <summary>Materials builders must deliver, by good id.</summary>
    public required IReadOnlyDictionary<string, int> Cost { get; init; }
    /// <summary>Ticks of work by one builder without tools.</summary>
    public required int BuildWork { get; init; }
    /// <summary>Condition that must hold to place it; empty for always.</summary>
    public required string Requires { get; init; }
    /// <summary>People it houses.</summary>
    public int Shelter { get; init; }
    /// <summary>Whether it is a store where goods are dropped off and taken.</summary>
    public bool Storage { get; init; }
    /// <summary>Most workers it employs.</summary>
    public int Workers { get; init; }
    /// <summary>Research points per worker per tick.</summary>
    public int Research { get; init; }
    /// <summary>Production recipe, for workshops.</summary>
    public RecipeDef? Recipe { get; init; }
    /// <summary>Crop growing, for farms.</summary>
    public FieldDef? Field { get; init; }
}

/// <summary>A technology (progress.json).</summary>
public sealed record TechDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Condition under which it can be discovered.</summary>
    public required string Preconditions { get; init; }
    /// <summary>Progress at which it is discovered for certain.</summary>
    public required int Cost { get; init; }
    /// <summary>Progress points per 100 ticks of each kind of work, by <see cref="WorkKind"/> name.</summary>
    public required IReadOnlyDictionary<string, int> Activity { get; init; }
    /// <summary>Codex text: why it emerged.</summary>
    public required string Codex { get; init; }
}

/// <summary>An institution (progress.json).</summary>
public sealed record InstitutionDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Condition under which the player can establish it.</summary>
    public required string Preconditions { get; init; }
    /// <summary>Meals spent when it is established.</summary>
    public required int FoodCost { get; init; }
    /// <summary>Whether idle citizens then take up work automatically, by demand.</summary>
    public bool AutoJobs { get; init; }
    /// <summary>Codex text: why it emerged.</summary>
    public required string Codex { get; init; }
}

/// <summary>An era (progress.json).</summary>
public sealed record EraDef
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Name { get; init; }
    /// <summary>Condition under which a civilization enters this era from the previous one.</summary>
    public required string Preconditions { get; init; }
    /// <summary>Codex text.</summary>
    public required string Codex { get; init; }
}

/// <summary>Emergence settings and the techs, institutions and eras (progress.json).</summary>
public sealed record ProgressRules
{
    /// <summary>How often preconditions are evaluated.</summary>
    public required int EvaluationIntervalTicks { get; init; }
    /// <summary>Chance scale of an early discovery: luckPercent x progress / cost per evaluation.</summary>
    public required int LuckPercent { get; init; }
    /// <summary>Technologies.</summary>
    public required IReadOnlyList<TechDef> Techs { get; init; }
    /// <summary>Institutions.</summary>
    public required IReadOnlyList<InstitutionDef> Institutions { get; init; }
    /// <summary>Eras in order.</summary>
    public required IReadOnlyList<EraDef> Eras { get; init; }
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
    public required IReadOnlyList<DepositDef> Deposits { get; init; }
}

/// <summary>File shape of goods.json.</summary>
internal sealed record GoodsFile
{
    public required IReadOnlyList<GoodDef> Goods { get; init; }
    public required ToolRules Tools { get; init; }
}

/// <summary>File shape of buildings.json.</summary>
internal sealed record BuildingsFile
{
    public required IReadOnlyList<BuildingDef> Buildings { get; init; }
}
