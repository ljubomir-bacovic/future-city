using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Carries out citizens' orders: gathering and hunting, building, work at farms, workshops, shrines, mints and
/// marketplaces, and trips to market. Goods are carried by people: gatherers walk loads to the nearest store,
/// builders and craftsmen fetch materials from stores, so distance sets how much gets done. Once families own their
/// goods, people working for their family carry loads home and fetch from home instead, and loads owed as tribute go
/// to the nearest public store. When a source runs out, workers move on to the nearest similar one nearby, so an
/// unattended band keeps stripping the area around it.
/// </summary>
public sealed partial class OrderSystem : ISimSystem
{
    /// <summary>How far (in tiles) a worker looks for a similar source when theirs runs out.</summary>
    public const int RetargetRadius = 8;

    // Hunters re-plan toward a moving animal at most this often.
    private const int ChaseReplanInterval = 5;

    /// <inheritdoc />
    public void Update(World world)
    {
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order, TilePosition, Mover, Owner>()))
        {
            switch (unit.GetComponent<Order>().Kind)
            {
                case OrderKind.Move:
                    if (!unit.GetComponent<Mover>().Moving) unit.GetComponent<Order>() = default;
                    break;
                case OrderKind.ReturnToCamp:
                    UpdateReturnToCamp(world, unit);
                    break;
                case OrderKind.Gather:
                    UpdateGather(world, unit);
                    break;
                case OrderKind.Hunt:
                    UpdateHunt(world, unit);
                    break;
                case OrderKind.Build:
                    UpdateBuild(world, unit);
                    break;
                case OrderKind.Work:
                    UpdateWork(world, unit);
                    break;
                case OrderKind.Trade:
                    UpdateTrade(world, unit);
                    break;
                case OrderKind.Arm:
                    UpdateArm(world, unit);
                    break;
                case OrderKind.Loot:
                    UpdateLoot(world, unit);
                    break;
            }
        }
    }

    private static void UpdateReturnToCamp(World world, Entity unit)
    {
        // Someone working for their family takes what they carry home rather than to the camp.
        var result = Households.TryGetEmployer(world, unit, out _) ? Deliver(world, unit) : GoToCamp(world, unit);
        if (result != Progress.Underway) unit.GetComponent<Order>() = default;
    }

    private enum Progress { Underway, Arrived, Failed }

    // Walks toward a single tile until within `reach` tiles of it.
    private static Progress Approach(World world, Entity unit, TilePosition goal, int reach, bool replanEveryTick) =>
        Approach(world, unit, goal.X, goal.Y, goal.X, goal.Y, reach, replanEveryTick);

    // Walks toward an entity's footprint (a whole building, or one tile) until within `reach` tiles of it.
    private static Progress ApproachEntity(World world, Entity unit, Entity target, int reach)
    {
        var pos = target.GetComponent<TilePosition>();
        int last = Buildings.SizeOf(world, target) - 1;
        return Approach(world, unit, pos.X, pos.Y, pos.X + last, pos.Y + last, reach, replanEveryTick: true);
    }

    // Walks toward the rectangle (x0, y0)-(x1, y1) until within `reach` tiles of it. A unit already walking to a tile
    // inside the rectangle keeps its route; otherwise it re-plans when it is heading elsewhere (if allowed).
    private static Progress Approach(World world, Entity unit, int x0, int y0, int x1, int y1, int reach, bool replanEveryTick)
    {
        ref var mover = ref unit.GetComponent<Mover>();
        var pos = unit.GetComponent<TilePosition>();
        int dx = pos.X < x0 ? x0 - pos.X : pos.X > x1 ? pos.X - x1 : 0;
        int dy = pos.Y < y0 ? y0 - pos.Y : pos.Y > y1 ? pos.Y - y1 : 0;
        if (Math.Max(dx, dy) <= reach)
        {
            Movement.Stop(ref mover, pos);
            return mover.Moving ? Progress.Underway : Progress.Arrived;
        }
        bool headingThere = mover.Moving && mover.GoalX >= x0 && mover.GoalX <= x1 && mover.GoalY >= y0 && mover.GoalY <= y1;
        if (headingThere) return Progress.Underway;
        if (mover.Moving && !replanEveryTick) return Progress.Underway; // keep chasing the old spot for now
        int gx = Math.Clamp(pos.X, x0, x1), gy = Math.Clamp(pos.Y, y0, y1);
        if (!Movement.SetGoal(world, ref mover, pos, gx, gy) && !mover.Moving) return Progress.Failed;
        return Progress.Underway;
    }

    // Walks to the owner's camp and drops off what is carried on arrival.
    private static Progress GoToCamp(World world, Entity unit)
    {
        if (!Bands.TryGetCamp(world, unit.GetComponent<Owner>().Player, out var camp)) return Progress.Failed;
        var result = ApproachEntity(world, unit, camp, reach: 1);
        if (result != Progress.Arrived) return result;
        DropOff(world, unit, camp);
        return Progress.Arrived;
    }

    // Walks what is carried to where it belongs: the worker's home, or the nearest public store (for public work, for
    // people living at the camp, and for loads owed as tribute).
    private static Progress Deliver(World world, Entity unit)
    {
        if (unit.GetComponent<Citizen>().Carried == 0) return Progress.Arrived;
        if (!TryFindDropOff(world, unit, out var store)) return Progress.Failed;
        var result = ApproachEntity(world, unit, store, reach: 1);
        if (result != Progress.Arrived) return result;
        DropOff(world, unit, store);
        return Progress.Arrived;
    }

    private static bool TryFindDropOff(World world, Entity unit, out Entity store)
    {
        var pos = unit.GetComponent<TilePosition>();
        int player = unit.GetComponent<Owner>().Player;
        if (!Households.TryGetEmployer(world, unit, out var home))
            return Stores.TryFindNearest(world, player, pos.X, pos.Y, out store);
        var citizen = unit.GetComponent<Citizen>();
        bool owesThisLoad = unit.GetComponent<Order>().Kind != OrderKind.Trade
                            && home.GetComponent<Household>().TributeOwed[citizen.CarriedGood] >= citizen.Carried * 100;
        if (owesThisLoad && Stores.TryFindNearest(world, player, pos.X, pos.Y, out store)) return true;
        store = home;
        return true;
    }

    private static void DropOff(World world, Entity unit, Entity store)
    {
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (citizen.Carried > 0)
        {
            Stores.Put(store, citizen.CarriedGood, citizen.Carried);
            if (unit.GetComponent<Order>().Kind == OrderKind.Loot) Combat.BringLoot(world, unit.GetComponent<Owner>().Player, citizen.Carried);
            if (unit.GetComponent<Order>().Kind != OrderKind.Trade && Households.TryGetEmployer(world, unit, out var home))
                Tribute(world, unit, home, store, citizen.CarriedGood, citizen.Carried);
        }
        citizen.Carried = 0;
        Labor.PickUpTool(world, unit, store);
    }

    // A family owes tribute on what its members bring home, and pays it by delivering whole loads to public stores.
    private static void Tribute(World world, Entity unit, Entity home, Entity store, int good, int amount)
    {
        ref var household = ref home.GetComponent<Household>();
        int player = unit.GetComponent<Owner>().Player;
        if (store.Id == home.Id)
        {
            if (Civics.TryGet(world, player, out var civ))
                household.TributeOwed[good] += amount * civ.GetComponent<Civilization>().TributePercent;
            return;
        }
        household.TributeOwed[good] = Math.Max(0, household.TributeOwed[good] - amount * 100);
        Economy.Record(world, player, LedgerEntry.Tribute, amount);
    }

    // Where a worker takes a good from: their home (working for their family) or the nearest public store holding it.
    private static bool TryFindSupply(World world, Entity unit, int good, out Entity store)
    {
        if (Households.TryGetEmployer(world, unit, out store))
            return store.GetComponent<Inventory>().Amounts[good] > 0;
        var pos = unit.GetComponent<TilePosition>();
        return Stores.TryFindNearest(world, unit.GetComponent<Owner>().Player, pos.X, pos.Y, out store, good);
    }

    // Walks to where the order's good is kept and takes up to `amount` of it (at most a full load).
    private static Progress Fetch(World world, Entity unit, int amount)
    {
        ref var order = ref unit.GetComponent<Order>();
        if (!TryFindSupply(world, unit, order.Good, out var store))
            return Progress.Failed;
        var result = ApproachEntity(world, unit, store, reach: 1);
        if (result != Progress.Arrived) return result;
        ref var citizen = ref unit.GetComponent<Citizen>();
        var amounts = store.GetComponent<Inventory>().Amounts;
        int take = Math.Min(Math.Min(amount, amounts[order.Good]), world.Content.Citizens.CarryCapacity);
        amounts[order.Good] -= take;
        citizen.CarriedGood = order.Good;
        citizen.Carried = take;
        Labor.PickUpTool(world, unit, store);
        return take > 0 ? Progress.Arrived : Progress.Failed;
    }

    // Nothing left to do here: bring home what was gathered, or stand by.
    private static void GiveUp(Entity unit, ref Order order, in Citizen citizen)
    {
        Movement.Stop(ref unit.GetComponent<Mover>(), unit.GetComponent<TilePosition>());
        order = citizen.Carried > 0 ? new Order { Kind = OrderKind.ReturnToCamp, Public = order.Public } : default;
    }
}
