using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// What agreements between civilizations do, once a year. Tribute in coins passes from treasury to treasury; tribute in
/// goods is taken from the payer's public stores and carried by caravan to the receiver's camp. Tribute that cannot be
/// paid lapses. Partners under a trade agreement each send a caravan with their treasury's surplus and some coins to
/// the other's marketplace, where it trades without tariffs and brings the proceeds home (see <see cref="MerchantSystem"/>).
/// </summary>
public sealed class DiplomacySystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        int year = world.Content.Calendar.TicksPerYear;
        if (world.Tick == 0 || world.Tick % year != 0) return;
        var civs = World.InIdOrder(world.Store.Query<Civilization, Diplomacy, Owner>());
        foreach (var civ in civs)
        {
            int payer = civ.GetComponent<Owner>().Player;
            for (int receiver = 1; receiver <= GameSetup.MaxCivilizations; receiver++)
            {
                var (good, amount) = Relations.Tribute(world, payer, receiver);
                if (amount > 0 && !PayTribute(world, payer, receiver, good, amount))
                {
                    Relations.SetTribute(world, payer, receiver, -1, 0);
                    world.Emit(SimEventKind.TributeLapsed, payer, 0, 0, 0, receiver);
                }
            }
        }
        foreach (var civ in civs)
        {
            int from = civ.GetComponent<Owner>().Player;
            for (int to = 1; to <= GameSetup.MaxCivilizations; to++)
            {
                if (Relations.HaveTradeAgreement(world, from, to)) SendTradeCaravan(world, from, to);
            }
        }
    }

    private static bool PayTribute(World world, int payer, int receiver, int good, int amount)
    {
        if (!Civics.TryGet(world, payer, out var from) || !Civics.TryGet(world, receiver, out var to)) return false;
        if (good < 0)
        {
            ref var treasury = ref from.GetComponent<Trader>();
            if (treasury.Coins < amount) return false;
            treasury.Coins -= amount;
            to.GetComponent<Trader>().Coins += amount;
            return true;
        }
        if (Stores.Total(world, payer, good) < amount || !Bands.TryGetCamp(world, payer, out var home)
            || !Bands.TryGetCamp(world, receiver, out var camp))
            return false;
        var cargo = new int[world.Content.Goods.Count];
        cargo[good] = Stores.Take(world, payer, good, amount);
        var pos = home.GetComponent<TilePosition>();
        MerchantSystem.Send(world, payer, receiver, pos.X, pos.Y, camp, cargo, coins: 0, tribute: true);
        return true;
    }

    // A caravan with the treasury's surplus (what it would offer at market) and a share of its coins.
    private static void SendTradeCaravan(World world, int from, int to)
    {
        if (!Economy.TryGetMarket(world, from, out var home) || !Economy.TryGetMarket(world, to, out var market)
            || !Civics.TryGet(world, from, out var civ))
            return;
        var rules = world.Content.Economy.TradeAgreements;
        var offered = Traders.PlanOf(world, civ).Offered;
        var cargo = new int[offered.Length];
        int room = rules.CaravanCargo;
        for (int g = 0; g < offered.Length && room > 0; g++)
        {
            int take = Stores.Take(world, from, g, Math.Min(room, offered[g]));
            cargo[g] = take;
            room -= take;
        }
        ref var treasury = ref civ.GetComponent<Trader>();
        int coins = treasury.Coins * rules.CaravanCoinsPercent / 100;
        treasury.Coins -= coins;
        var pos = home.GetComponent<TilePosition>();
        MerchantSystem.Send(world, from, to, pos.X, pos.Y, market, cargo, coins, tribute: false);
    }
}
