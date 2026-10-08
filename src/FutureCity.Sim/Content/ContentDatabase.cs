using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Content;

/// <summary>A building type with its references resolved: costs and recipes as arrays indexed by good.</summary>
public sealed class BuildingType
{
    internal BuildingType(int index, BuildingDef def, int[] cost, int[] inputs, int[] outputs, int fieldGood, Condition requires)
    {
        Index = index;
        Def = def;
        Cost = cost;
        Inputs = inputs;
        Outputs = outputs;
        FieldGood = fieldGood;
        Requires = requires;
    }

    /// <summary>Index in <see cref="ContentDatabase.Buildings"/>; building components store it.</summary>
    public int Index { get; }
    /// <summary>The definition.</summary>
    public BuildingDef Def { get; }
    /// <summary>Construction materials by good.</summary>
    public IReadOnlyList<int> Cost { get; }
    /// <summary>Recipe inputs by good (all zero if not a workshop).</summary>
    public IReadOnlyList<int> Inputs { get; }
    /// <summary>Recipe outputs by good (all zero if not a workshop).</summary>
    public IReadOnlyList<int> Outputs { get; }
    /// <summary>Good harvested from the field, or -1 if not a farm.</summary>
    public int FieldGood { get; }
    /// <summary>Condition for placing it.</summary>
    public Condition Requires { get; }
    /// <summary>Whether people can be assigned to work here.</summary>
    public bool IsWorkplace => Def.Workers > 0;
}

/// <summary>A technology with its condition parsed and activity weights indexed by <see cref="WorkKind"/>.</summary>
public sealed class TechType
{
    internal TechType(int index, TechDef def, Condition preconditions, int[] activity)
    {
        Index = index;
        Def = def;
        Preconditions = preconditions;
        Activity = activity;
    }

    /// <summary>Index in <see cref="ContentDatabase.Techs"/>.</summary>
    public int Index { get; }
    /// <summary>The definition.</summary>
    public TechDef Def { get; }
    /// <summary>When it can be discovered.</summary>
    public Condition Preconditions { get; }
    /// <summary>Progress points per 100 ticks of each kind of work.</summary>
    public IReadOnlyList<int> Activity { get; }
}

/// <summary>An institution with its condition parsed.</summary>
public sealed class InstitutionType
{
    internal InstitutionType(int index, InstitutionDef def, Condition preconditions)
    {
        Index = index;
        Def = def;
        Preconditions = preconditions;
    }

    /// <summary>Index in <see cref="ContentDatabase.Institutions"/>.</summary>
    public int Index { get; }
    /// <summary>The definition.</summary>
    public InstitutionDef Def { get; }
    /// <summary>When it can be established.</summary>
    public Condition Preconditions { get; }
}

/// <summary>An era with its condition parsed.</summary>
public sealed class EraType
{
    internal EraType(int index, EraDef def, Condition preconditions)
    {
        Index = index;
        Def = def;
        Preconditions = preconditions;
    }

    /// <summary>Index in <see cref="ContentDatabase.Eras"/>.</summary>
    public int Index { get; }
    /// <summary>The definition.</summary>
    public EraDef Def { get; }
    /// <summary>When a civilization in the previous era enters this one.</summary>
    public Condition Preconditions { get; }
}

/// <summary>A raw material source with its good and work kind resolved: a deposit kind or a terrain resource.</summary>
/// <param name="Good">Good it yields.</param>
/// <param name="TicksPerUnit">Ticks of work per unit.</param>
/// <param name="Work">Kind of work.</param>
public readonly record struct SourceInfo(int Good, int TicksPerUnit, WorkKind Work);

/// <summary>
/// All loaded, validated game content. Immutable after loading.
/// </summary>
public sealed class ContentDatabase
{
    private readonly Dictionary<string, int> _terrainIndex;
    private readonly Dictionary<string, int> _goodIndex;
    private readonly int[] _plantGood;
    private readonly int[] _animalGood;
    private readonly SourceInfo[] _deposits;
    private readonly SourceInfo?[] _terrainResource;
    private readonly int[] _depletedTerrain;

    internal ContentDatabase(ContentParts parts, string hash)
    {
        Rules = parts.Rules;
        Terrains = parts.Terrains;
        Citizens = parts.Citizens;
        Plants = parts.Nature.Plants;
        Animals = parts.Nature.Animals;
        Deposits = parts.Nature.Deposits;
        Goods = parts.Goods.Goods;
        Tools = parts.Goods.Tools;
        Progress = parts.Progress;
        Hash = hash;

        _terrainIndex = Index(Terrains, t => t.Id);
        _goodIndex = Index(Goods, g => g.Id);
        _plantGood = Plants.Select(p => GoodIndex(p.Good)).ToArray();
        _animalGood = Animals.Select(a => GoodIndex(a.Good)).ToArray();
        _deposits = Deposits.Select(d => new SourceInfo(GoodIndex(d.Good), d.TicksPerUnit, ParseWork(d.Work))).ToArray();
        _terrainResource = Terrains
            .Select(t => t.Resource is { } r ? new SourceInfo(GoodIndex(r.Good), r.TicksPerUnit, ParseWork(r.Work)) : (SourceInfo?)null)
            .ToArray();
        _depletedTerrain = Terrains.Select(t => t.Resource is { } r ? TerrainIndex(r.DepletedTerrain) : -1).ToArray();
        ToolGood = GoodIndex(Tools.Good);
        FoodGoods = Enumerable.Range(0, Goods.Count)
            .Where(g => Goods[g].Nutrition > 0)
            .OrderByDescending(g => Goods[g].Nutrition).ThenBy(g => g)
            .ToArray();

        var buildingIndex = Index(parts.Buildings, b => b.Id);
        var techIndex = Index(Progress.Techs, t => t.Id);
        var institutionIndex = Index(Progress.Institutions, i => i.Id);
        _buildingIndex = buildingIndex;
        _techIndex = techIndex;
        _institutionIndex = institutionIndex;

        Buildings = parts.Buildings.Select((b, i) => new BuildingType(i, b, GoodArray(b.Cost),
            GoodArray(b.Recipe?.Inputs), GoodArray(b.Recipe?.Outputs), b.Field is { } f ? GoodIndex(f.Good) : -1,
            ParseCondition(b.Requires))).ToArray();
        Techs = Progress.Techs.Select((t, i) => new TechType(i, t, ParseCondition(t.Preconditions),
            Enum.GetValues<WorkKind>().Select(k => t.Activity.TryGetValue(WorkName(k), out int w) ? w : 0).ToArray())).ToArray();
        Institutions = Progress.Institutions.Select((x, i) => new InstitutionType(i, x, ParseCondition(x.Preconditions))).ToArray();
        Eras = Progress.Eras.Select((e, i) => new EraType(i, e, ParseCondition(e.Preconditions))).ToArray();
    }

    private readonly Dictionary<string, int> _buildingIndex;
    private readonly Dictionary<string, int> _techIndex;
    private readonly Dictionary<string, int> _institutionIndex;

    /// <summary>Global setup rules.</summary>
    public GameRules Rules { get; }

    /// <summary>The calendar.</summary>
    public CalendarRules Calendar => Rules.Calendar;

    /// <summary>Terrain types in file order. A terrain's index in this list is what maps store.</summary>
    public IReadOnlyList<TerrainDef> Terrains { get; }

    /// <summary>Citizen ageing, needs and growth rules.</summary>
    public CitizenRules Citizens { get; }

    /// <summary>Wild food plants in file order. Plant components store the index.</summary>
    public IReadOnlyList<PlantDef> Plants { get; }

    /// <summary>Game animals in file order. Animal and carcass components store the index.</summary>
    public IReadOnlyList<AnimalDef> Animals { get; }

    /// <summary>Raw material deposits in file order. Deposit components store the index.</summary>
    public IReadOnlyList<DepositDef> Deposits { get; }

    /// <summary>Goods in file order. Inventories are arrays indexed by good.</summary>
    public IReadOnlyList<GoodDef> Goods { get; }

    /// <summary>Food goods, best nutrition first (the order people eat them in).</summary>
    public IReadOnlyList<int> FoodGoods { get; }

    /// <summary>How tools work.</summary>
    public ToolRules Tools { get; }

    /// <summary>Index of the tool good.</summary>
    public int ToolGood { get; }

    /// <summary>Building types in file order. Building components store the index.</summary>
    public IReadOnlyList<BuildingType> Buildings { get; }

    /// <summary>Emergence settings.</summary>
    public ProgressRules Progress { get; }

    /// <summary>Technologies in file order.</summary>
    public IReadOnlyList<TechType> Techs { get; }

    /// <summary>Institutions in file order.</summary>
    public IReadOnlyList<InstitutionType> Institutions { get; }

    /// <summary>Eras in order.</summary>
    public IReadOnlyList<EraType> Eras { get; }

    /// <summary>SHA-256 of all content files. Saves record it so a save is never loaded against different content.</summary>
    public string Hash { get; }

    /// <summary>Returns the index of the terrain with the given id.</summary>
    public int TerrainIndex(string id) =>
        _terrainIndex.TryGetValue(id, out int index) ? index : throw new KeyNotFoundException($"Unknown terrain '{id}'.");

    /// <summary>Returns the map size with the given id.</summary>
    public MapSizeDef MapSize(string id) =>
        Rules.MapSizes.FirstOrDefault(m => m.Id == id) ?? throw new KeyNotFoundException($"Unknown map size '{id}'.");

    /// <summary>Returns the index of the plant with the given id.</summary>
    public int PlantIndex(string id) => IndexOf(Plants, p => p.Id == id, "plant", id);

    /// <summary>Returns the index of the animal with the given id.</summary>
    public int AnimalIndex(string id) => IndexOf(Animals, a => a.Id == id, "animal", id);

    /// <summary>Returns the index of the deposit kind with the given id.</summary>
    public int DepositIndex(string id) => IndexOf(Deposits, d => d.Id == id, "deposit", id);

    /// <summary>Returns the index of the good with the given id.</summary>
    public int GoodIndex(string id) =>
        _goodIndex.TryGetValue(id, out int index) ? index : throw new KeyNotFoundException($"Unknown good '{id}'.");

    /// <summary>Returns the index of the building type with the given id, or -1.</summary>
    public int BuildingIndex(string id) => _buildingIndex.GetValueOrDefault(id, -1);

    /// <summary>Returns the index of the technology with the given id, or -1.</summary>
    public int TechIndex(string id) => _techIndex.GetValueOrDefault(id, -1);

    /// <summary>Returns the index of the institution with the given id, or -1.</summary>
    public int InstitutionIndex(string id) => _institutionIndex.GetValueOrDefault(id, -1);

    /// <summary>Good yielded by a plant kind.</summary>
    public int PlantGood(int kind) => _plantGood[kind];

    /// <summary>Good yielded by a carcass of an animal kind.</summary>
    public int AnimalGood(int kind) => _animalGood[kind];

    /// <summary>What a deposit kind yields.</summary>
    public SourceInfo DepositSource(int kind) => _deposits[kind];

    /// <summary>What tiles of a terrain yield, if anything.</summary>
    public SourceInfo? TerrainSource(int terrain) => _terrainResource[terrain];

    /// <summary>The terrain a tile becomes when its resource runs out, or -1.</summary>
    public int DepletedTerrain(int terrain) => _depletedTerrain[terrain];

    /// <summary>Food value of one unit of a good, in percent of a meal.</summary>
    public int Nutrition(int good) => Goods[good].Nutrition;

    /// <summary>Content name of a work kind.</summary>
    public static string WorkName(WorkKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Resolves a fact name used in conditions, or returns null if it is unknown.</summary>
    public FactRef? ResolveFact(string name) =>
        ResolveFact(name, _goodIndex, _buildingIndex, _techIndex, _institutionIndex);

    internal static FactRef? ResolveFact(string name, IReadOnlyDictionary<string, int> goods,
        IReadOnlyDictionary<string, int> buildings, IReadOnlyDictionary<string, int> techs,
        IReadOnlyDictionary<string, int> institutions)
    {
        switch (name)
        {
            case "population": return new FactRef(FactKind.Population, 0);
            case "adults": return new FactRef(FactKind.Adults, 0);
            case "children": return new FactRef(FactKind.Children, 0);
            case "food": return new FactRef(FactKind.Food, 0);
            case "shelter": return new FactRef(FactKind.Shelter, 0);
            case "era": return new FactRef(FactKind.Era, 0);
        }
        int dot = name.IndexOf('.');
        if (dot < 0) return null;
        string prefix = name[..dot], id = name[(dot + 1)..];
        (FactKind kind, int index) = prefix switch
        {
            "store" => (FactKind.Store, goods.GetValueOrDefault(id, -1)),
            "gathered" => (FactKind.Gathered, goods.GetValueOrDefault(id, -1)),
            "produced" => (FactKind.Produced, goods.GetValueOrDefault(id, -1)),
            "building" => (FactKind.Building, buildings.GetValueOrDefault(id, -1)),
            "tech" => (FactKind.Tech, techs.GetValueOrDefault(id, -1)),
            "institution" => (FactKind.Institution, institutions.GetValueOrDefault(id, -1)),
            _ => (FactKind.Population, -1),
        };
        return index < 0 ? null : new FactRef(kind, index);
    }

    /// <summary>A readable label for a fact, for "what is missing" lists.</summary>
    public string FactLabel(FactRef fact) => fact.Kind switch
    {
        FactKind.Population => "People",
        FactKind.Adults => "People of working age",
        FactKind.Children => "Children",
        FactKind.Food => "Meals in store",
        FactKind.Shelter => "Shelter",
        FactKind.Era => "Era",
        FactKind.Store => $"{Goods[fact.Index].Name} in store",
        FactKind.Gathered => $"{Goods[fact.Index].Name} gathered",
        FactKind.Produced => $"{Goods[fact.Index].Name} produced",
        FactKind.Building => $"{Buildings[fact.Index].Def.Name} built",
        FactKind.Tech => Techs[fact.Index].Def.Name,
        FactKind.Institution => Institutions[fact.Index].Def.Name,
        _ => fact.Kind.ToString(),
    };

    // The loader validates every condition first, so parsing here cannot fail.
    private Condition ParseCondition(string text) => Condition.Parse(text, ResolveFact);

    private static WorkKind ParseWork(string name) => Enum.Parse<WorkKind>(name, ignoreCase: true);

    private int[] GoodArray(IReadOnlyDictionary<string, int>? amounts)
    {
        var array = new int[Goods.Count];
        if (amounts != null)
        {
            foreach (var (id, amount) in amounts)
                array[GoodIndex(id)] = amount;
        }
        return array;
    }

    internal static Dictionary<string, int> Index<T>(IReadOnlyList<T> list, Func<T, string> id)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < list.Count; i++)
            index[id(list[i])] = i;
        return index;
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, Func<T, bool> match, string kind, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (match(list[i])) return i;
        }
        throw new KeyNotFoundException($"Unknown {kind} '{id}'.");
    }
}

/// <summary>The parsed content files, handed from the loader to the database.</summary>
internal sealed record ContentParts(
    GameRules Rules,
    IReadOnlyList<TerrainDef> Terrains,
    CitizenRules Citizens,
    NatureFile Nature,
    GoodsFile Goods,
    IReadOnlyList<BuildingDef> Buildings,
    ProgressRules Progress);
