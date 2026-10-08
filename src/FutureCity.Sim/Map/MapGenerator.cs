using FutureCity.Sim.Content;
using FutureCity.Sim.Random;

namespace FutureCity.Sim.Map;

/// <summary>
/// Builds the starting map for a new game.
/// Phase 0 produces an empty map of the default terrain; terrain features, water and resources arrive in Phase 1.
/// </summary>
public static class MapGenerator
{
    /// <summary>Generates the map for <paramref name="setup"/>. Must only use <paramref name="rng"/> for randomness.</summary>
    public static TileMap Generate(GameSetup setup, ContentDatabase content, Pcg32 rng)
    {
        _ = rng; // reserved for Phase 1 terrain generation
        var size = content.MapSize(setup.MapSize);
        int fill = content.TerrainIndex(content.Rules.DefaultTerrain);
        return new TileMap(size.Width, size.Height, (byte)fill);
    }
}
