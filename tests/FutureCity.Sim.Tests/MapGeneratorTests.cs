using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

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
        Assert.Equal(rules.Start.Goods["berries"], GameSupport.Berries(camp));

        int clear = world.Content.Rules.MapGeneration.StartClearRadius;
        int grass = world.Content.TerrainIndex("grass");
        for (int y = start.Y - clear; y <= start.Y + clear; y++)
        {
            for (int x = start.X - clear; x <= start.X + clear; x++)
                Assert.Equal(grass, world.Map.GetTerrain(x, y));
        }

        var census = Bands.CensusOf(world, Players.Human);
        Assert.Equal(rules.Start.Citizens, census.Total);
        Assert.True(census.Adults >= 2, "a band starts with at least two adults");
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

    [Theory]
    [MemberData(nameof(Maps))]
    public void Two_civilizations_start_far_apart_each_with_its_own_band_and_food(ulong seed, string size)
    {
        var world = GameSupport.NewGame(seed, size, civilizations: 2).World;
        var rules = world.Content.Citizens;
        int clear = world.Content.Rules.MapGeneration.StartClearRadius;
        Assert.True(Bands.TryGetCamp(world, 1, out var first));
        Assert.True(Bands.TryGetCamp(world, 2, out var second));
        var a = first.GetComponent<TilePosition>();
        var b = second.GetComponent<TilePosition>();
        Assert.True(a.DistanceTo(b.X, b.Y) >= world.Map.Width / 3, $"camps only {a.DistanceTo(b.X, b.Y)} tiles apart");
        Assert.True(world.CanReach(a.X, a.Y, b.X, b.Y), "the two bands share one land area");
        foreach (var (player, start) in new[] { (1, a), (2, b) })
        {
            Assert.True(Civics.TryGet(world, player, out _));
            Assert.Equal(rules.Start.Citizens, Bands.CensusOf(world, player).Total);
            int nearBushes = world.Store.Query<Plant, TilePosition>().Entities
                .Count(e => e.GetComponent<TilePosition>().DistanceTo(start.X, start.Y) <= clear + 10);
            Assert.True(nearBushes >= world.Content.Plants[0].StartClusters * world.Content.Plants[0].MinClusterSize);
            Assert.Contains(world.Store.Query<Deposit, TilePosition>().Entities,
                e => e.GetComponent<TilePosition>().DistanceTo(start.X, start.Y) <= clear + 14);
        }
    }

    [Fact]
    public void A_game_has_one_to_four_civilizations()
    {
        Assert.Throws<ArgumentException>(() => GameSupport.NewGame(1, "small", civilizations: 5));
        Assert.Throws<ArgumentException>(() => GameSupport.NewGame(1, "small", civilizations: 0));
    }
}
