using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Coins, wages, job choice by income, and guilds.</summary>
public class LabourTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    public void The_mint_strikes_coins_for_the_treasury_and_debased_coins_come_more_per_silver(int quality)
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.SetAmount(camp, "silver", 3);
        GameSupport.Economy(sim, money: true);
        GameSupport.Civ(sim).GetComponent<Civilization>().CoinQuality = quality;
        var mint = GameSupport.Building(sim, "mint", 14, 10);
        var worker = GameSupport.Adult(sim, 13, 10);
        sim.Enqueue(new AssignWork([worker.Id], mint.Id) { Player = Players.Human });

        int expected = 3 * sim.World.Content.Economy.Money.CoinsPerSilver * 100 / quality;
        var civ = GameSupport.Civ(sim);
        GameSupport.RunUntil(sim, () => civ.GetComponent<Civilization>().Minted >= expected, 2000);
        Assert.Equal(expected, GameSupport.TraderOf(civ).Coins);
        Assert.Equal(expected, civ.GetComponent<Civilization>().Minted);
        Assert.Equal(expected, Economy.MoneySupply(sim.World, Players.Human));
    }

    // A money economy with a marketplace, a family of one and a forest and berry bushes nearby.
    private static (Simulation Sim, Entity Home, Entity Member) Village()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);  // nothing in the public stores: the chief has no work to give
        GameSupport.Economy(sim, money: true);
        GameSupport.Marketplace(sim, 20, 20);
        for (int x = 4; x < 8; x++) GameSupport.SetTerrain(sim, "forest", x, 20);
        sim.World.Map.SetResource(4, 20, 40);
        for (int x = 4; x < 8; x++) sim.World.Map.SetResource(x, 20, 40);
        for (int x = 16; x < 20; x++) GameSupport.Plant(sim, x, 4, 100);
        var home = GameSupport.Home(sim, 12, 14);
        GameSupport.SetAmount(home, "berries", 12); // fed (food is not urgent) and nothing to take to market
        GameSupport.SetAmount(home, "wood", 4);
        GameSupport.SetAmount(home, "tools", 1);
        var member = GameSupport.Adult(sim, 12, 15).LivesIn(home);
        member.GetComponent<Citizen>().ToolWear = 1000;
        return (sim, home, member);
    }

    private static int Wood => GameSupport.Good("wood");
    private static int BerryGood => GameSupport.Good("berries");

    private static int GatheredGood(Simulation sim, Entity unit)
    {
        var order = unit.GetComponent<Order>();
        if (order.Kind != OrderKind.Gather) return -1;
        return order.TargetType == TargetType.Tile ? sim.World.Content.TerrainSource(order.TargetKind)!.Value.Good
            : sim.World.Content.PlantGood(order.TargetKind);
    }

    [Fact]
    public void Families_choose_the_work_that_pays_best_at_their_prices_and_switch_when_prices_change()
    {
        var (sim, home, member) = Village();
        ref var trader = ref GameSupport.TraderOf(home);
        trader.Beliefs[Wood] = 200; // wood fetches a lot
        GameSupport.Run(sim, 21);
        var o = member.GetComponent<Order>();
        Assert.True(Wood == GatheredGood(sim, member), $"{o.Kind} {o.TargetType} {o.Public} {o.Stage}");
        Assert.False(member.GetComponent<Order>().Public);

        GameSupport.TraderOf(home).Beliefs[Wood] = 5;
        GameSupport.TraderOf(home).Beliefs[BerryGood] = 300; // now berries pay far better
        GameSupport.RunUntil(sim, () => GatheredGood(sim, member) == BerryGood, 400);
    }

    [Fact]
    public void Public_workers_are_paid_the_going_wage_from_the_treasury()
    {
        var (sim, home, member) = Village();
        GameSupport.TraderOf(GameSupport.Civ(sim)).Coins = 100_000;
        GameSupport.Adult(sim, 12, 15).LivesIn(home); // a second adult, so the chief may call on one
        GameSupport.Building(sim, "storehouse", 26, 10, complete: false); // public work for the chief
        sim.Enqueue(new Build([member.Id], sim.World.Store.Query<Construction>().Entities.First().Id) { Player = Players.Human });
        GameSupport.Run(sim, 41);

        var civ = GameSupport.Civ(sim).GetComponent<Civilization>();
        Assert.True(civ.PublicWage > 0);
        Assert.True(GameSupport.TraderOf(home).Coins > 0);
        Assert.Equal(GameSupport.TraderOf(home).Coins, civ.Ledger[(int)LedgerEntry.Wages]);
        Assert.Equal(100_000 - civ.Ledger[(int)LedgerEntry.Wages], GameSupport.TraderOf(GameSupport.Civ(sim)).Coins);
    }

    [Fact]
    public void A_treasury_that_can_neither_feed_nor_pay_keeps_only_the_labour_owed_to_the_chief()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 0);
        GameSupport.SetAmount(camp, "wood", 500); // the site can progress
        GameSupport.Economy(sim, money: true);
        var home = GameSupport.Home(sim, 12, 14);
        GameSupport.SetAmount(home, "berries", 400);
        var site = GameSupport.Building(sim, "storehouse", 26, 10, complete: false);
        var members = new List<Entity>();
        for (int i = 0; i < 4; i++)
        {
            var member = GameSupport.Adult(sim, 12, 15).LivesIn(home);
            UnitOrders.Assign(member, OrderKind.Build, site, TargetType.Building, site.GetComponent<Building>().Kind);
            member.GetComponent<Order>().Auto = true;
            member.GetComponent<Order>().Public = true;
            members.Add(member);
        }

        GameSupport.Run(sim, 21);
        int owed = sim.World.Content.Economy.Wages.MinPublicWorkers;
        Assert.Equal(owed, members.Count(m => m.GetComponent<Order>().Public && m.GetComponent<Order>().Kind == OrderKind.Build));
    }

    [Fact]
    public void A_young_village_still_owes_the_chief_a_few_workers()
    {
        // Four adults in homes and an empty granary: 30% of them is one, but the chief can call on two.
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 0);
        GameSupport.SetAmount(camp, "wood", 500);
        GameSupport.Economy(sim);
        var home = GameSupport.Home(sim, 12, 14);
        GameSupport.SetAmount(home, "berries", 400);
        var members = Enumerable.Range(0, 4).Select(_ => GameSupport.Adult(sim, 12, 15).LivesIn(home)).ToList();
        GameSupport.Building(sim, "storehouse", 26, 10, complete: false);

        GameSupport.Run(sim, 41);
        Assert.Equal(sim.World.Content.Economy.Wages.MinPublicWorkers, members.Count(m => m.GetComponent<Order>().Public));
    }

    [Fact]
    public void Guild_workshops_make_more_per_batch()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim, money: true);
        GameSupport.Establish(sim, "guilds");
        var shop = GameSupport.Building(sim, "toolmaker", 14, 10);
        GameSupport.SetAmount(shop, "wood", 8);
        GameSupport.SetAmount(shop, "stone", 4);
        var worker = GameSupport.Adult(sim, 13, 10);
        sim.Enqueue(new AssignWork([worker.Id], shop.Id) { Player = Players.Human });

        // Four batches of one tool each, plus a quarter more: five tools.
        GameSupport.RunUntil(sim, () => GameSupport.Amount(shop, "stone") == 0 && worker.GetComponent<Citizen>().Carried == 0
                                        && GameSupport.Amount(shop, "tools") == 0, 3000);
        Assert.Equal(5, GameSupport.Amount(camp, "tools") + (worker.GetComponent<Citizen>().ToolWear > 0 ? 1 : 0));
    }

    [Fact]
    public void Guild_members_do_not_sell_below_cost_plus_margin()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim, money: true);
        var home = GameSupport.Home(sim, 12, 14);
        ref var trader = ref GameSupport.TraderOf(home);
        int tools = GameSupport.Good("tools");
        trader.Beliefs[tools] = 1;
        var plan = Traders.PlanOf(sim.World, home);
        Assert.Equal(1, Traders.AskLimit(sim.World, home, plan, tools));

        GameSupport.Establish(sim, "guilds");
        var recipe = sim.World.Content.Buildings[sim.World.Content.BuildingIndex("toolmaker")];
        long cost = 0;
        for (int g = 0; g < recipe.Inputs.Count; g++) cost += (long)recipe.Inputs[g] * trader.Beliefs[g];
        int floor = (int)(cost * (100 + sim.World.Content.Economy.Guilds.MarginPercent) / 100);
        Assert.Equal(floor, Traders.AskLimit(sim.World, home, plan, tools));
    }

    [Fact]
    public void Each_year_a_guilded_craft_admits_only_a_few_new_members()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim, money: true);
        GameSupport.Establish(sim, "guilds");
        var shop = GameSupport.Building(sim, "toolmaker", 14, 10);
        GameSupport.SetAmount(shop, "wood", 1000);
        GameSupport.SetAmount(shop, "stone", 1000);
        var worker = GameSupport.Adult(sim, 13, 10);
        sim.Enqueue(new AssignWork([worker.Id], shop.Id) { Player = Players.Human });
        GameSupport.Run(sim, sim.World.Content.Calendar.TicksPerYear + 1); // caps are set at the turn of the year

        var caps = GameSupport.Civ(sim).GetComponent<Civilization>().GuildCap;
        int perYear = sim.World.Content.Economy.Guilds.NewMembersPerYear;
        Assert.Equal(1 + perYear, caps[sim.World.Content.BuildingIndex("toolmaker")]);
        Assert.Equal(0, caps[sim.World.Content.BuildingIndex("farm")]); // not a craft
    }
}
