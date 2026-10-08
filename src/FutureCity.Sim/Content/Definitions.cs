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

/// <summary>Global game-setup rules (rules.json).</summary>
public sealed record GameRules
{
    /// <summary>Map sizes available at setup.</summary>
    public required IReadOnlyList<MapSizeDef> MapSizes { get; init; }
    /// <summary>Map size used when none is chosen.</summary>
    public required string DefaultMapSize { get; init; }
    /// <summary>Terrain that fills a new map before generation adds features.</summary>
    public required string DefaultTerrain { get; init; }
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
}

/// <summary>File shape of terrain.json.</summary>
internal sealed record TerrainFile
{
    public required IReadOnlyList<TerrainDef> Terrains { get; init; }
}
