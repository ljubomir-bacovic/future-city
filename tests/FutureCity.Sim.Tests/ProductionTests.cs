using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Workshops, production chains and tools.</summary>
public class ProductionTests
{
    private const int Me = Players.Human;

    private static void Work(Simulation sim, Entity building, params Entity[] people) =>
        sim.Enqueue(new AssignWork(people.Select(p => p.Id).ToArray(), building.Id) { Player = Me });

    [Fact]
    public void Grain_becomes_flour_and_flour_becomes_bread()
    {
        var sim = GameSupport.Plain();
        var civ = GameSupport.Civ(sim);
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.SetAmount(camp, "grain", 40);
        GameSupport.SetAmount(camp, "wood", 10);
        var mill = GameSupport.Building(sim, "mill", 13, 10);
        var bakery = GameSupport.Building(sim, "bakery", 13, 13);
        Work(sim, mill, GameSupport.Adult(sim));
        Work(sim, bakery, GameSupport.Adult(sim));

        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "bread") >= 40, 3000);

        Assert.Equal(0, GameSupport.Amount(camp, "grain"));
        Assert.Equal(40, civ.GetComponent<Civilization>().Produced[GameSupport.Good("flour")]);
        Assert.Equal(40, civ.GetComponent<Civilization>().Produced[GameSupport.Good("bread")]);
        Assert.True(GameSupport.Amount(camp, "wood") < 10, "baking burns wood");
    }

    [Fact]
    public void Bread_feeds_more_people_than_the_grain_it_is_made_from()
    {
        var content = TestSupport.Content;
        int grainMeals = content.Nutrition(content.GoodIndex("grain"));
        int breadMeals = content.Nutrition(content.GoodIndex("bread"));
        Assert.True(breadMeals > grainMeals);
        Assert.Equal(content.GoodIndex("grain"), content.FoodGoods[^1]); // raw grain is eaten last
    }

    [Fact]
    public void Toolmakers_make_tools_from_wood_and_stone()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.SetAmount(camp, "wood", 4);
        GameSupport.SetAmount(camp, "stone", 2);
        var toolmaker = GameSupport.Building(sim, "toolmaker", 13, 10);
        var maker = GameSupport.Adult(sim);
        Work(sim, toolmaker, maker);

        // The toolmaker may keep one of the tools for themselves when dropping them off.
        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "tools") + (maker.GetComponent<Citizen>().ToolWear > 0 ? 1 : 0) >= 2, 2000);
        Assert.Equal(0, GameSupport.Amount(camp, "wood"));
        Assert.Equal(0, GameSupport.Amount(camp, "stone"));
    }

    [Fact]
    public void Workers_pick_up_tools_at_the_store_and_work_faster()
    {
        int Gathered(bool tools)
        {
            var sim = GameSupport.Plain();
            var camp = GameSupport.Camp(sim, 10, 10);
            if (tools) GameSupport.SetAmount(camp, "tools", 1);
            var outcrop = GameSupport.Deposit(sim, "stone_outcrop", 11, 11);
            outcrop.GetComponent<Deposit>().Amount = 10_000;
            var person = GameSupport.Adult(sim, 10, 11);
            // Drop off a first load at camp (picking up a tool), then quarry for a while.
            person.GetComponent<Citizen>() = person.GetComponent<Citizen>() with { CarriedGood = GameSupport.Good("stone"), Carried = 1 };
            sim.Enqueue(new Gather([person.Id], outcrop.Id) { Player = Me });
            GameSupport.Run(sim, 500);
            if (tools)
            {
                Assert.Equal(0, GameSupport.Amount(camp, "tools"));
                Assert.InRange(person.GetComponent<Citizen>().ToolWear, 1, TestSupport.Content.Tools.Durability - 1);
            }
            return GameSupport.Amount(camp, "stone");
        }

        int without = Gathered(false), with = Gathered(true);
        Assert.True(with > without * 5 / 4, $"with tools {with}, without {without}");
    }

    [Fact]
    public void Workshops_wait_for_inputs()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        var mill = GameSupport.Building(sim, "mill", 13, 10);
        var miller = GameSupport.Adult(sim);
        Work(sim, mill, miller);

        GameSupport.Run(sim, 300);
        Assert.Equal(0, GameSupport.Amount(camp, "flour"));
        Assert.Equal(OrderKind.Work, miller.GetComponent<Order>().Kind);

        GameSupport.SetAmount(camp, "grain", 10);
        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "flour") == 10, 600);
    }
}
