using FutureCity.Sim.Components;

namespace FutureCity.Sim.Map;

/// <summary>
/// Fills a freshly generated map: the player's camp and band, berry bushes and game herds.
/// Wild food is placed only where the band can walk to, with a few sources guaranteed near the start.
/// </summary>
internal static class WorldPopulator
{
    private const int MaxAttempts = 200;

    public static void Populate(World world, int startX, int startY)
    {
        var map = world.Map;
        var rng = world.Rng;
        var rules = world.Content.Citizens;
        int grass = world.Content.TerrainIndex(world.Content.Rules.DefaultTerrain);
        var occupied = new bool[map.Width * map.Height];
        int clear = world.Content.Rules.MapGeneration.StartClearRadius;

        Spawn.Camp(world, Players.Human, startX, startY, rules.Start.Food);
        occupied[startY * map.Width + startX] = true;
        for (int i = 0; i < rules.Start.Citizens; i++)
        {
            int age = rng.NextInt(rules.Start.MinAgeYears, rules.Start.MaxAgeYears + 1);
            int x = startX + rng.NextInt(-1, 2), y = startY + rng.NextInt(-1, 2);
            Spawn.Citizen(world, Players.Human, x, y, -(long)age * rules.TicksPerYear - rng.NextInt(rules.TicksPerYear));
        }

        int area = map.Width * map.Height;
        for (int kind = 0; kind < world.Content.Plants.Count; kind++)
        {
            var plant = world.Content.Plants[kind];
            int clusters = area * plant.ClustersPer10kTiles / 10_000;
            for (int c = 0; c < plant.StartClusters + clusters; c++)
            {
                bool nearStart = c < plant.StartClusters;
                if (!TryPickTile(world, startX, startY, nearStart ? clear + 2 : clear + 3,
                        nearStart ? clear + 8 : int.MaxValue, out int cx, out int cy))
                    continue;
                int size = rng.NextInt(plant.MinClusterSize, plant.MaxClusterSize + 1);
                for (int placed = 0, attempt = 0; placed < size && attempt < MaxAttempts; attempt++)
                {
                    int x = cx + rng.NextInt(-2, 3), y = cy + rng.NextInt(-2, 3);
                    if (!map.Contains(x, y) || !world.CanReach(startX, startY, x, y) || map.GetTerrain(x, y) != grass
                        || occupied[y * map.Width + x])
                        continue;
                    occupied[y * map.Width + x] = true;
                    Spawn.Plant(world, kind, x, y);
                    placed++;
                }
            }
        }

        for (int kind = 0; kind < world.Content.Animals.Count; kind++)
        {
            var animal = world.Content.Animals[kind];
            int herds = area * animal.HerdsPer10kTiles / 10_000;
            for (int h = 0; h < animal.StartHerds + herds; h++)
            {
                bool nearStart = h < animal.StartHerds;
                // Herds keep their distance from people; the guaranteed ones are a short walk away.
                if (!TryPickTile(world, startX, startY, nearStart ? clear + 6 : clear + 10,
                        nearStart ? clear + 14 : int.MaxValue, out int hx, out int hy))
                    continue;
                int size = rng.NextInt(animal.MinHerdSize, animal.MaxHerdSize + 1);
                for (int placed = 0, attempt = 0; placed < size && attempt < MaxAttempts; attempt++)
                {
                    int x = hx + rng.NextInt(-2, 3), y = hy + rng.NextInt(-2, 3);
                    if (!world.CanReach(startX, startY, x, y)) continue;
                    Spawn.Animal(world, kind, x, y, hx, hy);
                    placed++;
                }
            }
        }
    }

    // A random reachable tile at a Chebyshev distance from the start within [minDistance, maxDistance].
    private static bool TryPickTile(World world, int startX, int startY, int minDistance, int maxDistance,
        out int x, out int y)
    {
        var map = world.Map;
        int radius = Math.Min(maxDistance, Math.Max(map.Width, map.Height));
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            x = Math.Clamp(startX + world.Rng.NextInt(-radius, radius + 1), 0, map.Width - 1);
            y = Math.Clamp(startY + world.Rng.NextInt(-radius, radius + 1), 0, map.Height - 1);
            int distance = new TilePosition(startX, startY).DistanceTo(x, y);
            if (distance >= minDistance && distance <= maxDistance && world.CanReach(startX, startY, x, y))
                return true;
        }
        x = y = 0;
        return false;
    }
}
