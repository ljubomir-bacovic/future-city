using FutureCity.Sim.Content;
using FutureCity.Sim.Navigation;
using FutureCity.Sim.Random;

namespace FutureCity.Sim.Map;

/// <summary>A generated map and where the player's band starts.</summary>
/// <param name="Map">The terrain.</param>
/// <param name="StartX">Starting camp column.</param>
/// <param name="StartY">Starting camp row.</param>
public sealed record GeneratedMap(TileMap Map, int StartX, int StartY);

/// <summary>
/// Builds the starting terrain for a new game: lakes and woods shaped by smooth noise, with the band's
/// start placed on open grassland in the largest connected land area, as close to the map center as possible.
/// </summary>
public static class MapGenerator
{
    /// <summary>Generates the terrain for <paramref name="setup"/>. Uses only <paramref name="rng"/> for randomness.</summary>
    public static GeneratedMap Generate(GameSetup setup, ContentDatabase content, Pcg32 rng)
    {
        var size = content.MapSize(setup.MapSize);
        var gen = content.Rules.MapGeneration;
        int width = size.Width, height = size.Height;
        int grass = content.TerrainIndex(content.Rules.DefaultTerrain);
        int water = content.TerrainIndex(gen.WaterTerrain);
        int forest = content.TerrainIndex(gen.ForestTerrain);
        var map = new TileMap(width, height, (byte)grass);

        var elevation = ValueNoise.Generate(width, height, gen.FeatureSize, rng);
        var woods = ValueNoise.Generate(width, height, Math.Max(4, gen.FeatureSize / 2), rng);

        int seaLevel = gen.WaterPercent == 0 ? int.MinValue : ValueNoise.Percentile(elevation, gen.WaterPercent);
        var landWoods = new List<int>();
        for (int i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < seaLevel) map.SetTerrain(i % width, i / width, water);
            else landWoods.Add(woods[i]);
        }
        int treeLine = gen.ForestPercent == 0 ? int.MaxValue : ValueNoise.Percentile(landWoods, 100 - gen.ForestPercent);
        for (int i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] >= seaLevel && woods[i] >= treeLine) map.SetTerrain(i % width, i / width, forest);
        }

        var (startX, startY) = ChooseStart(map, content, gen.StartClearRadius);
        for (int y = startY - gen.StartClearRadius; y <= startY + gen.StartClearRadius; y++)
        {
            for (int x = startX - gen.StartClearRadius; x <= startX + gen.StartClearRadius; x++)
            {
                if (map.Contains(x, y)) map.SetTerrain(x, y, grass);
            }
        }
        AddResources(map, content);
        AddFertility(map, content, water, rng);
        return new GeneratedMap(map, startX, startY);
    }

    // Every tile starts with its terrain's full resource (e.g. wood in forests).
    private static void AddResources(TileMap map, ContentDatabase content)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
                map.SetResource(x, y, content.Terrains[map.GetTerrain(x, y)].Resource?.Amount ?? 0);
        }
    }

    // Soil fertility: smooth variation between min and max, plus a bonus near water that fades with distance.
    private static void AddFertility(TileMap map, ContentDatabase content, int water, Pcg32 rng)
    {
        var rules = content.Rules.MapGeneration.Fertility;
        var noise = ValueNoise.Generate(map.Width, map.Height, content.Rules.MapGeneration.FeatureSize, rng);
        int low = ValueNoise.Percentile(noise, 0), high = Math.Max(low + 1, ValueNoise.Percentile(noise, 100));
        int radius = rules.WaterRadius;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int fertility = rules.Min + (int)((long)(noise[y * map.Width + x] - low) * (rules.Max - rules.Min) / (high - low));
                int nearest = int.MaxValue;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (map.Contains(x + dx, y + dy) && map.GetTerrain(x + dx, y + dy) == water)
                            nearest = Math.Min(nearest, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                    }
                }
                if (nearest != int.MaxValue) fertility += rules.WaterBonus * (radius + 1 - nearest) / (radius + 1);
                map.SetFertility(x, y, Math.Clamp(fertility, 0, 100));
            }
        }
    }

    // The tile nearest the center (ring by ring, in scan order) whose surroundings are dry land in the largest area.
    private static (int X, int Y) ChooseStart(TileMap map, ContentDatabase content, int clearRadius)
    {
        var pathfinder = new Pathfinder(map, content);
        var labels = Regions.Label(map, pathfinder, out int regionCount);
        var regionSize = new int[regionCount + 1];
        foreach (int label in labels) regionSize[label]++;
        int largest = 0;
        for (int r = 1; r <= regionCount; r++)
        {
            if (regionSize[r] > regionSize[largest]) largest = r;
        }

        int cx = map.Width / 2, cy = map.Height / 2;
        int maxRing = Math.Max(map.Width, map.Height);
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int y = cy - ring; y <= cy + ring; y++)
            {
                for (int x = cx - ring; x <= cx + ring; x++)
                {
                    if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != ring) continue;
                    if (largest != 0 && IsGoodStart(map, labels, largest, x, y, clearRadius)) return (x, y);
                }
            }
        }
        return (cx, cy); // no dry land anywhere: the clearing alone becomes the start area
    }

    private static bool IsGoodStart(TileMap map, int[] labels, int region, int x, int y, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int tx = x + dx, ty = y + dy;
                if (!map.Contains(tx, ty) || labels[ty * map.Width + tx] != region) return false;
            }
        }
        return true;
    }
}
