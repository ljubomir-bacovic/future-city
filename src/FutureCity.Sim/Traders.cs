using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim;

/// <summary>
/// What a trader holds, wants and can spare, by good. "Offered" and "wanted" are totals; only goods already at the
/// marketplace can actually be sold there.
/// </summary>
public sealed class TradePlan
{
    internal TradePlan(int goods)
    {
        Held = new int[goods];
        Target = new int[goods];
        Offered = new int[goods];
        Wanted = new int[goods];
    }

    /// <summary>Units held at home (or in public stores) and at the marketplace.</summary>
    public int[] Held { get; }
    /// <summary>Units the trader wants to keep.</summary>
    public int[] Target { get; }
    /// <summary>Units the trader would sell.</summary>
    public int[] Offered { get; }
    /// <summary>Units the trader would buy.</summary>
    public int[] Wanted { get; }
}

/// <summary>
/// The traders of the economy: households (on their homes), the treasury (on the civilization entity) and visiting
/// merchants. Each wants to keep a stock of goods (food in meals, firewood and tools per member, a crafter's inputs,
/// the treasury's reserves and building materials), offers what is well beyond it, and values goods by its beliefs.
/// </summary>
public static class Traders
{
    /// <summary>A trader with no coins and beliefs at the goods' starting values.</summary>
    internal static Trader NewTrader(ContentDatabase content) => new()
    {
        Coins = 0,
        AtMarket = new int[content.Goods.Count],
        Beliefs = content.Goods.Select(g => g.Value).ToArray(),
    };

    /// <summary>Whether the trader is a player's treasury.</summary>
    public static bool IsTreasury(Entity trader) => trader.HasComponent<Civilization>();

    /// <summary>Whether the trader is a foreign merchant caravan.</summary>
    public static bool IsMerchant(Entity trader) => trader.HasComponent<Merchant>();

    /// <summary>The player a trader belongs to, or for a merchant the player whose market it visits.</summary>
    public static int PlayerOf(Entity trader) =>
        IsMerchant(trader) ? trader.GetComponent<Merchant>().Player : trader.GetComponent<Owner>().Player;

    /// <summary>Goods the trader holds away from the marketplace: at home, in public stores, or on a caravan.</summary>
    public static int[] HomeStock(World world, Entity trader)
    {
        if (IsTreasury(trader)) return Stores.Totals(world, trader.GetComponent<Owner>().Player);
        return trader.TryGetComponent<Inventory>(out var inventory) ? (int[])inventory.Amounts.Clone()
            : new int[world.Content.Goods.Count];
    }

    /// <summary>What the trader holds, wants and offers now.</summary>
    /// <param name="world">The world.</param>
    /// <param name="trader">A household, treasury or merchant.</param>
    /// <param name="members">Household members by home id (<see cref="Households.Members"/>), to avoid recounting.</param>
    public static TradePlan PlanOf(World world, Entity trader, Dictionary<int, List<Entity>>? members = null)
    {
        var content = world.Content;
        int goods = content.Goods.Count;
        var plan = new TradePlan(goods);
        var home = HomeStock(world, trader);
        var atMarket = trader.GetComponent<Trader>().AtMarket;
        for (int g = 0; g < goods; g++) plan.Held[g] = home[g] + atMarket[g];

        if (IsMerchant(trader))
        {
            var bought = trader.GetComponent<Merchant>().Bought;
            for (int g = 0; g < goods; g++)
            {
                plan.Target[g] = content.MerchantWants[g];
                plan.Wanted[g] = Math.Max(0, content.MerchantWants[g] - bought[g]);
                plan.Offered[g] = content.MerchantWants[g] > 0 ? 0 : plan.Held[g];
            }
            return plan;
        }

        int meals = IsTreasury(trader) ? TreasuryTargets(world, trader, plan.Target)
            : HouseholdTargets(world, trader, plan.Target, members);
        int surplus = content.Economy.Households.SurplusPercent;
        for (int g = 0; g < goods; g++)
        {
            plan.Wanted[g] = Math.Max(0, plan.Target[g] - plan.Held[g]);
            plan.Offered[g] = Math.Max(0, plan.Held[g] - plan.Target[g] * surplus / 100);
        }
        PlanFood(world, trader.GetComponent<Trader>().Beliefs, plan, meals, surplus);
        return plan;
    }

    // Food is wanted and offered in meals: a family short of meals wants the food that is cheapest per meal, and
    // offers its most valuable food first when it has far more than it needs.
    private static void PlanFood(World world, int[] beliefs, TradePlan plan, int mealsTarget, int surplus)
    {
        var content = world.Content;
        int held = Stores.MealsIn(world, plan.Held);
        var foods = content.FoodGoods.OrderBy(f => (long)beliefs[f] * 100 / content.Nutrition(f)).ThenBy(f => f).ToArray();
        foreach (int f in foods)
        {
            // Food goods also needed as materials or inputs (grain for a miller) keep their own target.
            plan.Wanted[f] = Math.Max(0, plan.Target[f] - plan.Held[f]);
            plan.Offered[f] = 0;
        }
        if (held < mealsTarget && foods.Length > 0)
        {
            int cheapest = foods[0];
            int nutrition = content.Nutrition(cheapest);
            plan.Wanted[cheapest] = Math.Max(plan.Wanted[cheapest], ((mealsTarget - held) * 100 + nutrition - 1) / nutrition);
        }
        long excess = (long)held * 100 - (long)mealsTarget * surplus; // in nutrition points
        for (int i = foods.Length - 1; i >= 0 && excess > 0; i--)
        {
            int f = foods[i];
            int nutrition = content.Nutrition(f);
            int spare = Math.Max(0, plan.Held[f] - plan.Target[f] * surplus / 100);
            int units = (int)Math.Min(spare, (excess + nutrition - 1) / nutrition);
            plan.Offered[f] = units;
            excess -= (long)units * nutrition;
        }
    }

    // A family keeps firewood and tools per member, a crafter's inputs, and meals per member.
    private static int HouseholdTargets(World world, Entity home, int[] target, Dictionary<int, List<Entity>>? members)
    {
        var content = world.Content;
        var rules = content.Economy.Households;
        var people = members != null ? members.GetValueOrDefault(home.Id) ?? []
            : Households.Members(world, home.GetComponent<Owner>().Player).GetValueOrDefault(home.Id) ?? [];
        for (int g = 0; g < target.Length; g++) target[g] = content.Goods[g].HouseholdTarget * people.Count;
        foreach (var member in people)
        {
            var order = member.GetComponent<Order>();
            if (order.Kind != OrderKind.Work || order.Public || !world.TryGetEntity(order.Target, out var workplace)
                || !workplace.HasComponent<Building>())
                continue;
            var type = Buildings.TypeOf(world, workplace);
            for (int g = 0; g < target.Length; g++) target[g] += type.Inputs[g] * rules.InputBatches;
        }
        return rules.FoodTargetPerMember * people.Count;
    }

    // The treasury keeps a food reserve, materials per person and for building sites, and silver for the mint.
    private static int TreasuryTargets(World world, Entity civ, int[] target)
    {
        var content = world.Content;
        int player = civ.GetComponent<Owner>().Player;
        int population = Bands.CensusOf(world, player).Total;
        for (int g = 0; g < target.Length; g++) target[g] = content.Goods[g].TargetPerCapita * population;
        foreach (var site in world.Store.Query<Construction, Building, Owner>().Entities)
        {
            if (site.GetComponent<Owner>().Player != player) continue;
            var missing = Buildings.MissingMaterials(world, site);
            for (int g = 0; g < target.Length; g++) target[g] += missing[g];
        }
        target[content.SilverGood] += content.Economy.Treasury.SilverTarget;
        return content.Economy.Treasury.FoodTargetPerCapita * population;
    }

    /// <summary>
    /// What a unit of a good is worth to the trader when deciding what to work at: its belief, more if the family is
    /// short of it, much less if it already has plenty and there is no market to sell the rest at.
    /// </summary>
    public static int Value(World world, Entity trader, TradePlan plan, int good, bool canSell)
    {
        int value = trader.GetComponent<Trader>().Beliefs[good];
        if (plan.Wanted[good] > 0) return value * (100 + world.Content.Economy.Market.NeedPremiumPercent) / 100;
        if (!canSell && plan.Held[good] >= plan.Target[good]) return value / 4;
        return value;
    }

    /// <summary>The lowest price at which the trader sells a unit of a good today.</summary>
    public static int AskLimit(World world, Entity trader, TradePlan plan, int good)
    {
        var rules = world.Content.Economy;
        int player = PlayerOf(trader);
        if (IsMerchant(trader))
            return Math.Max(1, (int)((long)WorldPrice(world, player, good) * (100 + rules.Merchants.SellMarkupPercent) / 100));
        int belief = trader.GetComponent<Trader>().Beliefs[good];
        int ask = plan.Held[good] > 2 * plan.Target[good] ? belief * (100 - rules.Market.SurplusDiscountPercent) / 100 : belief;
        if (Economy.HasGuilds(world, player)) ask = Math.Max(ask, GuildFloor(world, trader, good));
        return Math.Max(1, ask);
    }

    /// <summary>The highest price at which the trader buys a unit of a good today.</summary>
    public static int BidLimit(World world, Entity trader, TradePlan plan, int good)
    {
        var rules = world.Content.Economy;
        if (IsMerchant(trader))
            return Math.Max(1, (int)((long)WorldPrice(world, PlayerOf(trader), good) * (100 - rules.Merchants.BuyDiscountPercent) / 100));
        int belief = trader.GetComponent<Trader>().Beliefs[good];
        int bid = plan.Held[good] * 2 < plan.Target[good] ? belief * (100 + rules.Market.NeedPremiumPercent) / 100 : belief;
        return Math.Max(1, bid);
    }

    /// <summary>
    /// A good's world price in the player's coins: its value in silver terms, so debased coins buy less abroad.
    /// </summary>
    public static int WorldPrice(World world, int player, int good)
    {
        int quality = Emergence.Civics.TryGet(world, player, out var civ) ? civ.GetComponent<Civilization>().CoinQuality : 100;
        return (int)((long)world.Content.Goods[good].Value * 100 / Math.Max(1, quality));
    }

    // Under guilds, crafters never sell their product below the cost of its inputs plus the guild margin.
    private static int GuildFloor(World world, Entity trader, int good)
    {
        var content = world.Content;
        var beliefs = trader.GetComponent<Trader>().Beliefs;
        foreach (var type in content.Buildings)
        {
            if (!type.IsWorkshop || type.Outputs[good] <= 0) continue;
            long cost = 0;
            for (int g = 0; g < beliefs.Length; g++) cost += (long)type.Inputs[g] * beliefs[g];
            return (int)(cost * (100 + content.Economy.Guilds.MarginPercent) / 100 / type.Outputs[good]);
        }
        return 0;
    }
}
