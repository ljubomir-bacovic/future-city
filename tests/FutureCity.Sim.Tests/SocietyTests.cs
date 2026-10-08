using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Tests;

/// <summary>Social classes and happiness.</summary>
public class SocietyTests
{
    [Fact]
    public void Classes_follow_from_work_and_family_wealth()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim, money: true);
        var shrine = GameSupport.Building(sim, "shrine", 14, 10);
        var shop = GameSupport.Building(sim, "toolmaker", 16, 10);
        var market = GameSupport.Marketplace(sim, 20, 20);
        var homes = Enumerable.Range(0, 4).Select(i => GameSupport.Home(sim, 30, 10 + 2 * i)).ToArray();
        GameSupport.TraderOf(homes[3]).Coins = 100_000; // far richer than the rest
        var priest = GameSupport.Adult(sim).LivesIn(homes[0]);
        var smith = GameSupport.Adult(sim).LivesIn(homes[1]);
        var trader = GameSupport.Adult(sim).LivesIn(homes[2]);
        var lord = GameSupport.Adult(sim).LivesIn(homes[3]);
        var farmer = GameSupport.Adult(sim).LivesIn(homes[0]);
        UnitOrders.Assign(priest, OrderKind.Work, shrine, TargetType.Building, 0);
        UnitOrders.Assign(smith, OrderKind.Work, shop, TargetType.Building, 0);
        UnitOrders.Assign(trader, OrderKind.Work, market, TargetType.Building, 0);
        UnitOrders.Assign(lord, OrderKind.Work, shop, TargetType.Building, 0);

        var nobles = Society.NobleHomes(sim.World, Players.Human);
        Assert.Equal(SocialClass.Clergy, Society.ClassOf(sim.World, priest, nobles));
        Assert.Equal(SocialClass.Craftsmen, Society.ClassOf(sim.World, smith, nobles));
        Assert.Equal(SocialClass.Merchants, Society.ClassOf(sim.World, trader, nobles));
        Assert.Equal(SocialClass.Nobility, Society.ClassOf(sim.World, lord, nobles)); // wealth comes first
        Assert.Equal(SocialClass.Peasants, Society.ClassOf(sim.World, farmer, nobles));

        var facts = Civics.FactsOf(sim.World, Players.Human);
        var craftsmen = sim.World.Content.ResolveFact("class.craftsmen")!.Value;
        Assert.Equal(1, facts.Get(craftsmen));
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(50, 100)]
    [InlineData(100, 120)]
    public void Happy_people_work_faster(int happiness, int percent)
    {
        var sim = GameSupport.Plain();
        Assert.Equal(percent, Labor.Productivity(sim.World, happiness));
    }

    [Fact]
    public void Happiness_rises_with_a_home_and_falls_with_heavy_taxes()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim);
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.SetAmount(home, "wood", 100);
        var housed = GameSupport.Adult(sim).LivesIn(home);
        for (int i = 0; i < 3; i++) GameSupport.Adult(sim).LivesIn(home); // the hut is full
        var camper = GameSupport.Adult(sim);
        int interval = sim.World.Content.Economy.Happiness.CheckIntervalTicks;

        GameSupport.Run(sim, interval * 20);
        int happy = housed.GetComponent<Citizen>().Happiness;
        Assert.True(happy > camper.GetComponent<Citizen>().Happiness);

        sim.Enqueue(new SetTaxes(sim.World.Content.Economy.Taxes.Tribute.Max, 0, 0) { Player = Players.Human });
        GameSupport.Run(sim, interval * 20);
        Assert.True(housed.GetComponent<Citizen>().Happiness < happy);
    }

    [Fact]
    public void Miserable_people_fall_into_unrest()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim);
        for (int i = 0; i < 4; i++)
        {
            var unit = GameSupport.Adult(sim);
            unit.GetComponent<Citizen>().Happiness = 5;
            unit.GetComponent<Citizen>().Health = sim.World.Content.Citizens.MaxHealth / 4; // sick
        }
        sim.Enqueue(new SetTaxes(sim.World.Content.Economy.Taxes.Tribute.Max, 0, 0) { Player = Players.Human });

        var events = GameSupport.Run(sim, sim.World.Content.Economy.Happiness.CheckIntervalTicks + 1);
        Assert.Single(events, e => e.Kind == SimEventKind.Unrest);
        Assert.True(GameSupport.Civ(sim).GetComponent<Civilization>().Unrest);
    }
}
