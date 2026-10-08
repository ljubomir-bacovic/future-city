using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// Market days. Goods at the marketplace belong to whoever carried them there (<see cref="Trader.AtMarket"/>); a
/// trade only changes who owns them, and owners carry them home later. The marketplace's inventory is always the sum
/// of everyone's goods there.
/// <para>
/// Before coins, traders barter: a trade needs a double coincidence of wants (each side has what the other wants),
/// at a ratio between both sides' values. Failing that, a seller accepts the good most people want that day (often
/// grain) because it can pass it on: the beginning of commodity money.
/// </para>
/// <para>
/// With coins, each good trades at the market's going price. Buyers bid for what they want, as much as their coins
/// buy, up to their limit; sellers offer their surplus down to theirs. Whatever both sides accept at the going price
/// trades at it, and then the price moves up when more was wanted than offered and down when more was offered.
/// Nobody sets prices: they follow supply and demand, and the amount of money people hold. Every trader's beliefs
/// about what goods are worth follow the prices it hears.
/// </para>
/// </summary>
public static class Markets
{
    /// <summary>Puts goods carried to the marketplace into the trader's goods there.</summary>
    public static void Deposit(Entity market, Entity trader, int good, int amount)
    {
        market.GetComponent<Inventory>().Amounts[good] += amount;
        trader.GetComponent<Trader>().AtMarket[good] += amount;
    }

    /// <summary>Takes goods of the trader out of the marketplace (to carry them away).</summary>
    public static void Withdraw(Entity market, Entity trader, int good, int amount)
    {
        market.GetComponent<Inventory>().Amounts[good] -= amount;
        trader.GetComponent<Trader>().AtMarket[good] -= amount;
    }

    /// <summary>The good of which the trader has the most to bring to market (held at home and offered), or -1.</summary>
    public static int NextToBring(World world, Entity trader, TradePlan plan, out int amount)
    {
        var home = Traders.HomeStock(world, trader);
        var atMarket = trader.GetComponent<Trader>().AtMarket;
        int best = -1;
        amount = 0;
        for (int g = 0; g < home.Length; g++)
        {
            int bring = Math.Min(home[g], plan.Offered[g] - atMarket[g]);
            if (bring <= amount) continue;
            best = g;
            amount = bring;
        }
        return best;
    }

    /// <summary>The good of which the trader has the most at the marketplace that it is not selling, or -1.</summary>
    public static int NextToCollect(World world, Entity trader, TradePlan plan, out int amount)
    {
        var atMarket = trader.GetComponent<Trader>().AtMarket;
        int best = -1;
        amount = 0;
        for (int g = 0; g < atMarket.Length; g++)
        {
            int collect = atMarket[g] - Math.Min(atMarket[g], plan.Offered[g]);
            if (collect <= amount) continue;
            best = g;
            amount = collect;
        }
        return best;
    }

    /// <summary>The player's treasury and households, and the merchants trading at this marketplace, in id order.</summary>
    public static List<Entity> Participants(World world, Entity market)
    {
        int player = market.GetComponent<Owner>().Player;
        var traders = new List<Entity>();
        foreach (var entity in World.InIdOrder(world.Store.Query<Trader>()))
        {
            if (entity.TryGetComponent<Merchant>(out var merchant))
            {
                if (merchant.Market == market.Id && merchant.Stage == MerchantStage.Trading) traders.Add(entity);
            }
            else if (entity.TryGetComponent<Owner>(out var owner) && owner.Player == player)
            {
                traders.Add(entity);
            }
        }
        return traders;
    }

    /// <summary>Holds one market day: barter or auctions, belief updates and the day's record.</summary>
    public static void HoldDay(World world, Entity market)
    {
        int player = market.GetComponent<Owner>().Player;
        var traders = Participants(world, market);
        var members = Households.Members(world, player);
        var plans = traders.ToDictionary(t => t.Id, t => Traders.PlanOf(world, t, members));
        ref var state = ref market.GetComponent<Market>();
        state.Commission = 0;
        if (Economy.HasMoney(world, player))
        {
            state.Medium = -1; // coins now
            for (int g = 0; g < world.Content.Goods.Count; g++) Auction(world, market, traders, plans, g);
        }
        else
        {
            Barter(world, market, traders, plans);
        }
        RecordDay(world, market);
    }

    // ---- Barter ----

    internal static void Barter(World world, Entity market, List<Entity> traders, Dictionary<int, TradePlan> plans)
    {
        var content = world.Content;
        int goods = content.Goods.Count;
        int player = market.GetComponent<Owner>().Player;
        ref var state = ref market.GetComponent<Market>();
        var offers = traders.ToDictionary(t => t.Id, t => Offers(t, plans[t.Id]));
        var wants = traders.ToDictionary(t => t.Id, t => (int[])plans[t.Id].Wanted.Clone());
        var sold = traders.ToDictionary(t => t.Id, _ => new bool[goods]);
        // Barter teaches little about prices: values move toward the ratios goods were swapped at, and a seller whose
        // goods were wanted but not taken asks less. A want nobody could meet says nothing about worth.
        var wantedByAnyone = new bool[goods];
        foreach (var t in traders)
        {
            for (int g = 0; g < goods; g++) wantedByAnyone[g] |= wants[t.Id][g] > 0;
        }
        state.BarterWants = 0;
        state.BarterMatched = 0;
        Array.Clear(state.Volume);
        state.Medium = MostWanted(traders, wants);

        foreach (var a in traders)
        {
            for (int g = 0; g < goods; g++)
            {
                if (wants[a.Id][g] <= 0) continue;
                state.BarterWants++;
                bool matched = false;
                foreach (var b in traders)
                {
                    if (b.Id == a.Id || offers[b.Id][g] <= 0 || wants[a.Id][g] <= 0) continue;
                    int x = Counterpart(offers[a.Id], wants[b.Id], g);
                    bool medium = false;
                    if (x < 0 && state.Medium >= 0 && state.Medium != g && offers[a.Id][state.Medium] > 0)
                    {
                        x = state.Medium; // b takes what everybody wants, to pass on later
                        medium = true;
                    }
                    if (x < 0) continue;
                    int valueG = Math.Max(1, (Belief(a, g) + Belief(b, g)) / 2);
                    int valueX = Math.Max(1, (Belief(a, x) + Belief(b, x)) / 2);
                    int qg = Math.Min(wants[a.Id][g], offers[b.Id][g]);
                    int qx = (int)(((long)qg * valueG + valueX - 1) / valueX);
                    int maxX = medium ? offers[a.Id][x] : Math.Min(offers[a.Id][x], wants[b.Id][x]);
                    if (qx > maxX)
                    {
                        qx = maxX;
                        qg = (int)((long)qx * valueX / valueG);
                    }
                    if (qg <= 0 || qx <= 0) continue;

                    Move(a, b, x, qx); // a gives x
                    Move(b, a, g, qg); // b gives g
                    offers[a.Id][x] -= qx;
                    wants[b.Id][x] = Math.Max(0, wants[b.Id][x] - qx);
                    offers[b.Id][g] -= qg;
                    wants[a.Id][g] -= qg;
                    sold[a.Id][x] = sold[b.Id][g] = true;
                    state.Volume[g] += qg;
                    state.Volume[x] += qx;
                    state.Price[g] = valueG;
                    state.Price[x] = valueX;
                    Pull(world, a, g, valueG); Pull(world, b, g, valueG);
                    Pull(world, a, x, valueX); Pull(world, b, x, valueX);
                    matched = true;
                    if (Civics.TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().Trades++;
                }
                if (matched) state.BarterMatched++;
            }
        }
        foreach (var t in traders)
        {
            for (int g = 0; g < goods; g++)
            {
                if (offers[t.Id][g] > 0 && !sold[t.Id][g] && wantedByAnyone[g]) Step(world, t, g, up: false);
            }
        }
    }

    // The good wanted by the most traders today (ties: the lowest index), or -1.
    private static int MostWanted(List<Entity> traders, Dictionary<int, int[]> wants)
    {
        int best = -1, bestCount = 0;
        int goods = wants.Count == 0 ? 0 : wants.Values.First().Length;
        for (int g = 0; g < goods; g++)
        {
            int count = traders.Count(t => wants[t.Id][g] > 0);
            if (count <= bestCount) continue;
            best = g;
            bestCount = count;
        }
        return best;
    }

    // What a trader actually offers at the market: its surplus, as far as it has carried it there.
    private static int[] Offers(Entity trader, TradePlan plan)
    {
        var atMarket = trader.GetComponent<Trader>().AtMarket;
        var offers = new int[atMarket.Length];
        for (int g = 0; g < offers.Length; g++) offers[g] = Math.Min(atMarket[g], plan.Offered[g]);
        return offers;
    }

    // A good the first trader offers that the second wants, other than the one being traded for (the largest match).
    private static int Counterpart(int[] offersOfA, int[] wantsOfB, int except)
    {
        int best = -1, bestAmount = 0;
        for (int x = 0; x < offersOfA.Length; x++)
        {
            int amount = Math.Min(offersOfA[x], wantsOfB[x]);
            if (x == except || amount <= bestAmount) continue;
            best = x;
            bestAmount = amount;
        }
        return best;
    }

    // ---- Auctions ----

    private readonly record struct Offer(Entity Trader, int Quantity, int Limit);

    // One good's market with coins. Buyers who can pay bid what they want, as much as their coins buy at today's
    // price, up to their limit; sellers ask with their limit. Everything that both sides accept at today's price trades at it:
    // the highest bidders and the lowest askers first. Then the price moves with the gap between demand and supply.
    internal static void Auction(World world, Entity market, List<Entity> traders, Dictionary<int, TradePlan> plans, int good)
    {
        ref var state = ref market.GetComponent<Market>();
        var bids = new List<Offer>();
        var asks = new List<Offer>();
        foreach (var t in traders)
        {
            var plan = plans[t.Id];
            int sell = Math.Min(plan.Offered[good], t.GetComponent<Trader>().AtMarket[good]);
            if (sell > 0) asks.Add(new Offer(t, sell, Traders.AskLimit(world, t, plan, good)));
            int limit = Traders.BidLimit(world, t, plan, good);
            if (plan.Wanted[good] > 0 && t.GetComponent<Trader>().Coins >= limit) bids.Add(new Offer(t, plan.Wanted[good], limit));
        }
        state.Volume[good] = 0;
        if (bids.Count == 0 || asks.Count == 0) return; // nobody (with coins) on the other side: nothing to learn

        int price = state.Price[good] > 0 ? state.Price[good] : OpeningPrice(bids, asks);
        bids.Sort((x, y) => x.Limit != y.Limit ? y.Limit.CompareTo(x.Limit) : x.Trader.Id.CompareTo(y.Trader.Id));
        asks.Sort((x, y) => x.Limit != y.Limit ? x.Limit.CompareTo(y.Limit) : x.Trader.Id.CompareTo(y.Trader.Id));
        var demand = bids.Where(b => b.Limit >= price)
            .Select(b => b with { Quantity = Math.Min(b.Quantity, b.Trader.GetComponent<Trader>().Coins / price) })
            .Where(b => b.Quantity > 0).ToList();
        var supply = asks.Where(a => a.Limit <= price).ToList();
        long demanded = demand.Sum(b => (long)b.Quantity), supplied = supply.Sum(a => (long)a.Quantity);

        Settle(world, market, demand, supply, good, price);

        // Excess demand pushes the price up, excess supply down, in proportion to the gap.
        int step = world.Content.Economy.Market.BeliefStepPercent;
        long gap = demanded - supplied, larger = Math.Max(demanded, supplied);
        int next = price;
        if (gap != 0 && larger > 0)
        {
            int move = (int)((long)price * step * Math.Abs(gap) / larger / 100);
            next = price + Math.Sign(gap) * Math.Max(1, move);
        }
        state.Price[good] = Math.Clamp(next, 1, MaxBelief(world, good));

        // Everyone at home hears the price and adjusts what they think the good is worth.
        int player = market.GetComponent<Owner>().Player;
        foreach (var t in traders)
        {
            if (!Traders.IsMerchant(t) && Traders.PlayerOf(t) == player) Pull(world, t, good, state.Price[good]);
        }
    }

    // A first price for a good never traded for coins: halfway between the best bid and the best ask.
    private static int OpeningPrice(List<Offer> bids, List<Offer> asks) =>
        Math.Max(1, (bids.Max(b => b.Limit) + asks.Min(a => a.Limit)) / 2);

    private static void Settle(World world, Entity market, List<Offer> bids, List<Offer> asks, int good, int price)
    {
        var rules = world.Content.Economy.Market;
        int player = market.GetComponent<Owner>().Player;
        Civics.TryGet(world, player, out var civEntity);
        var traders = TraderFamiliesAt(world, market);
        ref var state = ref market.GetComponent<Market>();

        int i = 0, j = 0;
        int bidLeft = bids.Count > 0 ? bids[0].Quantity : 0, askLeft = asks.Count > 0 ? asks[0].Quantity : 0;
        while (i < bids.Count && j < asks.Count)
        {
            var buyer = bids[i].Trader;
            var seller = asks[j].Trader;
            ref var buyerTrader = ref buyer.GetComponent<Trader>();
            int q = Math.Min(Math.Min(bidLeft, askLeft), buyerTrader.Coins / price);
            if (q > 0)
            {
                ref var civ = ref civEntity.GetComponent<Civilization>();
                int value = q * price;
                bool foreign = Traders.IsMerchant(buyer) || Traders.IsMerchant(seller);
                int tax = Traders.IsTreasury(seller) ? 0 : value * civ.MarketTaxPercent / 100;
                int tariff = foreign ? value * civ.TariffPercent / 100 : 0;
                int commission = traders.Count > 0 ? value * rules.CommissionPercent / 100 : 0;
                buyerTrader.Coins -= value;
                buyerTrader.AtMarket[good] += q;
                ref var sellerTrader = ref seller.GetComponent<Trader>();
                sellerTrader.AtMarket[good] -= q;
                sellerTrader.Coins += value - tax - tariff - commission;
                ref var treasury = ref civEntity.GetComponent<Trader>();
                treasury.Coins += tax + tariff;
                PayCommission(traders, commission);
                state.Commission += commission;
                state.Volume[good] += q;
                if (buyer.TryGetComponent<Merchant>(out _)) buyer.GetComponent<Merchant>().Bought[good] += q;
                civ.Trades++;
                Economy.Record(world, player, LedgerEntry.MarketTax, tax);
                Economy.Record(world, player, LedgerEntry.Tariffs, tariff);
                if (Traders.IsTreasury(buyer)) Economy.Record(world, player, LedgerEntry.Purchases, value);
                if (Traders.IsTreasury(seller)) Economy.Record(world, player, LedgerEntry.Sales, value);
            }
            bidLeft -= q;
            askLeft -= q;
            if (q == 0 || bidLeft == 0) { if (++i < bids.Count) bidLeft = bids[i].Quantity; }
            if (askLeft == 0 && ++j < asks.Count) askLeft = asks[j].Quantity;
        }
    }

    // The families of the market traders working at the marketplace now, one entry per trader. A trader living at
    // the camp has no family to keep a commission, so none is charged for them and no coins are lost.
    private static List<Entity> TraderFamiliesAt(World world, Entity market)
    {
        var families = new List<Entity>();
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order>()))
        {
            var order = unit.GetComponent<Order>();
            if (order.Kind == OrderKind.Work && order.Target == market.Id && order.Stage == OrderStage.Work
                && Households.TryGetHome(world, unit, out var home))
                families.Add(home);
        }
        return families;
    }

    // Splits a commission among the market traders' families.
    private static void PayCommission(List<Entity> families, int commission)
    {
        if (families.Count == 0 || commission <= 0) return;
        int share = commission / families.Count, rest = commission % families.Count;
        for (int k = 0; k < families.Count; k++)
            families[k].GetComponent<Trader>().Coins += share + (k < rest ? 1 : 0);
    }

    // ---- Beliefs and records ----

    private static int Belief(Entity trader, int good) => trader.GetComponent<Trader>().Beliefs[good];

    private static void Move(Entity from, Entity to, int good, int amount)
    {
        from.GetComponent<Trader>().AtMarket[good] -= amount;
        to.GetComponent<Trader>().AtMarket[good] += amount;
        if (to.TryGetComponent<Merchant>(out _)) to.GetComponent<Merchant>().Bought[good] += amount;
    }

    // A trader who traded (or heard the price) moves its belief part of the way toward the price.
    private static void Pull(World world, Entity trader, int good, int price)
    {
        if (Traders.IsMerchant(trader)) return; // merchants trade at world prices
        ref int belief = ref trader.GetComponent<Trader>().Beliefs[good];
        int gap = price - belief;
        int move = gap * world.Content.Economy.Market.BeliefPullPercent / 100;
        if (move == 0) move = Math.Sign(gap);
        belief = Math.Clamp(belief + move, 1, MaxBelief(world, good));
    }

    // A buyer who got nothing thinks the good is worth more; a seller who sold nothing thinks it is worth less.
    private static void Step(World world, Entity trader, int good, bool up)
    {
        if (Traders.IsMerchant(trader)) return;
        ref int belief = ref trader.GetComponent<Trader>().Beliefs[good];
        int step = Math.Max(1, belief * world.Content.Economy.Market.BeliefStepPercent / 100);
        belief = Math.Clamp(up ? belief + step : belief - step, 1, MaxBelief(world, good));
    }

    // Beliefs stay within a wide band around the starting value (wide enough for heavy inflation).
    private static int MaxBelief(World world, int good) =>
        world.Content.Goods[good].Value * world.Content.Economy.Market.MaxPriceMultiple;

    private static void RecordDay(World world, Entity market)
    {
        var content = world.Content;
        int goods = content.Goods.Count;
        int player = market.GetComponent<Owner>().Player;
        ref var state = ref market.GetComponent<Market>();
        int length = content.Economy.Market.HistoryLength;
        int slot = state.Days % length;
        for (int g = 0; g < goods; g++) state.PriceHistory[slot * goods + g] = state.Price[g];
        if (Economy.HasMoney(world, player))
        {
            if (state.BasePrice.All(p => p == 0))
            {
                for (int g = 0; g < goods; g++) state.BasePrice[g] = Economy.Price(world, player, g);
            }
            state.CpiHistory[slot] = Cpi(world, market);
        }
        state.MoneyHistory[slot] = Economy.MoneySupply(world, player);
        state.Days++;
    }

    /// <summary>
    /// The consumer price index: the cost of a basket of everyday goods (<c>cpiWeight</c>) today, in percent of its
    /// cost on the first day of trade in coins. 0 before coins.
    /// </summary>
    public static int Cpi(World world, Entity market)
    {
        var state = market.GetComponent<Market>();
        int player = market.GetComponent<Owner>().Player;
        long now = 0, then = 0;
        for (int g = 0; g < state.Price.Length; g++)
        {
            int weight = world.Content.Goods[g].CpiWeight;
            if (weight == 0 || state.BasePrice[g] == 0) continue;
            now += (long)weight * Economy.Price(world, player, g);
            then += (long)weight * state.BasePrice[g];
        }
        return then == 0 ? 0 : (int)(now * 100 / then);
    }

    /// <summary>A fresh market record sized for the content.</summary>
    internal static Market NewMarket(Content.ContentDatabase content)
    {
        int goods = content.Goods.Count;
        int length = content.Economy.Market.HistoryLength;
        return new Market
        {
            Price = new int[goods],
            Volume = new int[goods],
            PriceHistory = new int[goods * length],
            CpiHistory = new int[length],
            MoneyHistory = new int[length],
            BasePrice = new int[goods],
            Medium = -1,
        };
    }

    /// <summary>
    /// Price history of a good, oldest first, for the last <paramref name="days"/> market days (0 = no price yet).
    /// </summary>
    public static int[] History(World world, Entity market, int good, int days)
    {
        var state = market.GetComponent<Market>();
        int goods = world.Content.Goods.Count, length = world.Content.Economy.Market.HistoryLength;
        days = Math.Min(days, Math.Min(length, state.Days));
        var result = new int[days];
        for (int k = 0; k < days; k++)
        {
            int slot = (state.Days - days + k) % length;
            result[k] = good >= 0 ? state.PriceHistory[slot * goods + good] : state.CpiHistory[slot];
        }
        return result;
    }
}
