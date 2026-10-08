using FutureCity.Sim.Components;

namespace FutureCity.Sim.Map;

/// <summary>
/// Fills a freshly generated map: the player's civilization, camp and band, berry bushes, game herds and raw
/// material deposits. Everything is placed only where the band can walk to, with a few sources guaranteed near the start.
/// </summary>
internal static class WorldPopulator
{
    private const int MaxAttempts = 200;

    public static void Populate(World world, int startX, int startY)
    {
        var map = world.Map;
        var rng = world.Rng;
        var content = world.Content;
        var rules = content.Citizens;
        int ticksPerYear = content.Calendar.TicksPerYear;
        int grass = content.TerrainIndex(content.Rules.DefaultTerrain);
        var occupied = new bool[map.Width * map.Height];
        int clear = content.Rules.MapGeneration.StartClearRadius;

        Spawn.Civilization(world, Players.Human);
        var camp = Spawn.Camp(world, Players.Human, startX, startY);
        // Start goods in id order of the goods list, so the result never depends on dictionary order.
        for (int good = 0; good < content.Goods.Count; good++)
        {
            if (rules.Start.Goods.TryGetValue(content.Goods[good].Id, out int amount))
                Stores.Put(camp, good, amount);
        }
        MarkCamp(occupied, map, startX, startY);
        for (int i = 0; i < rules.Start.Citizens; i++)
        {
            // The first two are adults (parents); the rest of the band may include children.
            int youngest = i < 2 ? Math.Max(rules.Start.MinAgeYears, rules.AdultAgeYears) : rules.Start.MinAgeYears;
            int age = rng.NextInt(youngest, rules.Start.MaxAgeYears + 1);
            int x = startX + rng.NextInt(-1, 2), y = startY + rng.NextInt(-1, 2);
            Spawn.Citizen(world, Players.Human, x, y, -(long)age * ticksPerYear - rng.NextInt(ticksPerYear));
        }

        int area = map.Width * map.Height;
        for (int kind = 0; kind < content.Plants.Count; kind++)
        {
            var plant = content.Plants[kind];
            int clusters = area * plant.ClustersPer10kTiles / 10_000;
            for (int c = 0; c < plant.StartClusters + clusters; c++)
            {
                bool nearStart = c < plant.StartClusters;
                if (!TryPickTile(world, startX, startY, nearStart ? clear + 2 : clear + 3,
                        nearStart ? clear + 8 : int.MaxValue, _ => true, out int cx, out int cy))
                    continue;
                int size = rng.NextInt(plant.MinClusterSize, plant.MaxClusterSize + 1);
                PlaceCluster(world, occupied, grass, startX, startY, cx, cy, size, _ => true, (x, y) => Spawn.Plant(world, kind, x, y));
            }
        }

        for (int kind = 0; kind < content.Deposits.Count; kind++)
        {
            var deposit = content.Deposits[kind];
            Func<(int X, int Y), bool> site = deposit.NearWater ? t => NextToWater(world, t.X, t.Y) : _ => true;
            int clusters = area * deposit.ClustersPer10kTiles / 10_000;
            for (int c = 0; c < deposit.StartClusters + clusters; c++)
            {
                bool nearStart = c < deposit.StartClusters;
                // A guaranteed deposit that finds no suitable spot nearby is placed wherever one exists.
                if (!TryPickTile(world, startX, startY, clear + 2, nearStart ? clear + 12 : int.MaxValue, site, out int cx, out int cy)
                    && !(nearStart && TryPickTile(world, startX, startY, clear + 2, int.MaxValue, site, out cx, out cy)))
                    continue;
                int size = rng.NextInt(deposit.MinClusterSize, deposit.MaxClusterSize + 1);
                PlaceCluster(world, occupied, grass, startX, startY, cx, cy, size, site, (x, y) => Spawn.Deposit(world, kind, x, y));
            }
        }

        for (int kind = 0; kind < content.Animals.Count; kind++)
        {
            var animal = content.Animals[kind];
            int herds = area * animal.HerdsPer10kTiles / 10_000;
            for (int h = 0; h < animal.StartHerds + herds; h++)
            {
                bool nearStart = h < animal.StartHerds;
                // Herds keep their distance from people; the guaranteed ones are a short walk away.
                if (!TryPickTile(world, startX, startY, nearStart ? clear + 6 : clear + 10,
                        nearStart ? clear + 14 : int.MaxValue, _ => true, out int hx, out int hy))
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

    // The camp and the ring around it stay clear, so the band has room to build its first huts next to the fire.
    private static void MarkCamp(bool[] occupied, TileMap map, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (map.Contains(x + dx, y + dy)) occupied[(y + dy) * map.Width + x + dx] = true;
            }
        }
    }

    // Places up to `size` things on free, reachable grass tiles around (cx, cy).
    private static void PlaceCluster(World world, bool[] occupied, int grass, int startX, int startY, int cx, int cy, int size,
        Func<(int X, int Y), bool> site, Action<int, int> spawn)
    {
        var map = world.Map;
        for (int placed = 0, attempt = 0; placed < size && attempt < MaxAttempts; attempt++)
        {
            int x = cx + world.Rng.NextInt(-2, 3), y = cy + world.Rng.NextInt(-2, 3);
            if (!map.Contains(x, y) || !world.CanReach(startX, startY, x, y) || map.GetTerrain(x, y) != grass
                || occupied[y * map.Width + x] || !site((x, y)))
                continue;
            occupied[y * map.Width + x] = true;
            spawn(x, y);
            placed++;
        }
    }

    private static bool NextToWater(World world, int x, int y)
    {
        int water = world.Content.TerrainIndex(world.Content.Rules.MapGeneration.WaterTerrain);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (world.Map.Contains(x + dx, y + dy) && world.Map.GetTerrain(x + dx, y + dy) == water) return true;
            }
        }
        return false;
    }

    // A random reachable tile meeting `site` at a Chebyshev distance from the start within [minDistance, maxDistance].
    private static bool TryPickTile(World world, int startX, int startY, int minDistance, int maxDistance,
        Func<(int X, int Y), bool> site, out int x, out int y)
    {
        var map = world.Map;
        int radius = Math.Min(maxDistance, Math.Max(map.Width, map.Height));
        for (int attempt = 0; attempt < MaxAttempts * 5; attempt++)
        {
            x = Math.Clamp(startX + world.Rng.NextInt(-radius, radius + 1), 0, map.Width - 1);
            y = Math.Clamp(startY + world.Rng.NextInt(-radius, radius + 1), 0, map.Height - 1);
            int distance = new TilePosition(startX, startY).DistanceTo(x, y);
            if (distance >= minDistance && distance <= maxDistance && world.CanReach(startX, startY, x, y) && site((x, y)))
                return true;
        }
        x = y = 0;
        return false;
    }
}
