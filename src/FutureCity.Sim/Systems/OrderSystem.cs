using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Carries out citizens' orders: walking to targets, gathering, hunting and bringing food back to camp.
/// When a source runs out, gatherers and hunters move on to the nearest similar one nearby, so an
/// unattended band keeps stripping the area around it.
/// </summary>
public sealed class OrderSystem : ISimSystem
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
            }
        }
    }

    private static void UpdateReturnToCamp(World world, Entity unit)
    {
        var result = GoToCamp(world, unit);
        if (result != Progress.Underway) unit.GetComponent<Order>() = default;
    }

    private static void UpdateGather(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        var rules = world.Content.Citizens;

        if (order.Stage == OrderStage.Deliver)
        {
            var result = GoToCamp(world, unit);
            if (result == Progress.Underway) return;
            if (result == Progress.Failed) { order = default; return; }
            order.Stage = OrderStage.Travel; // delivered: back to work
        }

        if (!world.TryGetEntity(order.Target, out var target) || !FoodSources.HasGatherableFood(target))
        {
            if (citizen.CarriedFood > 0) { order.Stage = OrderStage.Deliver; return; } // bring this load home first
            if (!Retarget(world, ref order)) { GiveUp(unit, ref order, citizen); return; }
            if (order.Kind == OrderKind.Hunt) return;
            world.TryGetEntity(order.Target, out target);
        }

        if (citizen.CarriedFood >= rules.CarryCapacity)
        {
            order.Stage = OrderStage.Deliver;
            return;
        }

        var targetPos = target.GetComponent<TilePosition>();
        (order.TargetX, order.TargetY) = (targetPos.X, targetPos.Y);
        if (order.Stage == OrderStage.Travel)
        {
            var travel = Approach(world, unit, targetPos, reach: 0, replanEveryTick: true);
            if (travel == Progress.Failed) { GiveUp(unit, ref order, citizen); return; }
            if (travel == Progress.Underway) return;
            order.Stage = OrderStage.Work;
            order.Timer = 0;
        }

        // Working at the source.
        int ticksPerFood = target.TryGetComponent<Plant>(out var p)
            ? world.Content.Plants[p.Kind].TicksPerFood
            : world.Content.Animals[target.GetComponent<Carcass>().Kind].ButcherTicksPerFood;
        if (++order.Timer < ticksPerFood) return;
        order.Timer = 0;
        citizen.CarriedFood++;
        if (target.HasComponent<Plant>())
        {
            ref var plant = ref target.GetComponent<Plant>();
            if (--plant.Food == 0)
                plant.DormantUntil = world.Tick + world.Content.Plants[plant.Kind].DormantTicks;
        }
        else
        {
            ref var carcass = ref target.GetComponent<Carcass>();
            if (--carcass.Food == 0)
                target.DeleteEntity();
        }
        if (citizen.CarriedFood >= rules.CarryCapacity)
            order.Stage = OrderStage.Deliver;
    }

    private static void UpdateHunt(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        bool exists = world.TryGetEntity(order.Target, out var target);
        if (!exists || !target.HasComponent<Animal>())
        {
            if (exists && target.HasComponent<Carcass>())
            {
                // Someone else made the kill: help butcher.
                order = order with { Kind = OrderKind.Gather, Stage = OrderStage.Travel, Timer = 0 };
                return;
            }
            if (FoodSources.TryFindAnimal(world, order.TargetKind, order.TargetX, order.TargetY, RetargetRadius, out var next))
            {
                order = order with { Target = next.Id, Stage = OrderStage.Travel, Timer = 0 };
                return;
            }
            GiveUp(unit, ref order, unit.GetComponent<Citizen>());
            return;
        }

        var animalPos = target.GetComponent<TilePosition>();
        (order.TargetX, order.TargetY) = (animalPos.X, animalPos.Y);
        if (order.Stage == OrderStage.Travel)
        {
            bool replan = (world.Tick + unit.Id) % ChaseReplanInterval == 0;
            var travel = Approach(world, unit, animalPos, reach: 1, replanEveryTick: replan);
            if (travel == Progress.Failed) { GiveUp(unit, ref order, unit.GetComponent<Citizen>()); return; }
            if (travel == Progress.Underway) return;
            order.Stage = OrderStage.Work;
            order.Timer = 0;
        }

        if (unit.GetComponent<TilePosition>().DistanceTo(animalPos.X, animalPos.Y) > 1)
        {
            order.Stage = OrderStage.Travel; // it got away
            return;
        }

        var kind = world.Content.Animals[target.GetComponent<Animal>().Kind];
        if (++order.Timer < kind.KillTicks) return;

        int animalKind = target.GetComponent<Animal>().Kind;
        target.RemoveComponent<Animal>();
        target.RemoveComponent<Mover>();
        target.AddComponent(new Carcass { Kind = animalKind, Food = kind.Food });
        world.Emit(SimEventKind.AnimalKilled, unit.GetComponent<Owner>().Player, target.Id, animalPos.X, animalPos.Y);
        unit.GetComponent<Order>() = order with { Kind = OrderKind.Gather, Stage = OrderStage.Travel, Target = target.Id, Timer = 0 };
    }

    private enum Progress { Underway, Arrived, Failed }

    // Walks toward a tile until within `reach` tiles of it. Re-plans when the mover is heading elsewhere.
    private static Progress Approach(World world, Entity unit, TilePosition goal, int reach, bool replanEveryTick)
    {
        ref var mover = ref unit.GetComponent<Mover>();
        var pos = unit.GetComponent<TilePosition>();
        if (pos.DistanceTo(goal.X, goal.Y) <= reach)
        {
            Movement.Stop(ref mover, pos);
            return mover.Moving ? Progress.Underway : Progress.Arrived;
        }
        bool headingThere = mover.Moving && mover.GoalX == goal.X && mover.GoalY == goal.Y;
        if (headingThere) return Progress.Underway;
        if (mover.Moving && !replanEveryTick) return Progress.Underway; // keep chasing the old spot for now
        if (!Movement.SetGoal(world, ref mover, pos, goal.X, goal.Y) && !mover.Moving) return Progress.Failed;
        return Progress.Underway;
    }

    // Walks to the owner's camp and drops off food on arrival.
    private static Progress GoToCamp(World world, Entity unit)
    {
        if (!Bands.TryGetCamp(world, unit.GetComponent<Owner>().Player, out var camp)) return Progress.Failed;
        var result = Approach(world, unit, camp.GetComponent<TilePosition>(), reach: 1, replanEveryTick: true);
        if (result != Progress.Arrived) return result;
        ref var citizen = ref unit.GetComponent<Citizen>();
        camp.GetComponent<Camp>().Food += citizen.CarriedFood;
        citizen.CarriedFood = 0;
        return Progress.Arrived;
    }

    // Finds a similar source where the old one was. Butchers with nothing left to cut go hunting again.
    private static bool Retarget(World world, ref Order order)
    {
        int x = order.TargetX, y = order.TargetY;
        if (order.TargetIsPlant
                ? FoodSources.TryFindPlant(world, order.TargetKind, x, y, RetargetRadius, out var next)
                : FoodSources.TryFindCarcass(world, x, y, RetargetRadius, out next))
        {
            order = order with { Target = next.Id, Stage = OrderStage.Travel, Timer = 0 };
            return true;
        }
        if (!order.TargetIsPlant && FoodSources.TryFindAnimal(world, order.TargetKind, x, y, RetargetRadius, out next))
        {
            order = order with { Kind = OrderKind.Hunt, Target = next.Id, Stage = OrderStage.Travel, Timer = 0 };
            return true;
        }
        return false;
    }

    // Nothing left to do here: bring home what was gathered, or stand by.
    private static void GiveUp(Entity unit, ref Order order, in Citizen citizen)
    {
        Movement.Stop(ref unit.GetComponent<Mover>(), unit.GetComponent<TilePosition>());
        order = citizen.CarriedFood > 0 ? new Order { Kind = OrderKind.ReturnToCamp } : default;
    }
}
