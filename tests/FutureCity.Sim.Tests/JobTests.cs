using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Automatic, demand-driven jobs once a chiefdom organizes labour.</summary>
public class JobTests
{
    private const int Me = Players.Human;

    private static (Simulation Sim, Entity Camp, Entity[] People) Village(int food, bool chiefdom)
    {
        var sim = GameSupport.Plain();
        GameSupport.Civ(sim);
        var camp = GameSupport.Camp(sim, 10, 10, food);
        for (int i = 0; i < 4; i++) GameSupport.Plant(sim, 15 + i, 14, food: 100);
        var people = Enumerable.Range(0, 4).Select(_ => GameSupport.Adult(sim)).ToArray();
        if (chiefdom) GameSupport.Establish(sim);
        return (sim, camp, people);
    }

    [Fact]
    public void Without_a_chiefdom_idle_people_wait_for_orders()
    {
        var (sim, _, people) = Village(food: 0, chiefdom: false);
        GameSupport.Run(sim, 200);
        Assert.All(people, p => Assert.Equal(OrderKind.Idle, p.GetComponent<Order>().Kind));
    }

    [Fact]
    public void With_a_chiefdom_idle_people_gather_the_food_that_is_short()
    {
        var (sim, _, people) = Village(food: 0, chiefdom: true);
        GameSupport.Run(sim, TestSupport.Content.Citizens.Jobs.CheckIntervalTicks + 1);
        Assert.All(people, p =>
        {
            var order = p.GetComponent<Order>();
            Assert.Equal(OrderKind.Gather, order.Kind);
            Assert.Equal(TargetType.Plant, order.TargetType);
            Assert.True(order.Auto);
        });
    }

    [Fact]
    public void Construction_sites_get_builders()
    {
        var (sim, camp, people) = Village(food: 10_000, chiefdom: true);
        GameSupport.SetAmount(camp, "wood", 50);
        GameSupport.SetAmount(camp, "clay", 50);
        GameSupport.SetAmount(camp, "stone", 50);
        GameSupport.SetAmount(camp, "tools", 50);
        var hut = GameSupport.Building(sim, "hut", 14, 10, complete: false);

        GameSupport.Run(sim, TestSupport.Content.Citizens.Jobs.CheckIntervalTicks + 1);

        Assert.Contains(people, p => p.GetComponent<Order>() is { Kind: OrderKind.Build } o && o.Target == hut.Id);
        GameSupport.RunUntil(sim, () => Buildings.IsComplete(hut), 1500);
    }

    [Fact]
    public void Players_orders_are_never_overridden()
    {
        var (sim, _, people) = Village(food: 10_000, chiefdom: true);
        GameSupport.SetTerrain(sim, "forest", 20, 20);
        sim.World.Map.SetResource(20, 20, 40);
        sim.Enqueue(new GatherTile([people[0].Id], 20, 20) { Player = Me });

        GameSupport.Run(sim, TestSupport.Content.Citizens.Jobs.CheckIntervalTicks * 5);

        Assert.Equal(TargetType.Tile, people[0].GetComponent<Order>().TargetType);
        Assert.False(people[0].GetComponent<Order>().Auto);
    }

    [Fact]
    public void Automatic_gatherers_are_withdrawn_when_the_need_is_met()
    {
        var (sim, camp, people) = Village(food: 0, chiefdom: true);
        GameSupport.Run(sim, TestSupport.Content.Citizens.Jobs.CheckIntervalTicks + 1);
        Assert.Contains(people, p => p.GetComponent<Order>().Kind == OrderKind.Gather);

        GameSupport.SetAmount(camp, "berries", 100_000);
        // Each drops off the load in hand, then is withdrawn at the next check.
        GameSupport.RunUntil(sim, () => !people.Any(p => p.GetComponent<Order>() is { Kind: OrderKind.Gather, TargetType: TargetType.Plant }), 400);
    }
}
