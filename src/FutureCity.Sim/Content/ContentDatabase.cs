namespace FutureCity.Sim.Content;

/// <summary>
/// All loaded, validated game content. Immutable after loading.
/// </summary>
public sealed class ContentDatabase
{
    private readonly Dictionary<string, int> _terrainIndex;

    internal ContentDatabase(GameRules rules, IReadOnlyList<TerrainDef> terrains, CitizenRules citizens,
        IReadOnlyList<PlantDef> plants, IReadOnlyList<AnimalDef> animals, string hash)
    {
        Rules = rules;
        Terrains = terrains;
        Citizens = citizens;
        Plants = plants;
        Animals = animals;
        Hash = hash;
        _terrainIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < terrains.Count; i++)
            _terrainIndex[terrains[i].Id] = i;
    }

    /// <summary>Global setup rules.</summary>
    public GameRules Rules { get; }

    /// <summary>Terrain types in file order. A terrain's index in this list is what maps store.</summary>
    public IReadOnlyList<TerrainDef> Terrains { get; }

    /// <summary>Citizen ageing, needs and growth rules.</summary>
    public CitizenRules Citizens { get; }

    /// <summary>Wild food plants in file order. Plant components store the index.</summary>
    public IReadOnlyList<PlantDef> Plants { get; }

    /// <summary>Game animals in file order. Animal and carcass components store the index.</summary>
    public IReadOnlyList<AnimalDef> Animals { get; }

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

    private static int IndexOf<T>(IReadOnlyList<T> list, Func<T, bool> match, string kind, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (match(list[i])) return i;
        }
        throw new KeyNotFoundException($"Unknown {kind} '{id}'.");
    }
}
