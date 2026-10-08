using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Automatic jobs, once a civilization has an institution that organizes labour (a chiefdom). Idle adults take the
/// job with the highest demand: building sites, workplaces whose products are short, research while something can be
/// discovered, or gathering the raw good most needed. Demand is the gap between the stock the band wants per person
/// and what its stores hold, shared among the people already doing that job. Jobs assigned this way are withdrawn
/// when their good is no longer needed; the player's own orders are never overridden.
/// </summary>
public sealed class JobAssignmentSystem : ISimSystem
{
    // How far from camp automatic gatherers look for sources.
    private const int GatherRadius = 30;

    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Citizens.Jobs;
        if (world.Tick % rules.CheckIntervalTicks != 0) return;
        foreach (var civ in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            int player = civ.GetComponent<Owner>().Player;
            if (Civics.HasAutoJobs(world, player) && Bands.TryGetCamp(world, player, out var camp))
                Assign(world, player, camp);
        }
    }

    private static void Assign(World world, int player, Entity camp)
    {
        var content = world.Content;
        var demand = Demand.Of(world, player);
        var units = World.InIdOrder(world.Store.Query<Citizen, Order, Owner>())
            .Where(u => u.GetComponent<Owner>().Player == player && Bands.IsAdult(world, u.GetComponent<Citizen>()))
            .ToList();

        var workers = new Dictionary<int, int>();   // building id -> people working or building there
        var gatherers = new int[content.Goods.Count];
        foreach (var unit in units)
        {
            var order = unit.GetComponent<Order>();
            if (order.Kind is OrderKind.Work or OrderKind.Build) workers[order.Target] = workers.GetValueOrDefault(order.Target) + 1;
            else if (GoodOf(world, order) is int good and >= 0) gatherers[good]++;
        }

        // Withdraw automatic gatherers whose good is no longer wanted (after they drop off their load).
        foreach (var unit in units)
        {
            ref var order = ref unit.GetComponent<Order>();
            if (!order.Auto || order.Kind is not (OrderKind.Gather or OrderKind.Hunt) || unit.GetComponent<Citizen>().Carried > 0) continue;
            if (GoodOf(world, order) is int good and >= 0 && demand.ForGood(good) <= 0)
            {
                gatherers[good]--;
                order = default;
            }
        }

        var sites = World.InIdOrder(world.Store.Query<Building, Owner>())
            .Where(b => b.GetComponent<Owner>().Player == player)
            .ToList();
        var campPos = camp.GetComponent<TilePosition>();
        var gatherable = new bool?[content.Goods.Count]; // looked up once per check, only when needed
        foreach (var unit in units)
        {
            if (unit.GetComponent<Order>().Kind != OrderKind.Idle) continue;
            int bestScore = 0;
            Entity bestBuilding = default;
            int bestGood = -1;
            foreach (var building in sites)
            {
                int score = BuildingScore(world, player, building, demand, workers.GetValueOrDefault(building.Id));
                if (score <= bestScore) continue;
                bestScore = score;
                bestBuilding = building;
                bestGood = -1;
            }
            for (int good = 0; good < content.Goods.Count; good++)
            {
                int score = demand.ForGood(good) / (1 + gatherers[good]);
                if (score <= bestScore || !(gatherable[good] ??= TryFindSource(world, good, campPos, out _, out _))) continue;
                bestScore = score;
                bestGood = good;
                bestBuilding = default;
            }

            if (bestGood >= 0 && StartGathering(world, unit, bestGood, campPos))
            {
                gatherers[bestGood]++;
            }
            else if (!bestBuilding.IsNull)
            {
                var kind = Buildings.IsComplete(bestBuilding) ? OrderKind.Work : OrderKind.Build;
                UnitOrders.Assign(unit, kind, bestBuilding, TargetType.Building, bestBuilding.GetComponent<Building>().Kind);
                unit.GetComponent<Order>().Auto = true;
                workers[bestBuilding.Id] = workers.GetValueOrDefault(bestBuilding.Id) + 1;
            }
        }
    }

    // Demand score of one more person at a building, or 0 if it does not need anyone.
    private static int BuildingScore(World world, int player, Entity building, Demand demand, int working)
    {
        var rules = world.Content.Citizens.Jobs;
        var type = Buildings.TypeOf(world, building);
        if (!Buildings.IsComplete(building))
            return working < rules.MaxBuildersPerSite ? rules.BuildPriority / (1 + working) : 0;
        if (working >= type.Def.Workers) return 0;

        if (type.Def.Research > 0)
        {
            var facts = demand.Facts;
            bool anything = Enumerable.Range(0, world.Content.Techs.Count).Any(t => Civics.IsDiscoverable(world, player, t, facts));
            return anything ? rules.ResearchPriority / (1 + working) : 0;
        }
        if (type.Def.Field != null)
        {
            var field = building.GetComponent<Field>();
            bool work = field.Stage == FieldStage.Ripe || (field.Stage == FieldStage.Fallow && Calendar.Season(world).Sowing)
                        || building.GetComponent<Inventory>().Amounts[type.FieldGood] > 0;
            // A ripe crop must come in before it rots, whatever the stores hold.
            return !work ? 0 : field.Stage == FieldStage.Ripe ? rules.BuildPriority + demand.ForGood(type.FieldGood)
                : 1 + demand.ForGood(type.FieldGood) / (1 + working);
        }
        if (type.Def.Recipe != null)
        {
            var stock = building.GetComponent<Inventory>().Amounts;
            for (int g = 0; g < stock.Length; g++)
            {
                if (type.Inputs[g] > stock[g] + demand.Store[g]) return 0; // nothing to work with
            }
            int score = 0;
            for (int g = 0; g < stock.Length; g++)
            {
                if (type.Outputs[g] > 0) score += demand.ForGood(g);
            }
            return score / (1 + working);
        }
        return 0;
    }

    // The good a gathering or hunting order brings in, or -1.
    private static int GoodOf(World world, in Order order)
    {
        if (order.Kind == OrderKind.Hunt) return world.Content.AnimalGood(order.TargetKind);
        if (order.Kind != OrderKind.Gather) return -1;
        return order.TargetType switch
        {
            TargetType.Plant => world.Content.PlantGood(order.TargetKind),
            TargetType.Carcass => world.Content.AnimalGood(order.TargetKind),
            TargetType.Deposit => world.Content.DepositSource(order.TargetKind).Good,
            TargetType.Tile => world.Content.TerrainSource(order.TargetKind)?.Good ?? -1,
            _ => -1,
        };
    }

    private static bool StartGathering(World world, Entity unit, int good, TilePosition from)
    {
        if (!TryFindSource(world, good, from, out var source, out var tile)) return false;
        if (source.IsNull)
        {
            UnitOrders.AssignTile(unit, tile.X, tile.Y, world.Map.GetTerrain(tile.X, tile.Y));
        }
        else if (source.HasComponent<Animal>())
        {
            UnitOrders.Assign(unit, OrderKind.Hunt, source, TargetType.Animal, source.GetComponent<Animal>().Kind);
        }
        else
        {
            UnitOrders.Assign(unit, OrderKind.Gather, source, Sources.TypeOf(source), Sources.KindOf(source));
        }
        unit.GetComponent<Order>().Auto = true;
        return true;
    }

    // The nearest place to get a good: a well-stocked plant, a carcass or animal, a deposit, or a resource tile.
    private static bool TryFindSource(World world, int good, TilePosition from, out Entity source, out TilePosition tile)
    {
        var content = world.Content;
        int capacity = content.Citizens.CarryCapacity;
        source = default;
        tile = default;
        for (int kind = 0; kind < content.Plants.Count; kind++)
        {
            if (content.PlantGood(kind) == good && Sources.TryFindPlant(world, kind, from.X, from.Y, GatherRadius, out source, capacity))
                return true;
        }
        for (int kind = 0; kind < content.Animals.Count; kind++)
        {
            if (content.AnimalGood(kind) != good) continue;
            if (Sources.TryFindCarcass(world, from.X, from.Y, GatherRadius, out source)
                && content.AnimalGood(source.GetComponent<Carcass>().Kind) == good)
                return true;
            if (Sources.TryFindAnimal(world, kind, from.X, from.Y, GatherRadius, out source)) return true;
        }
        if (Sources.TryFindDepositOf(world, good, from.X, from.Y, GatherRadius, out source)) return true;
        source = default;
        if (!Sources.TryFindTile(world, good, -1, from.X, from.Y, GatherRadius, out int x, out int y)) return false;
        tile = new TilePosition(x, y);
        return true;
    }
}
