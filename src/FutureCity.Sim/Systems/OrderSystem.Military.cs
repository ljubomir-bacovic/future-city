using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

public sealed partial class OrderSystem
{
    // Soldiers waiting for equipment look for a store that has it this often.
    private const int ArmouryCheckInterval = 10;

    // A new soldier drops off what they carry, collects their equipment at a public store that holds all of it, and
    // walks to the rally point.
    private static void UpdateArm(World world, Entity unit)
    {
        if (unit.GetComponent<Citizen>().Carried > 0)
        {
            Deliver(world, unit);
            return;
        }
        ref var soldier = ref unit.GetComponent<Soldier>();
        if (soldier.Equipped)
        {
            GoToRally(world, unit);
            return;
        }

        ref var order = ref unit.GetComponent<Order>();
        if (order.Target == 0 || !world.TryGetEntity(order.Target, out var store) || !HoldsAll(world, store, soldier.Kind))
        {
            if ((world.Tick + unit.Id) % ArmouryCheckInterval != 0) return;
            if (!TryFindArmoury(world, unit, soldier.Kind, out store))
            {
                order.Target = 0; // wait until the treasury has the equipment
                return;
            }
            order.Target = store.Id;
            order.TargetType = TargetType.Building;
        }

        if (ApproachEntity(world, unit, store, reach: 1) != Progress.Arrived) return;
        var amounts = store.GetComponent<Inventory>().Amounts;
        var equipment = world.Content.Units[soldier.Kind].Equipment;
        for (int g = 0; g < amounts.Length; g++) amounts[g] -= equipment[g];
        soldier.Equipped = true;
        GoToRally(world, unit);
    }

    // A soldier carries goods off from an enemy store or ruins, a load at a time (the most valuable first), to their own
    // nearest public store, until nothing is left or the target is gone.
    private static void UpdateLoot(World world, Entity unit)
    {
        if (unit.GetComponent<Citizen>().Carried > 0)
        {
            if (Deliver(world, unit) == Progress.Failed) unit.GetComponent<Order>() = new Order { Public = true };
            return;
        }
        ref var order = ref unit.GetComponent<Order>();
        int player = unit.GetComponent<Owner>().Player;
        int good = -1;
        if (!world.TryGetEntity(order.Target, out var target) || !Combat.CanLoot(world, player, target)
            || (good = Combat.RichestGood(world, target)) < 0)
        {
            order = new Order { Public = true };
            return;
        }
        var arrived = ApproachEntity(world, unit, target, reach: 1);
        if (arrived == Progress.Failed) { order = new Order { Public = true }; return; }
        if (arrived == Progress.Underway) return;
        var amounts = target.GetComponent<Inventory>().Amounts;
        int take = Math.Min(amounts[good], world.Content.Citizens.CarryCapacity);
        amounts[good] -= take;
        ref var citizen = ref unit.GetComponent<Citizen>();
        citizen.CarriedGood = good;
        citizen.Carried = take;
        if (target.TryGetComponent<Owner>(out var owner) && owner.Player != Players.Nature)
            Combat.LoseToLooters(world, owner.Player, target, take);
    }

    private static bool HoldsAll(World world, Entity store, int kind)
    {
        if (!Stores.IsStore(world, store)) return false;
        var amounts = store.GetComponent<Inventory>().Amounts;
        var equipment = world.Content.Units[kind].Equipment;
        for (int g = 0; g < amounts.Length; g++)
        {
            if (amounts[g] < equipment[g]) return false;
        }
        return true;
    }

    // The nearest reachable public store holding the whole equipment; ties go to the lowest id.
    private static bool TryFindArmoury(World world, Entity unit, int kind, out Entity armoury)
    {
        var pos = unit.GetComponent<TilePosition>();
        armoury = default;
        int best = int.MaxValue;
        foreach (var store in Stores.Of(world, unit.GetComponent<Owner>().Player))
        {
            var at = store.GetComponent<TilePosition>();
            if (!HoldsAll(world, store, kind) || !world.CanReach(pos.X, pos.Y, at.X, at.Y)) continue;
            int distance = Buildings.DistanceTo(world, store, pos.X, pos.Y);
            if (distance >= best) continue;
            best = distance;
            armoury = store;
        }
        return best != int.MaxValue;
    }

    // Armed soldiers gather at the rally point, or wait where they are if there is none.
    private static void GoToRally(World world, Entity unit)
    {
        int player = unit.GetComponent<Owner>().Player;
        if (!Civics.TryGet(world, player, out var civEntity) || !civEntity.GetComponent<Civilization>().HasRally)
        {
            unit.GetComponent<Order>() = new Order { Public = true };
            return;
        }
        var civ = civEntity.GetComponent<Civilization>();
        // Soldiers at or on their way to the rally point stand around it rather than all on one tile.
        int waiting = 0;
        foreach (var other in world.Store.Query<Soldier, Owner, Mover>().Entities)
        {
            var mover = other.GetComponent<Mover>();
            if (other.Id != unit.Id && other.GetComponent<Owner>().Player == player
                && Math.Max(Math.Abs(mover.GoalX - civ.RallyX), Math.Abs(mover.GoalY - civ.RallyY)) <= 2)
                waiting++;
        }
        var spot = UnitOrders.SpreadAround(world, civ.RallyX, civ.RallyY, waiting + 1)[waiting];
        unit.GetComponent<Order>() = new Order { Kind = OrderKind.Move, Public = true };
        Movement.SetGoal(world, ref unit.GetComponent<Mover>(), unit.GetComponent<TilePosition>(), spot.X, spot.Y);
    }
}
