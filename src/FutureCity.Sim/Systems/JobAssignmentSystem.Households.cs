using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

// Jobs once families own their goods. The chief (treasury) still assigns public work (building sites, shrines, the
// mint, gathering for the public stores) to as many people as it can feed, later pay. Everyone else works for their
// family at whatever earns most at the family's own valuation of goods: market prices once there is a market,
// before that what the family is short of. People change jobs only for clearly better pay, a few at a time.
public sealed partial class JobAssignmentSystem
{
    private sealed class HouseholdContext
    {
        public required int Player;
        public required Entity Civ;
        public required bool Money;
        public required bool HasMarket;
        public required Entity Market;
        public required Dictionary<int, List<Entity>> Members;
        public required Dictionary<int, TradePlan> Plans;
        public required Dictionary<int, int> Workers;   // building id -> people working or building there
        public required int[] Crafters;                 // by building kind
        public required List<Entity> Buildings;
        public required bool?[] Gatherable;
        public required TilePosition Camp;
    }

    private static void AssignHouseholds(World world, int player, Entity camp)
    {
        var content = world.Content;
        var rules = content.Economy.Wages;
        if (!Civics.TryGet(world, player, out var civEntity)) return;
        var units = World.InIdOrder(world.Store.Query<Citizen, Order, Owner>())
            .Where(u => u.GetComponent<Owner>().Player == player && Bands.IsAdult(world, u.GetComponent<Citizen>()))
            .ToList();
        var members = Households.Members(world, player);
        var ctx = new HouseholdContext
        {
            Player = player,
            Civ = civEntity,
            Money = Economy.HasMoney(world, player),
            HasMarket = Economy.TryGetMarket(world, player, out var market),
            Market = market,
            Members = members,
            Plans = Households.Of(world, player).ToDictionary(h => h.Id, h => Traders.PlanOf(world, h, members)),
            Workers = [],
            Crafters = HouseholdSystem.Crafters(world, player),
            Buildings = World.InIdOrder(world.Store.Query<Building, Owner>())
                .Where(b => b.GetComponent<Owner>().Player == player).ToList(),
            Gatherable = new bool?[content.Goods.Count],
            Camp = camp.GetComponent<TilePosition>(),
        };
        foreach (var unit in units)
        {
            var order = unit.GetComponent<Order>();
            if (order.Kind is OrderKind.Work or OrderKind.Build)
                ctx.Workers[order.Target] = ctx.Workers.GetValueOrDefault(order.Target) + 1;
        }

        // Private incomes set the going wage; the treasury pays public workers that plus a premium.
        var incomes = new List<int>();
        foreach (var unit in units)
        {
            var order = unit.GetComponent<Order>();
            if (order.Public || order.Kind == OrderKind.Idle || !Households.TryGetHome(world, unit, out var home)) continue;
            int income = Income(world, ctx, home, order);
            if (income > 0) incomes.Add(income);
        }
        ref var civ = ref civEntity.GetComponent<Civilization>();
        if (ctx.Money)
        {
            incomes.Sort();
            int typical = incomes.Count > 0 ? incomes[incomes.Count / 2] : Math.Max(1, civ.PublicWage);
            civ.PublicWage = Math.Max(1, typical * (100 + rules.PublicPremiumPercent) / 100);
        }
        else
        {
            civ.PublicWage = 0;
        }

        int capacity = PayPublicWorkers(world, ctx, units);
        Trips(world, ctx, units);
        var demand = Demand.Of(world, player);
        int publicWorkers = units.Count(u => IsPublicWorker(world, u));
        var gatherers = new int[content.Goods.Count];
        foreach (var unit in units)
        {
            var order = unit.GetComponent<Order>();
            if (order.Public && GoodOf(world, order) is int good and >= 0) gatherers[good]++;
        }
        WithdrawPublicGatherers(world, units, demand, gatherers);

        foreach (var unit in units)
        {
            if (unit.GetComponent<Order>().Kind != OrderKind.Idle) continue;
            bool hasHome = Households.TryGetHome(world, unit, out var home);
            if ((!hasHome || publicWorkers < capacity) && AssignPublic(world, ctx, unit, demand, gatherers))
            {
                if (hasHome) publicWorkers++;
                continue;
            }
            if (hasHome) AssignPrivate(world, ctx, unit, home);
        }
        SwitchJobs(world, ctx, units);
    }

    // Someone with a home doing public work (people living at the camp work for the treasury anyway).
    private static bool IsPublicWorker(World world, Entity unit)
    {
        var order = unit.GetComponent<Order>();
        return order.Public && order.Kind is not (OrderKind.Idle or OrderKind.Move) && Households.TryGetHome(world, unit, out _);
    }

    // Pays this check's wages (with coins) and returns how many public workers the treasury can keep: as many as it
    // can feed from the public stores before coins, or pay for a few checks after. Automatic public workers beyond
    // that are let go.
    private static int PayPublicWorkers(World world, HouseholdContext ctx, List<Entity> units)
    {
        var rules = world.Content.Economy.Wages;
        var civ = ctx.Civ.GetComponent<Civilization>();
        ref var treasury = ref ctx.Civ.GetComponent<Trader>();
        int wage = civ.PublicWage * world.Content.Citizens.Jobs.CheckIntervalTicks / 100;
        if (ctx.Money)
        {
            foreach (var unit in units)
            {
                if (!IsPublicWorker(world, unit) || treasury.Coins < wage) continue;
                Households.TryGetHome(world, unit, out var home);
                treasury.Coins -= wage;
                home.GetComponent<Trader>().Coins += wage;
                Economy.Record(world, ctx.Player, LedgerEntry.Wages, wage);
            }
        }
        int capacity = ctx.Money
            ? treasury.Coins / Math.Max(1, wage * rules.ReserveChecks)
            : Stores.Meals(world, ctx.Player) / rules.RationMeals;
        int working = units.Count(u => IsPublicWorker(world, u));
        for (int k = units.Count - 1; k >= 0 && working > capacity; k--)
        {
            var unit = units[k];
            ref var order = ref unit.GetComponent<Order>();
            if (!order.Auto || !IsPublicWorker(world, unit) || unit.GetComponent<Citizen>().Carried > 0) continue;
            order = default;
            working--;
        }
        return capacity;
    }

    // Public gatherers go home when the public stores have enough of their good.
    private static void WithdrawPublicGatherers(World world, List<Entity> units, Demand demand, int[] gatherers)
    {
        foreach (var unit in units)
        {
            ref var order = ref unit.GetComponent<Order>();
            if (!order.Auto || !order.Public || order.Kind is not (OrderKind.Gather or OrderKind.Hunt)
                || unit.GetComponent<Citizen>().Carried > 0)
                continue;
            if (GoodOf(world, order) is int good and >= 0 && demand.ForGood(good) <= 0)
            {
                gatherers[good]--;
                order = default;
            }
        }
    }

    // The chief's jobs: building sites, shrines and the mint, and gathering what the public stores lack.
    private static bool AssignPublic(World world, HouseholdContext ctx, Entity unit, Demand demand, int[] gatherers)
    {
        int bestScore = 0, bestGood = -1;
        Entity bestBuilding = default;
        foreach (var building in ctx.Buildings)
        {
            var type = Buildings.TypeOf(world, building);
            if (Buildings.IsComplete(building) && !type.IsPublicWorkplace) continue;
            int score = BuildingScore(world, ctx.Player, building, demand, ctx.Workers.GetValueOrDefault(building.Id));
            if (score <= bestScore) continue;
            bestScore = score;
            bestBuilding = building;
        }
        for (int good = 0; good < world.Content.Goods.Count; good++)
        {
            int score = demand.ForGood(good) / (1 + gatherers[good]);
            if (score <= bestScore || !(ctx.Gatherable[good] ??= TryFindSource(world, good, ctx.Camp, out _, out _))) continue;
            bestScore = score;
            bestGood = good;
            bestBuilding = default;
        }
        if (bestGood >= 0 && StartGathering(world, unit, bestGood, ctx.Camp))
        {
            gatherers[bestGood]++;
            unit.GetComponent<Order>().Public = true;
            return true;
        }
        if (bestBuilding.IsNull) return false;
        var kind = Buildings.IsComplete(bestBuilding) ? OrderKind.Work : OrderKind.Build;
        UnitOrders.Assign(unit, kind, bestBuilding, TargetType.Building, bestBuilding.GetComponent<Building>().Kind);
        unit.GetComponent<Order>().Auto = true;
        ctx.Workers[bestBuilding.Id] = ctx.Workers.GetValueOrDefault(bestBuilding.Id) + 1;
        return true;
    }

    // Work for the family: the best-paying of gathering, farming, crafting and trading at the market.
    private static bool AssignPrivate(World world, HouseholdContext ctx, Entity unit, Entity home)
    {
        var (income, good, building) = BestPrivateJob(world, ctx, home, unit);
        if (income <= 0) return false;
        return StartPrivate(world, ctx, unit, home, good, building);
    }

    private static bool StartPrivate(World world, HouseholdContext ctx, Entity unit, Entity home, int good, Entity building)
    {
        if (good >= 0)
        {
            if (!StartGathering(world, unit, good, home.GetComponent<TilePosition>())) return false;
        }
        else
        {
            UnitOrders.Assign(unit, OrderKind.Work, building, TargetType.Building, building.GetComponent<Building>().Kind);
            ctx.Workers[building.Id] = ctx.Workers.GetValueOrDefault(building.Id) + 1;
            ctx.Crafters[building.GetComponent<Building>().Kind]++;
        }
        ref var order = ref unit.GetComponent<Order>();
        order.Auto = true;
        order.Public = false;
        return true;
    }

    private static (int Income, int Good, Entity Building) BestPrivateJob(World world, HouseholdContext ctx, Entity home, Entity unit)
    {
        var content = world.Content;
        var plan = ctx.Plans[home.Id];
        int best = 0, bestGood = -1;
        Entity bestBuilding = default;
        for (int good = 0; good < content.Goods.Count; good++)
        {
            int income = GatherIncome(world, ctx, home, plan, good);
            if (income <= best || !(ctx.Gatherable[good] ??= TryFindSource(world, good, ctx.Camp, out _, out _))) continue;
            best = income;
            bestGood = good;
        }
        var current = unit.GetComponent<Order>();
        foreach (var building in ctx.Buildings)
        {
            var type = Buildings.TypeOf(world, building);
            if (!Buildings.IsComplete(building) || type.IsPublicWorkplace || !type.IsWorkplace) continue;
            bool here = current.Kind == OrderKind.Work && current.Target == building.Id;
            int working = ctx.Workers.GetValueOrDefault(building.Id) - (here ? 1 : 0);
            if (working >= type.Def.Workers || (!here && !GuildAdmits(world, ctx, type.Index))) continue;
            int income = WorkIncome(world, ctx, home, plan, building, working);
            if (income <= best) continue;
            best = income;
            bestGood = -1;
            bestBuilding = building;
        }
        return (best, bestGood, bestBuilding);
    }

    // Under guilds a craft takes only so many people a year.
    private static bool GuildAdmits(World world, HouseholdContext ctx, int kind)
    {
        if (!world.Content.Buildings[kind].IsWorkshop || !Economy.HasGuilds(world, ctx.Player)) return true;
        return ctx.Crafters[kind] < ctx.Civ.GetComponent<Civilization>().GuildCap[kind];
    }

    // What the family earns per 100 ticks from the job an order describes (0 if it earns nothing now).
    private static int Income(World world, HouseholdContext ctx, Entity home, in Order order)
    {
        var plan = ctx.Plans.GetValueOrDefault(home.Id);
        if (plan == null) return 0;
        if (order.Kind is OrderKind.Gather or OrderKind.Hunt)
            return GoodOf(world, order) is int good and >= 0 ? GatherIncome(world, ctx, home, plan, good) : 0;
        if (order.Kind == OrderKind.Work && world.TryGetEntity(order.Target, out var building) && building.HasComponent<Building>())
            return WorkIncome(world, ctx, home, plan, building, Math.Max(0, ctx.Workers.GetValueOrDefault(building.Id) - 1));
        return 0;
    }

    private static int GatherIncome(World world, HouseholdContext ctx, Entity home, TradePlan plan, int good)
    {
        int ticks = world.Content.GatherTicksPerUnit(good);
        if (ticks == 0) return 0;
        int value = Traders.Value(world, home, plan, good, ctx.HasMarket);
        return value * world.Content.Economy.Wages.TravelPercent / ticks;
    }

    // Income from a farm, workshop or market stall, shared with `working` others already there.
    private static int WorkIncome(World world, HouseholdContext ctx, Entity home, TradePlan plan, Entity building, int working)
    {
        var content = world.Content;
        var type = Buildings.TypeOf(world, building);
        var beliefs = home.GetComponent<Trader>().Beliefs;
        int travel = content.Economy.Wages.TravelPercent;
        if (type.Def.Field is { } field)
        {
            var state = building.GetComponent<Field>();
            bool work = state.Stage == FieldStage.Ripe || (state.Stage == FieldStage.Fallow && Calendar.Season(world).Sowing)
                        || building.GetComponent<Inventory>().Amounts[type.FieldGood] > 0;
            if (!work) return 0;
            long units = (long)field.Yield * state.Fertility / 100;
            long ticks = field.SowWork + (long)field.Yield * field.HarvestTicksPerUnit;
            return (int)(Traders.Value(world, home, plan, type.FieldGood, ctx.HasMarket) * units * travel / ticks);
        }
        if (type.Def.Recipe is { } recipe)
        {
            if (!InputsAvailable(world, ctx, home, building, type)) return 0;
            int bonus = Economy.HasGuilds(world, ctx.Player) ? content.Economy.Guilds.OutputBonusPercent : 0;
            long added = 0;
            for (int g = 0; g < beliefs.Length; g++)
            {
                added += (long)type.Outputs[g] * Traders.Value(world, home, plan, g, ctx.HasMarket) * (100 + bonus) / 100;
                added -= (long)type.Inputs[g] * beliefs[g];
            }
            return (int)Math.Max(0, added * travel / recipe.WorkTicks);
        }
        if (type.Def.Market && ctx.Money)
        {
            var market = building.GetComponent<Market>();
            int interval = content.Economy.Market.IntervalTicks;
            return market.Commission * 100 / (1 + working) / interval;
        }
        return 0;
    }

    // A crafter can work if the family (or the workshop) has the inputs, or the market has them on offer.
    private static bool InputsAvailable(World world, HouseholdContext ctx, Entity home, Entity workshop, Content.BuildingType type)
    {
        var stock = home.GetComponent<Inventory>().Amounts;
        var inShop = workshop.GetComponent<Inventory>().Amounts;
        bool all = true;
        for (int g = 0; g < stock.Length; g++)
        {
            if (type.Inputs[g] > stock[g] + inShop[g]) all = false;
        }
        if (all) return true;
        if (!ctx.HasMarket) return false;
        var onOffer = ctx.Market.GetComponent<Inventory>().Amounts;
        for (int g = 0; g < stock.Length; g++)
        {
            if (type.Inputs[g] > stock[g] + inShop[g] + onOffer[g]) return false;
        }
        return true;
    }

    // People working for their family move to a clearly better-paid job, a few per check.
    private static void SwitchJobs(World world, HouseholdContext ctx, List<Entity> units)
    {
        var rules = world.Content.Economy.Wages;
        int switches = 0;
        foreach (var unit in units)
        {
            if (switches >= rules.SwitchesPerCheck) return;
            var order = unit.GetComponent<Order>();
            if (!order.Auto || order.Public || order.Kind is not (OrderKind.Gather or OrderKind.Hunt or OrderKind.Work)
                || unit.GetComponent<Citizen>().Carried > 0 || !Households.TryGetHome(world, unit, out var home))
                continue;
            int current = Income(world, ctx, home, order);
            var (best, good, building) = BestPrivateJob(world, ctx, home, unit);
            if (best <= 0 || (long)best * 100 <= (long)current * (100 + rules.SwitchMarginPercent)) continue;
            if (good < 0 && building.Id == order.Target) continue;
            if (order.Kind == OrderKind.Work && world.TryGetEntity(order.Target, out var old))
            {
                ctx.Workers[old.Id] = ctx.Workers.GetValueOrDefault(old.Id) - 1;
                if (old.HasComponent<Building>()) ctx.Crafters[old.GetComponent<Building>().Kind]--;
            }
            if (StartPrivate(world, ctx, unit, home, good, building)) switches++;
        }
    }

    // Trips to market: a family with goods to sell or purchases waiting sends someone (an idle member, or one of its
    // workers between loads); the treasury sends one of its public workers or someone from the camp.
    private static void Trips(World world, HouseholdContext ctx, List<Entity> units)
    {
        if (!ctx.HasMarket) return;
        int capacity = world.Content.Citizens.CarryCapacity;
        var traveling = new HashSet<int>();
        bool treasuryTraveling = false;
        foreach (var unit in units)
        {
            var order = unit.GetComponent<Order>();
            if (order.Kind != OrderKind.Trade) continue;
            if (order.Public) treasuryTraveling = true;
            else if (Households.TryGetHome(world, unit, out var home)) traveling.Add(home.Id);
        }

        foreach (var (homeId, plan) in ctx.Plans)
        {
            if (traveling.Contains(homeId) || !world.TryGetEntity(homeId, out var home) || !NeedsTrip(world, home, plan, capacity))
                continue;
            var traveller = PickTraveller(world, ctx.Members.GetValueOrDefault(homeId) ?? [], publicWork: false);
            if (!traveller.IsNull) StartTrip(traveller, ctx.Market, publicWork: false);
        }

        if (!treasuryTraveling && NeedsTrip(world, ctx.Civ, Traders.PlanOf(world, ctx.Civ), capacity))
        {
            var traveller = PickTraveller(world, units.Where(u => !Households.TryGetHome(world, u, out _)).ToList(), publicWork: true);
            if (traveller.IsNull) traveller = PickTraveller(world, units, publicWork: true);
            if (!traveller.IsNull) StartTrip(traveller, ctx.Market, publicWork: true);
        }
    }

    private static bool NeedsTrip(World world, Entity trader, TradePlan plan, int capacity)
    {
        Markets.NextToBring(world, trader, plan, out int bring);
        Markets.NextToCollect(world, trader, plan, out int collect);
        return bring >= capacity / 2 || collect > 0;
    }

    // An idle adult, or else an automatic worker of the right kind (family or public) not carrying anything.
    private static Entity PickTraveller(World world, List<Entity> people, bool publicWork)
    {
        Entity worker = default;
        foreach (var unit in people)
        {
            if (!Bands.IsAdult(world, unit.GetComponent<Citizen>())) continue;
            var order = unit.GetComponent<Order>();
            if (order.Kind == OrderKind.Idle) return unit;
            if (worker.IsNull && order.Auto && order.Public == publicWork && order.Kind is OrderKind.Gather or OrderKind.Hunt
                && unit.GetComponent<Citizen>().Carried == 0)
                worker = unit;
        }
        return worker;
    }

    private static void StartTrip(Entity unit, Entity market, bool publicWork)
    {
        UnitOrders.Assign(unit, OrderKind.Trade, market, TargetType.Building, market.GetComponent<Building>().Kind);
        ref var order = ref unit.GetComponent<Order>();
        order.Stage = OrderStage.Fetch;
        order.Auto = true;
        order.Public = publicWork;
    }
}
