namespace FutureCity.Sim.Content;

/// <summary>
/// All loaded, validated game content. Immutable after loading.
/// </summary>
public sealed class ContentDatabase
{
    private readonly Dictionary<string, int> _terrainIndex;

    internal ContentDatabase(GameRules rules, IReadOnlyList<TerrainDef> terrains, string hash)
    {
        Rules = rules;
        Terrains = terrains;
        Hash = hash;
        _terrainIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < terrains.Count; i++)
            _terrainIndex[terrains[i].Id] = i;
    }

    /// <summary>Global setup rules.</summary>
    public GameRules Rules { get; }

    /// <summary>Terrain types in file order. A terrain's index in this list is what maps store.</summary>
    public IReadOnlyList<TerrainDef> Terrains { get; }

    /// <summary>SHA-256 of all content files. Saves record it so a save is never loaded against different content.</summary>
    public string Hash { get; }

    /// <summary>Returns the index of the terrain with the given id.</summary>
    public int TerrainIndex(string id) =>
        _terrainIndex.TryGetValue(id, out int index) ? index : throw new KeyNotFoundException($"Unknown terrain '{id}'.");

    /// <summary>Returns the map size with the given id.</summary>
    public MapSizeDef MapSize(string id) =>
        Rules.MapSizes.FirstOrDefault(m => m.Id == id) ?? throw new KeyNotFoundException($"Unknown map size '{id}'.");
}
