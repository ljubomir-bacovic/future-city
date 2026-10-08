using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

// Trips to market, work at the marketplace and at the mint.
public sealed partial class OrderSystem
{
    // A trip to market for the worker's family (or for the treasury, if public): take a load of what it can spare to
    // the marketplace, then bring back a load of what it bought there.
    private static void UpdateTrade(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        int capacity = world.Content.Citizens.CarryCapacity;
        if (!world.TryGetEntity(order.Target, out var market) || !market.HasComponent<Market>()
            || !Buildings.IsComplete(market) || market.GetComponent<Owner>().Player != unit.GetComponent<Owner>().Player
            || !TryGetTradeOwner(world, unit, out var trader))
        {
            GiveUp(unit, ref order, citizen);
            return;
        }

        switch (order.Stage)
        {
            case OrderStage.Fetch:
            {
                if (citizen.Carried > 0) { order.Stage = OrderStage.Supply; return; }
                var plan = Traders.PlanOf(world, trader);
                int good = Markets.NextToBring(world, trader, plan, out int amount);
                if (good < 0) { order.Stage = OrderStage.Travel; return; }
                order.Good = good;
                var result = Fetch(world, unit, Math.Min(amount, capacity));
                if (result == Progress.Underway) return;
                order.Stage = result == Progress.Arrived ? OrderStage.Supply : OrderStage.Travel;
                return;
            }
            case OrderStage.Supply:
            {
                if (citizen.Carried == 0) { order.Stage = OrderStage.Travel; return; }
                var result = ApproachEntity(world, unit, market, reach: 1);
                if (result == Progress.Underway) return;
                if (result == Progress.Failed) { order.Stage = OrderStage.Deliver; return; }
                Markets.Deposit(market, trader, citizen.CarriedGood, citizen.Carried);
                citizen.Carried = 0;
                order.Stage = OrderStage.Travel;
                return;
            }
            case OrderStage.Travel:
            {
                var result = ApproachEntity(world, unit, market, reach: 1);
                if (result == Progress.Underway) return;
                if (result == Progress.Failed) { order = default; return; }
                int good = Markets.NextToCollect(world, trader, Traders.PlanOf(world, trader), out int amount);
                if (good < 0) { order = default; return; } // nothing bought: the trip is over
                int take = Math.Min(amount, capacity);
                Markets.Withdraw(market, trader, good, take);
                citizen.CarriedGood = good;
                citizen.Carried = take;
                order.Stage = OrderStage.Deliver;
                return;
            }
            default:
            {
                var result = Deliver(world, unit);
                if (result == Progress.Underway) return;
                if (result == Progress.Failed) { GiveUp(unit, ref order, citizen); return; }
                order = default;
                return;
            }
        }
    }

    // Whom a market trip is for: the worker's family, or the treasury for public work.
    private static bool TryGetTradeOwner(World world, Entity unit, out Entity trader)
    {
        if (Households.TryGetEmployer(world, unit, out trader)) return true;
        return Civics.TryGet(world, unit.GetComponent<Owner>().Player, out trader) && trader.HasComponent<Trader>();
    }

    // Market traders keep the market running; they are paid a commission on each market day they are there.
    private static void UpdateMarketWork(World world, Entity unit, Entity market)
    {
        ref var order = ref unit.GetComponent<Order>();
        if (unit.GetComponent<Citizen>().Carried > 0) { order.Stage = OrderStage.Deliver; return; }
        if (ApproachEntity(world, unit, market, reach: 1) != Progress.Arrived) return;
        order.Stage = OrderStage.Work;
        Labor.Work(world, unit, WorkKind.Trade);
    }

    // Strike coins for the treasury from silver fetched from the public stores.
    private static void UpdateMint(World world, Entity unit, Entity mint, BuildingType type)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        int silver = world.Content.SilverGood;
        int player = unit.GetComponent<Owner>().Player;
        var def = type.Def.Mint!;
        if (citizen.Carried > 0)
        {
            order.Stage = citizen.CarriedGood == silver ? OrderStage.Supply : OrderStage.Deliver;
            return;
        }
        var stock = mint.GetComponent<Inventory>().Amounts;
        if (stock[silver] < def.Silver)
        {
            var wanted = new int[stock.Length];
            wanted[silver] = def.Silver - stock[silver];
            if (TryFetchFirst(world, unit, ref order, wanted)) return;
            if (Civics.HasAutoJobs(world, player)) { order = default; return; } // no silver: find other work
            ApproachEntity(world, unit, mint, reach: 1);
            return;
        }

        if (ApproachEntity(world, unit, mint, reach: 1) != Progress.Arrived) return;
        order.Stage = OrderStage.Work;
        order.Timer += Labor.Work(world, unit, WorkKind.Craft);
        if (order.Timer < def.WorkTicks * Labor.PerTick) return;
        order.Timer = 0;
        stock[silver] -= def.Silver;
        int coins = def.Silver * Economy.CoinsPerSilver(world, player);
        if (!Civics.TryGet(world, player, out var civ)) return;
        civ.GetComponent<Trader>().Coins += coins;
        civ.GetComponent<Civilization>().Minted += coins;
        Economy.Record(world, player, LedgerEntry.Minted, coins);
    }
}
