using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// Which stage a civilization's economy has reached, and totals across the treasury and all households.
/// Before Private property everything is in the shared (public) stores, so these totals equal <see cref="Stores"/>.
/// </summary>
public static class Economy
{
    /// <summary>Whether families own their goods (Private property is established).</summary>
    public static bool HasHouseholds(World world, int player) => HasEffect(world, player, e => e.Households);

    /// <summary>Whether the economy runs on coins (Coinage is established).</summary>
    public static bool HasMoney(World world, int player) => HasEffect(world, player, e => e.Money);

    /// <summary>Whether guilds regulate the crafts.</summary>
    public static bool HasGuilds(World world, int player) => HasEffect(world, player, e => e.Guilds);

    private static bool HasEffect(World world, int player, Func<InstitutionEffects, bool> effect)
    {
        if (!Civics.TryGet(world, player, out var civ)) return false;
        var established = civ.GetComponent<Civilization>().Institutions;
        for (int i = 0; i < established.Length; i++)
        {
            if (established[i] != 0 && world.Content.Institutions[i].Def.Effects is { } e && effect(e)) return true;
        }
        return false;
    }

    /// <summary>
    /// Units of each good the civilization holds: public stores, households' homes, and both their goods at the
    /// marketplace. Goods being carried, in construction sites and in workshops are not counted.
    /// </summary>
    public static int[] Holdings(World world, int player)
    {
        var totals = Stores.Totals(world, player);
        foreach (var home in Households.Of(world, player))
        {
            var amounts = home.GetComponent<Inventory>().Amounts;
            for (int g = 0; g < totals.Length; g++) totals[g] += amounts[g];
        }
        foreach (var trader in OwnTraders(world, player))
        {
            var atMarket = trader.GetComponent<Trader>().AtMarket;
            for (int g = 0; g < totals.Length; g++) totals[g] += atMarket[g];
        }
        return totals;
    }

    /// <summary>Meals the civilization holds (public stores and households).</summary>
    public static int Meals(World world, int player) => Stores.MealsIn(world, Holdings(world, player));

    /// <summary>Coins held by the treasury and the households: the money supply.</summary>
    public static int MoneySupply(World world, int player)
    {
        long coins = 0;
        foreach (var trader in OwnTraders(world, player)) coins += trader.GetComponent<Trader>().Coins;
        return (int)Math.Min(int.MaxValue, coins);
    }

    /// <summary>The treasury (civilization entity) and the households of a player, in id order.</summary>
    public static List<Entity> OwnTraders(World world, int player)
    {
        var traders = new List<Entity>();
        foreach (var entity in World.InIdOrder(world.Store.Query<Trader, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player == player && !entity.HasComponent<Merchant>()) traders.Add(entity);
        }
        return traders;
    }

    /// <summary>
    /// Takes food worth <paramref name="nutrition"/> points from the public stores, then from households in id order
    /// (used for births and feasts). Returns the points taken.
    /// </summary>
    public static int TakeFood(World world, int player, int nutrition)
    {
        int taken = Stores.TakeFood(world, player, nutrition);
        foreach (var home in Households.Of(world, player))
        {
            if (taken >= nutrition) break;
            taken += Stores.TakeFoodFrom(world, home, nutrition - taken);
        }
        return taken;
    }

    /// <summary>The player's completed marketplace (the lowest id if several).</summary>
    public static bool TryGetMarket(World world, int player, out Entity market)
    {
        foreach (var entity in World.InIdOrder(world.Store.Query<Market, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player != player || !Buildings.IsComplete(entity)) continue;
            market = entity;
            return true;
        }
        market = default;
        return false;
    }

    /// <summary>
    /// The going price of a good: the marketplace's last price, or before any trade the treasury's belief.
    /// </summary>
    public static int Price(World world, int player, int good)
    {
        if (TryGetMarket(world, player, out var market))
        {
            int price = market.GetComponent<Market>().Price[good];
            if (price > 0) return price;
        }
        if (Civics.TryGet(world, player, out var civ) && civ.TryGetComponent<Trader>(out var treasury))
            return treasury.Beliefs[good];
        return world.Content.Goods[good].Value;
    }

    /// <summary>Coins struck from one unit of silver at the player's current coin quality.</summary>
    public static int CoinsPerSilver(World world, int player)
    {
        int quality = Civics.TryGet(world, player, out var civ) ? civ.GetComponent<Civilization>().CoinQuality : 100;
        return world.Content.Economy.Money.CoinsPerSilver * 100 / Math.Max(1, quality);
    }

    /// <summary>Records an amount in the treasury's accounts for this year.</summary>
    internal static void Record(World world, int player, LedgerEntry entry, int amount)
    {
        if (Civics.TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().Ledger[(int)entry] += amount;
    }
}
