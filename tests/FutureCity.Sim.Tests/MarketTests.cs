using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Market days: barter, the call auction, beliefs, taxes, merchants and trips to market.</summary>
public class MarketTests
{
    private static (Simulation Sim, Entity Market) MarketTown(bool money)
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.Economy(sim, money);
        return (sim, GameSupport.Marketplace(sim, 20, 20));
    }

    // A family of one with a hut at (x, y).
    private static Entity Family(Simulation sim, int x, int y)
    {
        var home = GameSupport.Home(sim, x, y);
        GameSupport.Adult(sim, x, y + 1).LivesIn(home);
        return home;
    }

    [Fact]
    public void Barter_swaps_goods_when_each_side_has_what_the_other_wants()
    {
        var (sim, market) = MarketTown(money: false);
        var a = Family(sim, 30, 10);
        var b = Family(sim, 30, 14);
        GameSupport.SetAmount(a, "tools", 1);    // a only wants firewood
        GameSupport.SetAmount(b, "tools", 1);    // b is hungry: it wants food
        GameSupport.AtMarket(market, a, "berries", 100);
        GameSupport.AtMarket(market, b, "wood", 50);

        Markets.HoldDay(sim.World, market);

        Assert.True(GameSupport.AtMarket(a, "wood") >= 4); // a wanted firewood
        Assert.True(GameSupport.AtMarket(b, "berries") > 0);
        Assert.Equal(150, market.GetComponent<Inventory>().Amounts.Sum()); // only owners changed
        Assert.True(GameSupport.Civ(sim).GetComponent<Civilization>().Trades > 0);
    }

    [Fact]
    public void Barter_fails_without_a_double_coincidence_of_wants()
    {
        var (sim, market) = MarketTown(money: false);
        var a = Family(sim, 30, 10);
        var b = Family(sim, 30, 14);
        GameSupport.SetAmount(a, "berries", 50);
        GameSupport.SetAmount(b, "berries", 50);
        GameSupport.SetAmount(b, "wood", 10);
        GameSupport.AtMarket(market, a, "stone", 30); // nobody wants stone
        GameSupport.AtMarket(market, b, "clay", 30);  // a wants firewood, not clay

        Markets.HoldDay(sim.World, market);

        Assert.Equal(0, GameSupport.AtMarket(a, "clay"));
        Assert.Equal(30, GameSupport.AtMarket(a, "stone"));
        var state = market.GetComponent<Market>();
        Assert.True(state.BarterWants > state.BarterMatched);
    }

    [Fact]
    public void Without_a_match_sellers_take_the_good_everyone_wants()
    {
        var (sim, market) = MarketTown(money: false);
        var a = Family(sim, 30, 10);  // offers berries, wants firewood
        var b = Family(sim, 30, 14);  // offers wood, fed, wants tools only
        var c = Family(sim, 34, 10);  // hungry, offers nothing
        var d = Family(sim, 34, 14);  // hungry, offers nothing
        foreach (var home in new[] { a, b, c, d }) GameSupport.SetAmount(home, "tools", 1);
        GameSupport.SetAmount(b, "berries", 50);
        GameSupport.AtMarket(market, a, "berries", 100);
        GameSupport.AtMarket(market, b, "wood", 50);
        foreach (var home in new[] { c, d }) GameSupport.SetAmount(home, "wood", 10);

        Markets.HoldDay(sim.World, market);

        Assert.Equal(GameSupport.Good("berries"), market.GetComponent<Market>().Medium);
        Assert.True(GameSupport.AtMarket(a, "wood") > 0);
        Assert.True(GameSupport.AtMarket(b, "berries") > 0); // accepted as payment though b is fed
    }

    // Sellers s1 (belief 10) and s2 (20), buyers b1 (30) and b2 (12), 10 grain each way.
    private static (Entity S1, Entity S2, Entity B1, Entity B2) Auction(Simulation sim, Entity market)
    {
        var world = sim.World;
        int grain = GameSupport.Good("grain");
        var traders = new[] { Family(sim, 30, 10), Family(sim, 30, 14), Family(sim, 34, 10), Family(sim, 34, 14) };
        int[] beliefs = [10, 20, 30, 12];
        var plans = new Dictionary<int, TradePlan>();
        for (int k = 0; k < 4; k++)
        {
            ref var trader = ref GameSupport.TraderOf(traders[k]);
            trader.Beliefs[grain] = beliefs[k];
            trader.Coins = 1000;
            var plan = new TradePlan(world.Content.Goods.Count);
            if (k < 2)
            {
                GameSupport.AtMarket(market, traders[k], "grain", 10);
                plan.Offered[grain] = 10;
            }
            else
            {
                plan.Wanted[grain] = 10;
            }
            plans[traders[k].Id] = plan;
        }
        Markets.Auction(world, market, traders.ToList(), plans, grain);
        return (traders[0], traders[1], traders[2], traders[3]);
    }

    [Fact]
    public void An_auction_trades_at_one_price_and_the_treasury_takes_its_tax()
    {
        var (sim, market) = MarketTown(money: true);
        var (s1, s2, b1, b2) = Auction(sim, market);

        // The first price is halfway between the best bid (30) and the best ask (10): 20. At 20, b1 buys from s1,
        // the cheapest seller; b2 (12) will not pay that much. Supply exceeded demand, so the price falls.
        var state = market.GetComponent<Market>();
        int grain = GameSupport.Good("grain");
        Assert.Equal(10, state.Volume[grain]);
        Assert.InRange(state.Price[grain], 1, 19);
        Assert.Equal(10, GameSupport.AtMarket(b1, "grain"));
        Assert.Equal(0, GameSupport.AtMarket(s1, "grain"));
        Assert.Equal(800, GameSupport.TraderOf(b1).Coins);
        int tax = 200 * GameSupport.Civ(sim).GetComponent<Civilization>().MarketTaxPercent / 100;
        Assert.Equal(1000 + 200 - tax, GameSupport.TraderOf(s1).Coins);
        Assert.Equal(tax, GameSupport.TraderOf(GameSupport.Civ(sim)).Coins);
        Assert.Equal(tax, GameSupport.Civ(sim).GetComponent<Civilization>().Ledger[(int)LedgerEntry.MarketTax]);
        Assert.Equal(1000, GameSupport.TraderOf(b2).Coins);
        Assert.Equal(10, GameSupport.AtMarket(s2, "grain"));
    }

    [Fact]
    public void Beliefs_move_toward_the_price_and_away_from_failure()
    {
        var (sim, market) = MarketTown(money: true);
        var (s1, s2, b1, b2) = Auction(sim, market);
        int grain = GameSupport.Good("grain");
        Assert.True(GameSupport.TraderOf(s1).Beliefs[grain] > 10);  // sold cheap: asks more next time
        Assert.True(GameSupport.TraderOf(b1).Beliefs[grain] < 30);  // paid less than it would have
        Assert.True(GameSupport.TraderOf(s2).Beliefs[grain] < 20);  // sold nothing: asks less
        Assert.True(GameSupport.TraderOf(b2).Beliefs[grain] > 12);  // got nothing: bids more
    }

    [Fact]
    public void A_trip_to_market_carries_surplus_there_and_purchases_home()
    {
        var (sim, market) = MarketTown(money: true);
        var home = GameSupport.Home(sim, 26, 20);
        var member = GameSupport.Adult(sim, 26, 21).LivesIn(home);
        member.GetComponent<Citizen>().ToolWear = 100; // has a tool, so does not take the new one
        GameSupport.SetAmount(home, "berries", 30);
        GameSupport.SetAmount(home, "clay", 25);       // nobody in a family needs clay: surplus
        GameSupport.AtMarket(market, home, "tools", 1); // bought earlier, waiting to be fetched
        UnitOrders.Assign(member, OrderKind.Trade, market, TargetType.Building, market.GetComponent<Building>().Kind);
        ref var order = ref member.GetComponent<Order>();
        order.Stage = OrderStage.Fetch;
        order.Public = false;

        GameSupport.RunUntil(sim, () => member.GetComponent<Order>().Kind == OrderKind.Idle, 500);

        Assert.Equal(10, GameSupport.AtMarket(home, "clay")); // one load carried there
        Assert.Equal(15, GameSupport.Amount(home, "clay"));
        Assert.Equal(1, GameSupport.Amount(home, "tools"));   // and the tools brought back
        Assert.Equal(0, GameSupport.AtMarket(home, "tools"));
        Assert.Equal(10, GameSupport.Amount(market, "clay"));
    }

    [Fact]
    public void Merchants_come_to_a_marketplace_trade_and_leave_with_what_they_bought()
    {
        var (sim, market) = MarketTown(money: true);
        SimEvent arrived = default;
        GameSupport.RunUntil(sim, () =>
        {
            arrived = sim.World.Events.FirstOrDefault(e => e.Kind == SimEventKind.MerchantsArrived);
            return arrived.Kind == SimEventKind.MerchantsArrived;
        }, sim.World.Content.Economy.Merchants.VisitIntervalTicks * 2);
        Assert.True(sim.World.TryGetEntity(arrived.Entity, out var caravan));
        int silver = GameSupport.Good("silver");
        Assert.Equal(sim.World.Content.MerchantCargo[silver], GameSupport.TraderOf(caravan).AtMarket[silver]);
        Assert.Equal(sim.World.Content.MerchantCargo[silver], GameSupport.Amount(market, "silver"));

        // A family sells it grain, paying the tariff on foreign trade.
        var family = Family(sim, 30, 10);
        GameSupport.AtMarket(market, family, "grain", 40);
        GameSupport.TraderOf(family).Beliefs[GameSupport.Good("grain")] = 5;
        Markets.HoldDay(sim.World, market);
        Assert.True(caravan.GetComponent<Merchant>().Bought[GameSupport.Good("grain")] > 0);
        Assert.True(GameSupport.Civ(sim).GetComponent<Civilization>().Ledger[(int)LedgerEntry.Tariffs] > 0);

        // It packs up what it has at the market and walks off with it, then leaves the map.
        GameSupport.RunUntil(sim, () => caravan.GetComponent<Merchant>().Stage == MerchantStage.Leaving, 1000);
        Assert.All(GameSupport.TraderOf(caravan).AtMarket, amount => Assert.Equal(0, amount));
        Assert.True(GameSupport.Amount(caravan, "grain") > 0);
        GameSupport.RunUntil(sim, () => !sim.World.TryGetEntity(caravan.Id, out _), 5000);
    }

    [Fact]
    public void Merchants_sell_dear_goods_at_world_prices_and_pull_prices_down()
    {
        var (sim, market) = MarketTown(money: true);
        int tools = GameSupport.Good("tools");
        var family = Family(sim, 30, 10);
        GameSupport.TraderOf(family).Coins = 5000;
        GameSupport.TraderOf(family).Beliefs[tools] = 1000; // tools are scarce at home
        var caravan = sim.World.CreateEntity();
        caravan.AddComponent(new Merchant { Player = Players.Human, Market = market.Id, Stage = MerchantStage.Trading, Bought = new int[sim.World.Content.Goods.Count] });
        caravan.AddComponent(Traders.NewTrader(sim.World.Content));
        caravan.AddComponent(Inventory.Empty(sim.World.Content.Goods.Count));
        GameSupport.AtMarket(market, caravan, "tools", 5);

        Markets.HoldDay(sim.World, market);

        int worldAsk = sim.World.Content.Goods[tools].Value * (100 + sim.World.Content.Economy.Merchants.SellMarkupPercent) / 100;
        Assert.True(GameSupport.AtMarket(family, "tools") > 0);
        Assert.InRange(market.GetComponent<Market>().Price[tools], worldAsk, 1000);
        Assert.True(GameSupport.TraderOf(family).Beliefs[tools] < 1000);
    }

    [Fact]
    public void Setting_taxes_and_coin_quality_respects_the_limits()
    {
        var (sim, _) = MarketTown(money: true);
        var civ = GameSupport.Civ(sim);
        sim.Enqueue(new SetTaxes(20, 8, 30) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        Assert.Equal((20, 8, 30), (civ.GetComponent<Civilization>().TributePercent, civ.GetComponent<Civilization>().MarketTaxPercent,
            civ.GetComponent<Civilization>().TariffPercent));

        sim.Enqueue(new SetTaxes(99, 8, 30) { Player = Players.Human }); // tribute above the maximum: refused
        sim.Enqueue(new SetCoinQuality(10) { Player = Players.Human });  // below the minimum: refused
        GameSupport.Run(sim, 1);
        Assert.Equal(20, civ.GetComponent<Civilization>().TributePercent);
        Assert.Equal(100, civ.GetComponent<Civilization>().CoinQuality);

        sim.Enqueue(new SetCoinQuality(50) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        Assert.Equal(50, civ.GetComponent<Civilization>().CoinQuality);
    }
}
