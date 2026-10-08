using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>A social class. Classes are not assigned: they follow from what people do and what their family owns.</summary>
public enum SocialClass
{
    /// <summary>Farmers, gatherers, labourers and anyone without another class.</summary>
    Peasants,
    /// <summary>Workers at workshops and the mint.</summary>
    Craftsmen,
    /// <summary>Traders working at the marketplace.</summary>
    Merchants,
    /// <summary>Workers at shrines.</summary>
    Clergy,
    /// <summary>Members of families far richer than the average.</summary>
    Nobility,
}

/// <summary>Social classes and happiness of a civilization's people.</summary>
public static class Society
{
    /// <summary>Content ids of the classes (used in conditions as <c>class.&lt;id&gt;</c>), by <see cref="SocialClass"/>.</summary>
    public static readonly string[] ClassIds = Enum.GetValues<SocialClass>().Select(c => c.ToString().ToLowerInvariant()).ToArray();

    /// <summary>Display names of the classes, by <see cref="SocialClass"/>.</summary>
    public static readonly string[] ClassNames = Enum.GetValues<SocialClass>().Select(c => c.ToString()).ToArray();

    /// <summary>
    /// What each household is worth: its coins plus its goods at home and at the market, valued at its own beliefs.
    /// </summary>
    public static Dictionary<int, long> Wealth(World world, int player)
    {
        var wealth = new Dictionary<int, long>();
        foreach (var home in Households.Of(world, player))
        {
            var trader = home.GetComponent<Trader>();
            var stock = home.GetComponent<Inventory>().Amounts;
            long total = trader.Coins;
            for (int g = 0; g < stock.Length; g++) total += (long)(stock[g] + trader.AtMarket[g]) * trader.Beliefs[g];
            wealth[home.Id] = total;
        }
        return wealth;
    }

    /// <summary>Homes of families rich enough to count as nobility.</summary>
    public static HashSet<int> NobleHomes(World world, int player, Dictionary<int, long>? wealth = null)
    {
        wealth ??= Wealth(world, player);
        var nobles = new HashSet<int>();
        if (wealth.Count < 3) return nobles;
        long average = wealth.Values.Sum() / wealth.Count;
        int percent = world.Content.Economy.Classes.NobleWealthPercent;
        foreach (var (home, value) in wealth)
        {
            if (average > 0 && value * 100 >= average * percent) nobles.Add(home);
        }
        return nobles;
    }

    /// <summary>The class of an adult citizen.</summary>
    public static SocialClass ClassOf(World world, Entity unit, HashSet<int> nobleHomes)
    {
        if (nobleHomes.Contains(unit.GetComponent<Citizen>().Home)) return SocialClass.Nobility;
        var order = unit.GetComponent<Order>();
        if (order.Kind != OrderKind.Work || !world.TryGetEntity(order.Target, out var workplace) || !workplace.HasComponent<Building>())
            return SocialClass.Peasants;
        var def = Buildings.TypeOf(world, workplace).Def;
        if (def.Research > 0) return SocialClass.Clergy;
        if (def.Market) return SocialClass.Merchants;
        if (def.Recipe != null || def.Mint != null) return SocialClass.Craftsmen;
        return SocialClass.Peasants;
    }

    /// <summary>Adults in each class, by <see cref="SocialClass"/>.</summary>
    public static int[] Count(World world, int player)
    {
        var counts = new int[ClassIds.Length];
        var nobles = Economy.HasHouseholds(world, player) ? NobleHomes(world, player) : [];
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player != player || !Bands.IsAdult(world, unit.GetComponent<Citizen>())) continue;
            counts[(int)ClassOf(world, unit, nobles)]++;
        }
        return counts;
    }

    /// <summary>Average happiness of the player's people (50 if there is nobody).</summary>
    public static int AverageHappiness(World world, int player)
    {
        long sum = 0;
        int people = 0;
        foreach (var unit in world.Store.Query<Citizen, Owner>().Entities)
        {
            if (unit.GetComponent<Owner>().Player != player) continue;
            sum += unit.GetComponent<Citizen>().Happiness;
            people++;
        }
        return people == 0 ? world.Content.Economy.Happiness.Base : (int)(sum / people);
    }
}
