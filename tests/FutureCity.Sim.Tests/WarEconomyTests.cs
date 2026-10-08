using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Loot, closed markets and raided caravans: what war does to an economy.</summary>
public class WarEconomyTests
{
    // Player 1's town with a marketplace, and player 2's camp far away.
    private static (Simulation Sim, Entity Market, Entity EnemyCamp) Towns()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000, player: 1);
        GameSupport.Economy(sim, money: true);
        var market = GameSupport.Marketplace(sim, 20, 20);
        var enemyCamp = GameSupport.Camp(sim, 50, 50, food: 1000, player: 2);
        GameSupport.CivOf(sim, 2);
        return (sim, market, enemyCamp);
    }

    [Fact]
    public void Soldiers_carry_the_most_valuable_goods_off_from_an_enemy_store_to_their_own()
    {
        var (sim, _, enemyCamp) = Towns();
        GameSupport.SetAmount(enemyCamp, "berries", 0);
        GameSupport.SetAmount(enemyCamp, "tools", 3);
        GameSupport.SetAmount(enemyCamp, "wood", 12);
        var raider = GameSupport.Soldier(sim, "clubman", 48, 48, player: 1);
        GameSupport.Camp(sim, 40, 40, player: 1); // a forward store nearby
        var store = World.InIdOrder(sim.World.Store.Query<Camp>()).Last();

        sim.Enqueue(new Loot([raider.Id], enemyCamp.Id) { Player = 1 });
        GameSupport.Run(sim, 5);
        Assert.Equal(OrderKind.Idle, raider.GetComponent<Order>().Kind); // not at war: no looting

        GameSupport.War(sim);
        sim.Enqueue(new Loot([raider.Id], enemyCamp.Id) { Player = 1 });
        var events = GameSupport.Run(sim, 40);
        Assert.Equal(GameSupport.Good("tools"), raider.GetComponent<Citizen>().CarriedGood); // tools first: worth most
        Assert.Contains(events, e => e.Kind == SimEventKind.Plundered && e.Player == 2 && e.Detail == 3);

        GameSupport.RunUntil(sim, () => GameSupport.Amount(store, "wood") == 12, 2000);
        Assert.Equal(3, GameSupport.Amount(store, "tools"));
        var civ = GameSupport.CivOf(sim, 1).GetComponent<Civilization>();
        Assert.Equal(15, civ.Looted);
        Assert.Equal(15, civ.Ledger[(int)LedgerEntry.Loot]);
        Assert.Equal(15, GameSupport.CivOf(sim, 2).GetComponent<Civilization>().Plundered);
    }

    [Fact]
    public void Ruins_can_be_looted_by_anyone_and_decay()
    {
        var (sim, _, _) = Towns();
        var amounts = new int[sim.World.Content.Goods.Count];
        amounts[GameSupport.Good("stone")] = 8;
        Combat.Spill(sim.World, 14, 14, amounts);
        var pile = Assert.Single(World.InIdOrder(sim.World.Store.Query<LootPile>()));
        var soldier = GameSupport.Soldier(sim, "spearman", 13, 13, player: 1);
        sim.Enqueue(new Loot([soldier.Id], pile.Id) { Player = 1 });
        GameSupport.Run(sim, 200);
        Assert.True(Bands.TryGetCamp(sim.World, 1, out var camp));
        Assert.Equal(8, GameSupport.Amount(camp, "stone"));

        Combat.Spill(sim.World, 30, 30, amounts);
        GameSupport.Run(sim, sim.World.Content.Military.Combat.LootDecayTicks + 60);
        Assert.Equal(0, GameSupport.Count<LootPile>(sim));
    }

    [Fact]
    public void No_market_day_and_no_merchants_while_enemy_soldiers_are_at_the_marketplace()
    {
        var (sim, market, _) = Towns();
        GameSupport.War(sim);
        var enemy = GameSupport.Soldier(sim, "clubman", 24, 20, player: 2);
        enemy.GetComponent<Order>() = new Order { Kind = OrderKind.Move, Public = true }; // stands there
        GameSupport.Civ(sim).GetComponent<Civilization>().NextMerchantTick = sim.World.Tick + 2;
        var events = GameSupport.Run(sim, sim.World.Content.Economy.Market.IntervalTicks * 2);
        Assert.Contains(events, e => e.Kind == SimEventKind.MarketClosed && e.Player == 1);
        Assert.Equal(0, GameSupport.Count<Merchant>(sim));

        enemy.DeleteEntity(); // they leave
        events = GameSupport.Run(sim, sim.World.Content.Economy.Market.IntervalTicks * 2);
        Assert.DoesNotContain(events, e => e.Kind == SimEventKind.MarketClosed);
        Assert.Equal(1, GameSupport.Count<Merchant>(sim));
    }

    [Fact]
    public void A_raided_caravan_spills_its_cargo_and_what_it_had_at_market()
    {
        var (sim, market, _) = Towns();
        var caravan = sim.World.CreateEntity();
        caravan.AddComponent(new TilePosition(23, 23));
        caravan.AddComponent(new Mover(3, 23, 23, gates: 1));
        caravan.AddComponent(new Owner { Player = Players.Nature });
        caravan.AddComponent(new Merchant
        {
            Player = 1, Market = market.Id, Stage = MerchantStage.Trading, LeaveTick = 99_999,
            Bought = new int[sim.World.Content.Goods.Count],
        });
        caravan.AddComponent(Traders.NewTrader(sim.World.Content));
        caravan.AddComponent(Inventory.Empty(sim.World.Content.Goods.Count));
        GameSupport.SetAmount(caravan, "tools", 4);
        GameSupport.AtMarket(market, caravan, "silver", 6);

        var raider = GameSupport.Soldier(sim, "clubman", 25, 23, player: 2);
        GameSupport.War(sim);
        sim.Enqueue(new Attack([raider.Id], caravan.Id) { Player = 2 });
        var events = GameSupport.Run(sim, 30);
        Assert.Contains(events, e => e.Kind == SimEventKind.CaravanRaided && e.Player == 1);
        Assert.False(sim.World.TryGetEntity(caravan.Id, out _));
        var pile = Assert.Single(World.InIdOrder(sim.World.Store.Query<LootPile>()));
        Assert.Equal(4, GameSupport.Amount(pile, "tools"));
        Assert.Equal(6, GameSupport.Amount(pile, "silver"));
        Assert.Equal(0, GameSupport.Amount(market, "silver")); // the market's stock is still the sum of what traders have there
    }
}
