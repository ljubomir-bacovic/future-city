using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Wood, stone and clay: gathering, depletion and carrying goods to stores.</summary>
public class ResourceTests
{
    private const int Me = Players.Human;

    private static void Forest(Simulation sim, int x, int y, int wood)
    {
        GameSupport.SetTerrain(sim, "forest", x, y);
        sim.World.Map.SetResource(x, y, wood);
    }

    [Fact]
    public void Chopping_a_forest_tile_yields_wood_and_clears_it()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        Forest(sim, 15, 10, 3);
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new GatherTile([person.Id], 15, 10) { Player = Me });

        GameSupport.RunUntil(sim, () => person.GetComponent<Order>().Kind == OrderKind.Idle, 600);

        Assert.Equal(sim.World.Content.TerrainIndex("grass"), sim.World.Map.GetTerrain(15, 10));
        Assert.Equal(0, sim.World.Map.GetResource(15, 10));
        Assert.Equal(3, GameSupport.Amount(camp, "wood"));
    }

    [Fact]
    public void Woodcutters_move_on_to_nearby_forest()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        Forest(sim, 15, 10, 2);
        Forest(sim, 17, 11, 30);
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new GatherTile([person.Id], 15, 10) { Player = Me });

        GameSupport.RunUntil(sim, () => sim.World.Map.GetResource(17, 11) < 30, 600);
        Assert.Equal(OrderKind.Gather, person.GetComponent<Order>().Kind);
    }

    [Fact]
    public void Deposits_run_out_and_disappear()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var outcrop = GameSupport.Deposit(sim, "stone_outcrop", 14, 12);
        outcrop.GetComponent<Deposit>().Amount = 4;
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new Gather([person.Id], outcrop.Id) { Player = Me });

        GameSupport.RunUntil(sim, () => person.GetComponent<Order>().Kind == OrderKind.Idle, 600);

        Assert.False(sim.World.TryGetEntity(outcrop.Id, out _));
        Assert.Equal(4, GameSupport.Amount(camp, "stone"));
    }

    [Fact]
    public void Goods_go_to_the_nearest_store()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var storehouse = GameSupport.Building(sim, "storehouse", 30, 10);
        var pit = GameSupport.Deposit(sim, "clay_pit", 33, 13);
        var person = GameSupport.Adult(sim, 31, 13);
        sim.Enqueue(new Gather([person.Id], pit.Id) { Player = Me });

        GameSupport.RunUntil(sim, () => GameSupport.Amount(storehouse, "clay") > 0, 600);
        Assert.Equal(0, GameSupport.Amount(camp, "clay"));
    }

    [Fact]
    public void Construction_sites_are_not_stores()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        var site = GameSupport.Building(sim, "storehouse", 30, 10, complete: false);
        Assert.False(Stores.IsStore(sim.World, site));
        Assert.Single(Stores.Of(sim.World, Me));
    }

    [Fact]
    public void Distance_to_the_store_lowers_throughput()
    {
        int Delivered(int distance)
        {
            var sim = GameSupport.Plain();
            var camp = GameSupport.Camp(sim, 10, 10);
            var outcrop = GameSupport.Deposit(sim, "stone_outcrop", 10 + distance, 10);
            outcrop.GetComponent<Deposit>().Amount = 10_000;
            var person = GameSupport.Adult(sim, 10, 10);
            sim.Enqueue(new Gather([person.Id], outcrop.Id) { Player = Me });
            GameSupport.Run(sim, 1200);
            return GameSupport.Amount(camp, "stone");
        }

        int near = Delivered(3), far = Delivered(30);
        Assert.True(near > far * 2, $"near {near}, far {far}");
    }

    [Fact]
    public void Gathered_goods_count_for_the_civilization()
    {
        var sim = GameSupport.Plain();
        var civ = GameSupport.Civ(sim);
        GameSupport.Camp(sim, 10, 10);
        var outcrop = GameSupport.Deposit(sim, "stone_outcrop", 13, 10);
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new Gather([person.Id], outcrop.Id) { Player = Me });

        GameSupport.Run(sim, 300);

        var record = civ.GetComponent<Civilization>();
        Assert.True(record.Gathered[GameSupport.Good("stone")] > 0);
        Assert.True(record.Work[(int)WorkKind.Quarry] > 0);
    }
}
