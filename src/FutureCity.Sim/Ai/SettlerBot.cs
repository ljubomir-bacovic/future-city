using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Ai;

/// <summary>
/// A stand-in player that settles: it builds a storehouse, huts and a shrine, researches, establishes a chiefdom,
/// farms and sets up workshops, then (with Private property, Coinage and Guilds) a marketplace and a mint. Used for balancing and tests until the rival AI arrives (Phase 5). Like a human it
/// only reads the world and issues ordinary commands. Before the chiefdom it directs every adult (builders,
/// workplaces, material gatherers, and everyone else on food); afterwards it leaves labour to the automatic jobs.
/// </summary>
public sealed class SettlerBot
{
    private readonly ForagingBot _forager;

    /// <summary>Creates a bot for <paramref name="player"/>.</summary>
    public SettlerBot(int player)
    {
        Player = player;
        _forager = new ForagingBot(player, huntersPercent: 20);
    }

    /// <summary>The player it controls.</summary>
    public int Player { get; }

    /// <summary>How often the bot looks at its settlement.</summary>
    public int IntervalTicks { get; init; } = 10;

    // Building sites the bot keeps going at once.
    private const int MaxSites = 2;
    // Wood kept in store beyond what sites need, for the next building.
    private const int WoodReserve = 30;

    /// <summary>Looks at the settlement and queues commands. Call once before each step.</summary>
    public void Act(Simulation sim)
    {
        var world = sim.World;
        if (world.Tick % IntervalTicks != 0 || !Bands.TryGetCamp(world, Player, out var camp)) return;
        var facts = Civics.FactsOf(world, Player);
        ChooseResearch(sim, facts);
        EstablishInstitutions(sim, facts);
        PlanBuildings(sim, camp, facts);
        if (Civics.HasAutoJobs(world, Player)) HandOver(sim);
        else DirectLabour(sim, camp, facts);
    }

    // Once a chiefdom organizes labour, people still on the bot's own orders are released so automatic jobs take them.
    private void HandOver(Simulation sim)
    {
        var world = sim.World;
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order, Owner, TilePosition>()))
        {
            var order = unit.GetComponent<Order>();
            if (unit.GetComponent<Owner>().Player != Player || order.Auto || !Bands.IsAdult(world, unit.GetComponent<Citizen>())
                || unit.HasComponent<Soldier>()
                || order.Kind is not (OrderKind.Gather or OrderKind.Hunt or OrderKind.Build or OrderKind.Work))
                continue;
            var pos = unit.GetComponent<TilePosition>();
            sim.Enqueue(new MoveUnits([unit.Id], pos.X, pos.Y) { Player = Player }); // stop: idle on the next tick
        }
    }

    private void ChooseResearch(Simulation sim, Facts facts)
    {
        var world = sim.World;
        if (!Civics.TryGet(world, Player, out var civ)) return;
        int focus = civ.GetComponent<Civilization>().ResearchFocus;
        if (focus >= 0 && Civics.IsDiscoverable(world, Player, focus, facts)) return;
        // Farming first: it feeds the village. Then whatever else can be discovered.
        var order = world.Content.Techs.Select(t => t.Index).OrderBy(t => world.Content.Techs[t].Def.Id == "farming" ? 0 : 1).ThenBy(t => t);
        foreach (int tech in order)
        {
            if (!Civics.IsDiscoverable(world, Player, tech, facts) || tech == focus) continue;
            sim.Enqueue(new SetResearchFocus(world.Content.Techs[tech].Def.Id) { Player = Player });
            return;
        }
    }

    private void EstablishInstitutions(Simulation sim, Facts facts)
    {
        var world = sim.World;
        foreach (var institution in world.Content.Institutions)
        {
            if (Civics.CanEstablish(world, Player, institution.Index, facts))
                sim.Enqueue(new EstablishInstitution(institution.Def.Id) { Player = Player });
        }
    }

    // Places the next building on the list once fewer than MaxSites are under construction.
    private void PlanBuildings(Simulation sim, Entity camp, Facts facts)
    {
        var world = sim.World;
        var all = OwnBuildings(world);
        if (all.Count(b => !Buildings.IsComplete(b)) >= MaxSites) return;
        int Count(string id) => all.Count(b => b.GetComponent<Building>().Kind == world.Content.BuildingIndex(id));
        int Done(string id) => facts.Buildings[world.Content.BuildingIndex(id)];
        bool Can(string id) => world.Content.Buildings[world.Content.BuildingIndex(id)].Requires.IsMet(facts);

        int hutShelter = world.Content.Buildings[world.Content.BuildingIndex("hut")].Def.Shelter;
        int plannedShelter = Buildings.ShelterOf(world, Player) + (Count("hut") - Done("hut")) * hutShelter;
        // Once families own their homes, children are born only into huts with room: keep a few places free.
        bool families = Economy.HasHouseholds(world, Player);
        int housed = Households.Members(world, Player).Values.Sum(m => m.Count);
        bool hutsTight = families && Count("hut") * hutShelter - housed <= 3;
        string? next =
            Count("storehouse") == 0 ? "storehouse"
            : (plannedShelter - facts.Population <= 2 || hutsTight) && Count("hut") < 12 ? "hut"
            : Can("shrine") && Count("shrine") == 0 ? "shrine"
            : Count("hut") < 3 ? "hut"
            : Can("farm") && Count("farm") < Math.Max(2, facts.Population / 7) ? "farm"
            : Can("toolmaker") && Count("toolmaker") == 0 ? "toolmaker"
            : Can("marketplace") && Count("marketplace") == 0 ? "marketplace" // families need somewhere to trade
            : Can("mint") && Count("mint") == 0 ? "mint"
            : Can("mill") && Done("farm") >= 1 && Count("mill") == 0 ? "mill"
            : Can("bakery") && Done("mill") >= 1 && Count("bakery") == 0 ? "bakery"
            : facts.Era >= 1 && Count("hut") < 6 ? "hut" // settled families want homes of their own
            : null;
        if (next == null) return;
        int kind = world.Content.BuildingIndex(next);
        if (TryFindSpot(world, kind, camp.GetComponent<TilePosition>(), all, out int x, out int y))
            sim.Enqueue(new PlaceBuilding(next, x, y) { Player = Player });
    }

    // Before a chiefdom organizes labour, the bot hands out every job itself: enough people on food first, then
    // builders, workplaces and material gatherers in that order; anyone not needed goes back to gathering food.
    private void DirectLabour(Simulation sim, Entity camp, Facts facts)
    {
        var world = sim.World;
        var content = world.Content;
        var adults = World.InIdOrder(world.Store.Query<Citizen, Order, Owner>())
            .Where(u => u.GetComponent<Owner>().Player == Player && Bands.IsAdult(world, u.GetComponent<Citizen>())
                        && !u.HasComponent<Soldier>())
            .ToList();
        bool hungry = facts.Food < facts.Population * 15;
        int minFood = hungry ? (adults.Count * 2 + 2) / 3 : (facts.Population + 3) / 4;
        int capacity = Math.Max(0, adults.Count - minFood);

        // Jobs wanted, in priority order: (key, command to give, how many). Sites whose materials are all in store
        // come first, then gathering the materials others wait for, then workplaces, then one builder per other site.
        var wanted = new List<(string Key, Func<int, Command?> Order, int Count)>();
        var sites = OwnBuildings(world).Where(b => !Buildings.IsComplete(b)).ToList();
        bool Supplied(Entity site) => Buildings.MissingMaterials(world, site).Select((m, g) => m <= facts.Store[g]).All(ok => ok);
        foreach (var site in sites.Where(Supplied))
            wanted.Add(($"b{site.Id}", id => new Build([id], site.Id), 2));
        var demand = Demand.Of(world, Player);
        var campPos = camp.GetComponent<TilePosition>();
        foreach (var good in new[] { "wood", "clay", "stone" })
        {
            int g = content.GoodIndex(good);
            int need = demand.ForGood(g) + (good == "wood" ? Math.Max(0, WoodReserve - facts.Store[g]) : 0);
            int count = need <= 0 ? 0 : Math.Min(good == "wood" ? 3 : 2, 1 + need / 40);
            wanted.Add(($"g{g}", id => SourceOrder(world, g, id, campPos), count));
        }
        foreach (var building in OwnBuildings(world).Where(Buildings.IsComplete))
        {
            var type = Buildings.TypeOf(world, building);
            if (type.IsWorkplace && HasWork(world, building, type, facts))
                wanted.Add(($"b{building.Id}", id => new AssignWork([id], building.Id), type.Def.Workers));
        }
        foreach (var site in sites.Where(s => !Supplied(s)))
            wanted.Add(($"b{site.Id}", id => new Build([id], site.Id), 1));

        var desired = new Dictionary<string, int>();
        foreach (var (key, _, count) in wanted)
        {
            int take = Math.Min(count, capacity);
            desired[key] = take;
            capacity -= take;
        }

        // Current jobs; people over a job's quota (or in jobs no longer wanted) are released.
        var pool = new List<int>();
        var foragers = new List<int>();
        var have = new Dictionary<string, int>();
        foreach (var unit in adults)
        {
            var order = unit.GetComponent<Order>();
            string? key = order.Kind is OrderKind.Build or OrderKind.Work ? $"b{order.Target}"
                : GatheredGood(world, order) is int g and >= 0 ? $"g{g}" : null;
            if (key == null)
            {
                if (IsForaging(order)) foragers.Add(unit.Id);
                else if (order.Kind == OrderKind.Idle) pool.Add(unit.Id);
                continue;
            }
            have[key] = have.GetValueOrDefault(key) + 1;
            if (have[key] > desired.GetValueOrDefault(key)) pool.Add(unit.Id);
        }
        int spareForagers = Math.Max(0, foragers.Count - minFood);

        foreach (var (key, order, _) in wanted)
        {
            for (int n = Math.Min(have.GetValueOrDefault(key), desired[key]); n < desired[key]; n++)
            {
                int id;
                if (pool.Count > 0) { id = pool[0]; pool.RemoveAt(0); }
                else if (spareForagers > 0) { id = foragers[^1]; foragers.RemoveAt(foragers.Count - 1); spareForagers--; }
                else break;
                if (order(id) is { } command) sim.Enqueue(command with { Player = Player });
                else pool.Add(id);
            }
        }
        _forager.SendToFood(sim, pool);
    }

    private static Command? SourceOrder(World world, int good, int unit, TilePosition from)
    {
        if (Sources.TryFindDepositOf(world, good, from.X, from.Y, 40, out var deposit)) return new Gather([unit], deposit.Id);
        if (Sources.TryFindTile(world, good, -1, from.X, from.Y, 40, out int x, out int y)) return new GatherTile([unit], x, y);
        return null;
    }

    private static bool HasWork(World world, Entity building, Content.BuildingType type, Facts facts)
    {
        if (type.Def.Research > 0)
            return Enumerable.Range(0, world.Content.Techs.Count).Any(t => Civics.IsDiscoverable(world, building.GetComponent<Owner>().Player, t, facts));
        if (type.Def.Field != null) return true;
        for (int g = 0; g < type.Inputs.Count; g++)
        {
            if (type.Inputs[g] > facts.Store[g] + building.GetComponent<Inventory>().Amounts[g]) return false;
        }
        return true;
    }

    private static bool IsForaging(in Order order) =>
        order.Kind == OrderKind.Hunt || (order.Kind == OrderKind.Gather && order.TargetType is TargetType.Plant or TargetType.Carcass);

    private static int GatheredGood(World world, in Order order) => order.Kind != OrderKind.Gather ? -1 : order.TargetType switch
    {
        TargetType.Deposit => world.Content.DepositSource(order.TargetKind).Good,
        TargetType.Tile => world.Content.TerrainSource(order.TargetKind)?.Good ?? -1,
        _ => -1,
    };

    private List<Entity> OwnBuildings(World world) =>
        World.InIdOrder(world.Store.Query<Building, Owner>()).Where(b => b.GetComponent<Owner>().Player == Player).ToList();

    // The best free spot near camp: close by, with a one-tile path around every building, and fertile soil for farms.
    private bool TryFindSpot(World world, int kind, TilePosition camp, List<Entity> buildings, out int bestX, out int bestY)
    {
        var type = world.Content.Buildings[kind];
        int size = type.Def.Size;
        bool farm = type.Def.Field != null;
        int bestScore = int.MaxValue;
        bestX = bestY = 0;
        for (int ring = 2; ring <= 16; ring++)
        {
            for (int y = camp.Y - ring; y <= camp.Y + ring; y++)
            {
                for (int x = camp.X - ring; x <= camp.X + ring; x++)
                {
                    if (Math.Max(Math.Abs(x - camp.X), Math.Abs(y - camp.Y)) != ring) continue;
                    if (buildings.Any(b => Overlaps(world, b, x - 1, y - 1, size + 2))) continue;
                    if (Buildings.CanPlace(world, Player, kind, x, y) != Placement.Ok) continue;
                    int score = ring * 10 - (farm ? AverageFertility(world, x, y, size) * 2 : 0);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    (bestX, bestY) = (x, y);
                }
            }
            if (bestScore != int.MaxValue && !farm) break; // the nearest ring with room will do
        }
        return bestScore != int.MaxValue;
    }

    private static bool Overlaps(World world, Entity building, int x, int y, int size)
    {
        var pos = building.GetComponent<TilePosition>();
        int other = Buildings.SizeOf(world, building);
        return pos.X <= x + size - 1 && x <= pos.X + other - 1 && pos.Y <= y + size - 1 && y <= pos.Y + other - 1;
    }

    private static int AverageFertility(World world, int x, int y, int size)
    {
        int sum = 0;
        for (int ty = y; ty < y + size; ty++)
        {
            for (int tx = x; tx < x + size; tx++) sum += world.Map.GetFertility(tx, ty);
        }
        return sum / (size * size);
    }
}
