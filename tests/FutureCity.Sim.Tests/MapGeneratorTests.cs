using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

public class MapGeneratorTests
{
    public static TheoryData<ulong, string> Maps => new()
    {
        { 1, "small" }, { 2, "small" }, { 3, "medium" }, { 4, "large" }, { 42, "small" },
    };

    [Fact]
    public void Same_seed_gives_the_same_map_and_different_seeds_differ()
    {
        var a = GameSupport.NewGame(5).World.Map.RawTerrain.ToArray();
        var b = GameSupport.NewGame(5).World.Map.RawTerrain.ToArray();
        var c = GameSupport.NewGame(6).World.Map.RawTerrain.ToArray();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Water_and_forest_cover_about_the_configured_share(ulong seed, string size)
    {
        var world = GameSupport.NewGame(seed, size).World;
        var gen = world.Content.Rules.MapGeneration;
        var terrain = world.Map.RawTerrain.ToArray();
        int water = terrain.Count(t => t == world.Content.TerrainIndex("water"));
        int forest = terrain.Count(t => t == world.Content.TerrainIndex("forest"));
        Assert.InRange(water * 100 / terrain.Length, gen.WaterPercent - 3, gen.WaterPercent + 3);
        Assert.InRange(forest * 100 / (terrain.Length - water), gen.ForestPercent - 4, gen.ForestPercent + 3);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void Band_starts_in_a_clearing_with_reachable_food_nearby(ulong seed, string size)
    {
        var world = GameSupport.NewGame(seed, size).World;
        var rules = world.Content.Citizens;
        Assert.True(Bands.TryGetCamp(world, Players.Human, out var camp));
        var start = camp.GetComponent<TilePosition>();
        Assert.Equal(rules.Start.Food, camp.GetComponent<Camp>().Food);

        int clear = world.Content.Rules.MapGeneration.StartClearRadius;
        int grass = world.Content.TerrainIndex("grass");
        for (int y = start.Y - clear; y <= start.Y + clear; y++)
        {
            for (int x = start.X - clear; x <= start.X + clear; x++)
                Assert.Equal(grass, world.Map.GetTerrain(x, y));
        }

        Assert.Equal(rules.Start.Citizens, Bands.CensusOf(world, Players.Human).Adults);
        foreach (var entity in world.Store.Query<TilePosition>().Entities)
        {
            var pos = entity.GetComponent<TilePosition>();
            Assert.True(world.CanReach(start.X, start.Y, pos.X, pos.Y), $"entity {entity.Id} at ({pos.X}, {pos.Y}) is unreachable");
        }

        var bush = world.Content.Plants[0];
        int nearBushes = world.Store.Query<Plant, TilePosition>().Entities
            .Count(e => e.GetComponent<TilePosition>().DistanceTo(start.X, start.Y) <= clear + 10);
        Assert.True(nearBushes >= bush.StartClusters * bush.MinClusterSize, $"only {nearBushes} bushes near the start");
        Assert.Contains(world.Store.Query<Animal, TilePosition>().Entities,
            e => e.GetComponent<TilePosition>().DistanceTo(start.X, start.Y) <= clear + 16);
    }
}
