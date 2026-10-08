using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

// Gathering from plants, carcasses, deposits and forest tiles, and hunting.
public sealed partial class OrderSystem
{
    private static void UpdateGather(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        int capacity = world.Content.Citizens.CarryCapacity;

        if (order.Stage == OrderStage.Deliver)
        {
            var result = Deliver(world, unit);
            if (result == Progress.Underway) return;
            if (result == Progress.Failed) { GiveUp(unit, ref order, citizen); return; }
            order.Stage = OrderStage.Travel; // delivered: back to work
        }

        if (!TryGetSource(world, order, out var target, out var info))
        {
            if (citizen.Carried > 0) { order.Stage = OrderStage.Deliver; return; } // bring this load home first
            if (!Retarget(world, ref order)) { GiveUp(unit, ref order, citizen); return; }
            if (order.Kind == OrderKind.Hunt) return;
            TryGetSource(world, order, out target, out info);
        }

        if (citizen.Carried >= capacity || (citizen.Carried > 0 && citizen.CarriedGood != info.Good))
        {
            order.Stage = OrderStage.Deliver;
            return;
        }

        if (order.TargetType != TargetType.Tile)
        {
            var targetPos = target.GetComponent<TilePosition>();
            (order.TargetX, order.TargetY) = (targetPos.X, targetPos.Y);
        }
        if (order.Stage == OrderStage.Travel)
        {
            var travel = Approach(world, unit, new TilePosition(order.TargetX, order.TargetY), reach: 0, replanEveryTick: true);
            if (travel == Progress.Failed) { GiveUp(unit, ref order, citizen); return; }
            if (travel == Progress.Underway) return;
            order.Stage = OrderStage.Work;
            order.Timer = 0;
        }

        // Working at the source.
        order.Timer += Labor.Work(world, unit, info.Work);
        int perUnit = info.TicksPerUnit * Labor.PerTick;
        if (order.Timer < perUnit) return;
        order.Timer -= perUnit;
        TakeOne(world, order, target);
        citizen.CarriedGood = info.Good;
        citizen.Carried++;
        Civics.RecordGathered(world, unit.GetComponent<Owner>().Player, info.Good, 1);
        if (citizen.Carried >= capacity)
            order.Stage = OrderStage.Deliver;
    }

    // The order's source, if it still has something to take.
    private static bool TryGetSource(World world, in Order order, out Entity target, out SourceInfo info)
    {
        target = default;
        info = default;
        if (order.TargetType == TargetType.Tile)
        {
            if (Sources.TileInfo(world, order.TargetX, order.TargetY) is not { } tile
                || world.Map.GetTerrain(order.TargetX, order.TargetY) != order.TargetKind)
                return false;
            info = tile;
            return true;
        }
        if (!world.TryGetEntity(order.Target, out target) || !Sources.HasGoods(target)) return false;
        info = Sources.InfoOf(world.Content, target);
        return true;
    }

    private static void TakeOne(World world, in Order order, Entity target)
    {
        if (order.TargetType == TargetType.Tile)
        {
            int left = world.Map.GetResource(order.TargetX, order.TargetY) - 1;
            world.Map.SetResource(order.TargetX, order.TargetY, left);
            if (left == 0) // cleared: the forest is gone from this tile
                world.Map.SetTerrain(order.TargetX, order.TargetY, world.Content.DepletedTerrain(order.TargetKind));
        }
        else if (target.HasComponent<Plant>())
        {
            ref var plant = ref target.GetComponent<Plant>();
            if (--plant.Food == 0)
                plant.DormantUntil = world.Tick + world.Content.Plants[plant.Kind].DormantTicks;
        }
        else if (target.HasComponent<Carcass>())
        {
            if (--target.GetComponent<Carcass>().Food == 0) target.DeleteEntity();
        }
        else if (--target.GetComponent<Deposit>().Amount == 0)
        {
            target.DeleteEntity();
        }
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
                order = order with { Kind = OrderKind.Gather, TargetType = TargetType.Carcass, Stage = OrderStage.Travel, Timer = 0 };
                return;
            }
            if (Sources.TryFindAnimal(world, order.TargetKind, order.TargetX, order.TargetY, RetargetRadius, out var next))
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
        order.Timer += Labor.Work(world, unit, WorkKind.Hunt);
        if (order.Timer < kind.KillTicks * Labor.PerTick) return;

        int animalKind = target.GetComponent<Animal>().Kind;
        target.RemoveComponent<Animal>();
        target.RemoveComponent<Mover>();
        target.AddComponent(new Carcass { Kind = animalKind, Food = kind.Food });
        world.Emit(SimEventKind.AnimalKilled, unit.GetComponent<Owner>().Player, target.Id, animalPos.X, animalPos.Y);
        unit.GetComponent<Order>() = order with
        {
            Kind = OrderKind.Gather, TargetType = TargetType.Carcass, Stage = OrderStage.Travel, Target = target.Id, Timer = 0,
        };
    }

    // Finds a similar source where the old one was. Butchers with nothing left to cut go hunting again.
    private static bool Retarget(World world, ref Order order)
    {
        int x = order.TargetX, y = order.TargetY;
        Entity next = default;
        bool found = order.TargetType switch
        {
            TargetType.Plant => Sources.TryFindPlant(world, order.TargetKind, x, y, RetargetRadius, out next),
            TargetType.Carcass => Sources.TryFindCarcass(world, x, y, RetargetRadius, out next),
            TargetType.Deposit => Sources.TryFindDeposit(world, order.TargetKind, x, y, RetargetRadius, out next),
            _ => false,
        };
        if (found)
        {
            order = order with { Target = next.Id, Stage = OrderStage.Travel, Timer = 0 };
            return true;
        }
        if (order.TargetType == TargetType.Tile && world.Content.TerrainSource(order.TargetKind) is { } info
            && Sources.TryFindTile(world, info.Good, order.TargetKind, x, y, RetargetRadius, out int tx, out int ty))
        {
            order = order with { TargetX = tx, TargetY = ty, Stage = OrderStage.Travel, Timer = 0 };
            return true;
        }
        if (order.TargetType == TargetType.Carcass && Sources.TryFindAnimal(world, order.TargetKind, x, y, RetargetRadius, out next))
        {
            order = order with { Kind = OrderKind.Hunt, TargetType = TargetType.Animal, Target = next.Id, Stage = OrderStage.Travel, Timer = 0 };
            return true;
        }
        return false;
    }
}
