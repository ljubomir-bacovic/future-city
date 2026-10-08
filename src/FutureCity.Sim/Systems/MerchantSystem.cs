using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Foreign merchants. Once a player has a marketplace, a caravan sets out from the map edge every visit interval,
/// walks to the marketplace, trades there for a while like any other trader (paying tariffs), and walks back to the
/// edge with what it bought. Its coins and goods leave the economy with it.
/// </summary>
public sealed class MerchantSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Economy.Merchants;
        foreach (var civEntity in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            int player = civEntity.GetComponent<Owner>().Player;
            if (!Economy.TryGetMarket(world, player, out var market)) continue;
            ref var civ = ref civEntity.GetComponent<Civilization>();
            if (civ.NextMerchantTick == 0)
            {
                civ.NextMerchantTick = world.Tick + rules.VisitIntervalTicks / 2; // news of a new market travels
                continue;
            }
            if (world.Tick < civ.NextMerchantTick) continue;
            civ.NextMerchantTick = world.Tick + rules.VisitIntervalTicks;
            SpawnCaravan(world, player, market);
        }

        foreach (var caravan in World.InIdOrder(world.Store.Query<Merchant, TilePosition, Mover>()))
            UpdateCaravan(world, caravan);
    }

    private static void SpawnCaravan(World world, int player, Entity market)
    {
        var pos = market.GetComponent<TilePosition>();
        if (!TryFindEdge(world, pos, out int x, out int y)) return;
        var content = world.Content;
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Mover(content.Citizens.MoveTicksPerTile, x, y, gates: player)); // the town lets traders in
        entity.AddComponent(new Owner { Player = Players.Nature });
        entity.AddComponent(new Merchant
        {
            Player = player, Market = market.Id, Stage = MerchantStage.Arriving, EdgeX = x, EdgeY = y,
            Bought = new int[content.Goods.Count],
        });
        var trader = Traders.NewTrader(content);
        trader.Coins = content.Economy.Merchants.Coins;
        entity.AddComponent(trader);
        entity.AddComponent(new Inventory { Amounts = content.MerchantCargo.ToArray() });
    }

    // A walkable map-edge tile from which the marketplace can be reached, starting the search at a random point.
    private static bool TryFindEdge(World world, TilePosition target, out int x, out int y)
    {
        var map = world.Map;
        int perimeter = 2 * (map.Width + map.Height) - 4;
        int start = world.Rng.NextInt(perimeter);
        for (int k = 0; k < perimeter; k++)
        {
            (x, y) = EdgeTile(map.Width, map.Height, (start + k) % perimeter);
            if (world.CanReach(x, y, target.X, target.Y)) return true;
        }
        x = y = 0;
        return false;
    }

    private static (int X, int Y) EdgeTile(int width, int height, int i)
    {
        if (i < width) return (i, 0);
        i -= width;
        if (i < height - 1) return (width - 1, i + 1);
        i -= height - 1;
        if (i < width - 1) return (width - 2 - i, height - 1);
        i -= width - 1;
        return (0, height - 2 - i);
    }

    private static void UpdateCaravan(World world, Entity caravan)
    {
        ref var merchant = ref caravan.GetComponent<Merchant>();
        ref var mover = ref caravan.GetComponent<Mover>();
        var pos = caravan.GetComponent<TilePosition>();
        bool marketStands = world.TryGetEntity(merchant.Market, out var market) && market.HasComponent<Market>()
                            && Buildings.IsComplete(market);

        switch (merchant.Stage)
        {
            case MerchantStage.Arriving:
                if (!marketStands) { Leave(world, caravan, ref merchant, default); return; }
                if (Buildings.DistanceTo(world, market, pos.X, pos.Y) <= 1)
                {
                    Movement.Stop(ref mover, pos);
                    var cargo = caravan.GetComponent<Inventory>().Amounts;
                    for (int g = 0; g < cargo.Length; g++)
                    {
                        if (cargo[g] > 0) Markets.Deposit(market, caravan, g, cargo[g]);
                        cargo[g] = 0;
                    }
                    merchant.Stage = MerchantStage.Trading;
                    merchant.LeaveTick = world.Tick + world.Content.Economy.Merchants.StayTicks;
                    world.Emit(SimEventKind.MerchantsArrived, merchant.Player, caravan.Id, pos.X, pos.Y);
                }
                else if (!mover.Moving)
                {
                    var target = market.GetComponent<TilePosition>();
                    if (!Movement.SetGoal(world, ref mover, pos, target.X, target.Y)) Leave(world, caravan, ref merchant, default);
                }
                return;
            case MerchantStage.Trading:
                if (marketStands && world.Tick < merchant.LeaveTick) return;
                Leave(world, caravan, ref merchant, marketStands ? market : default);
                world.Emit(SimEventKind.MerchantsLeft, merchant.Player, caravan.Id, pos.X, pos.Y);
                return;
            default:
                if (pos.X == merchant.EdgeX && pos.Y == merchant.EdgeY)
                {
                    caravan.DeleteEntity(); // gone abroad, with its coins and goods
                    return;
                }
                if (!mover.Moving && !Movement.SetGoal(world, ref mover, pos, merchant.EdgeX, merchant.EdgeY))
                    caravan.DeleteEntity();
                return;
        }
    }

    // Packs up what the caravan has at the marketplace and heads back to the edge.
    private static void Leave(World world, Entity caravan, ref Merchant merchant, Entity market)
    {
        var atMarket = caravan.GetComponent<Trader>().AtMarket;
        var cargo = caravan.GetComponent<Inventory>().Amounts;
        for (int g = 0; g < atMarket.Length; g++)
        {
            if (atMarket[g] == 0) continue;
            cargo[g] += atMarket[g];
            if (!market.IsNull) Markets.Withdraw(market, caravan, g, atMarket[g]);
            else atMarket[g] = 0;
        }
        merchant.Stage = MerchantStage.Leaving;
        var pos = caravan.GetComponent<TilePosition>();
        ref var mover = ref caravan.GetComponent<Mover>();
        Movement.SetGoal(world, ref mover, pos, merchant.EdgeX, merchant.EdgeY);
    }
}
