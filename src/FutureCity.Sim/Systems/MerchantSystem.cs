using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Caravans. Once a player has a marketplace, a foreign caravan sets out from the map edge every visit interval, walks
/// to the marketplace, trades there for a while like any other trader (paying tariffs), and walks back to the edge with
/// what it bought: its coins and goods leave the economy with it. No caravan sets out while enemy soldiers are near the
/// marketplace, and soldiers at war with the town may raid caravans on their way.
/// Civilizations send caravans of their own too (see <see cref="DiplomacySystem"/>): under a trade agreement a treasury's
/// caravan trades at the partner's market and brings the proceeds home; tribute in goods is carried to the receiver's camp.
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
            if (Combat.MarketUnderThreat(world, market)) continue; // no caravan sets out for a town under attack
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
        var trader = Traders.NewTrader(content);
        trader.Coins = content.Economy.Merchants.Coins;
        Create(world, x, y, new Merchant
        {
            Player = player, Market = market.Id, Stage = MerchantStage.Arriving, EdgeX = x, EdgeY = y,
            Bought = new int[content.Goods.Count],
        }, trader, content.MerchantCargo.ToArray());
    }

    /// <summary>
    /// Sends a caravan from <paramref name="from"/>'s home (<paramref name="x"/>, <paramref name="y"/>) to
    /// <paramref name="destination"/> (a marketplace to trade at, or a camp to deliver tribute to) of player
    /// <paramref name="to"/>, carrying <paramref name="cargo"/> and <paramref name="coins"/>.
    /// </summary>
    internal static Entity Send(World world, int from, int to, int x, int y, Entity destination, int[] cargo, int coins, bool tribute)
    {
        var trader = Traders.NewTrader(world.Content);
        trader.Coins = coins;
        return Create(world, x, y, new Merchant
        {
            Player = to, Market = destination.Id, Stage = MerchantStage.Arriving, EdgeX = x, EdgeY = y,
            Bought = new int[world.Content.Goods.Count], From = from, Tribute = tribute,
        }, trader, cargo);
    }

    private static Entity Create(World world, int x, int y, Merchant merchant, Trader trader, int[] cargo)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Mover(world.Content.Citizens.MoveTicksPerTile, x, y, gates: merchant.Player)); // the town lets traders in
        entity.AddComponent(new Owner { Player = Players.Nature });
        entity.AddComponent(merchant);
        entity.AddComponent(trader);
        entity.AddComponent(new Inventory { Amounts = cargo });
        return entity;
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
        bool destinationStands = world.TryGetEntity(merchant.Market, out var destination)
            && (merchant.Tribute ? Stores.IsStore(world, destination) : destination.HasComponent<Market>() && Buildings.IsComplete(destination));

        switch (merchant.Stage)
        {
            case MerchantStage.Arriving:
                if (!destinationStands) { Leave(world, caravan, ref merchant, default); return; }
                if (Buildings.DistanceTo(world, destination, pos.X, pos.Y) <= 1)
                {
                    Movement.Stop(ref mover, pos);
                    if (merchant.Tribute)
                    {
                        Deliver(world, caravan, destination, merchant.Player);
                        Leave(world, caravan, ref merchant, default);
                        return;
                    }
                    var cargo = caravan.GetComponent<Inventory>().Amounts;
                    for (int g = 0; g < cargo.Length; g++)
                    {
                        if (cargo[g] > 0) Markets.Deposit(destination, caravan, g, cargo[g]);
                        cargo[g] = 0;
                    }
                    merchant.Stage = MerchantStage.Trading;
                    merchant.LeaveTick = world.Tick + world.Content.Economy.Merchants.StayTicks;
                    world.Emit(SimEventKind.MerchantsArrived, merchant.Player, caravan.Id, pos.X, pos.Y);
                }
                else if (!mover.Moving)
                {
                    var target = destination.GetComponent<TilePosition>();
                    if (!Movement.SetGoal(world, ref mover, pos, target.X, target.Y)) Leave(world, caravan, ref merchant, default);
                }
                return;
            case MerchantStage.Trading:
                if (destinationStands && world.Tick < merchant.LeaveTick) return;
                Leave(world, caravan, ref merchant, destinationStands ? destination : default);
                world.Emit(SimEventKind.MerchantsLeft, merchant.Player, caravan.Id, pos.X, pos.Y);
                return;
            default:
                if (pos.X == merchant.EdgeX && pos.Y == merchant.EdgeY)
                {
                    GoHome(world, caravan, merchant.From);
                    return;
                }
                if (!mover.Moving && !Movement.SetGoal(world, ref mover, pos, merchant.EdgeX, merchant.EdgeY))
                    GoHome(world, caravan, merchant.From);
                return;
        }
    }

    // A caravan back where it set out from: foreign merchants leave the map with their coins and goods; a civilization's
    // own caravan hands what it brought back to its treasury and public stores.
    private static void GoHome(World world, Entity caravan, int from)
    {
        if (from != 0 && Civics.TryGet(world, from, out var civ))
        {
            var pos = caravan.GetComponent<TilePosition>();
            civ.GetComponent<Trader>().Coins += caravan.GetComponent<Trader>().Coins;
            if (Stores.TryFindNearest(world, from, pos.X, pos.Y, out var store))
                Deliver(world, caravan, store, from);
        }
        caravan.DeleteEntity();
    }

    // Unloads the caravan's cargo into a store.
    private static void Deliver(World world, Entity caravan, Entity store, int player)
    {
        var cargo = caravan.GetComponent<Inventory>().Amounts;
        for (int g = 0; g < cargo.Length; g++)
        {
            if (cargo[g] == 0) continue;
            Stores.Put(store, g, cargo[g]);
            if (caravan.GetComponent<Merchant>().Tribute) Economy.Record(world, player, LedgerEntry.Tribute, cargo[g]);
            cargo[g] = 0;
        }
    }

    // Packs up what the caravan has at the marketplace and heads back to where it came from.
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
        if (merchant.From != 0) mover.Gates = merchant.From; // home through its own town's gates
        Movement.SetGoal(world, ref mover, pos, merchant.EdgeX, merchant.EdgeY);
    }
}
