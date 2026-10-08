using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Fighting. Soldiers with an attack order chase their target and strike it whenever their weapon is ready; soldiers on
/// an attack-move walk on but fight every enemy they see; idle soldiers fight enemies that come within sight; towers shoot
/// at enemies in range. Hits take health (people) or hit points (buildings), and shake a soldier's morale; a soldier
/// whose morale breaks flees home for a while. The dead are counted as losses of their civilization, destroyed
/// buildings leave ruins to loot (see <see cref="Combat"/>). Ruins and spilled cargo disappear after a while.
/// </summary>
public sealed class CombatSystem : ISimSystem
{
    // Soldiers on their own initiative (not ordered) give up a chase this far beyond their sight.
    private const int LeashFactor = 2;

    // Chasing soldiers re-plan toward a moving target at most this often.
    private const int ChaseReplanInterval = 5;

    /// <inheritdoc />
    public void Update(World world)
    {
        bool war = Relations.AnyWar(world);
        var soldiers = World.InIdOrder(world.Store.Query<Soldier, Order, TilePosition, Mover, Owner>());
        var targets = war ? Targets(world) : [];
        var dead = new HashSet<int>();
        foreach (var unit in soldiers)
        {
            if (dead.Contains(unit.Id)) continue;
            UpdateSoldier(world, unit, war, targets, dead);
        }
        if (war) UpdateTowers(world, targets, dead);
        if (world.Tick % 50 == 0) DecayLoot(world);
    }

    // Everything that can be fought, once per tick: people, buildings and merchant caravans.
    private static List<Entity> Targets(World world)
    {
        var list = new List<Entity>();
        foreach (var entity in World.InIdOrder(world.Store.Query<TilePosition>()))
        {
            if (entity.HasComponent<Citizen>() || entity.HasComponent<Building>() || entity.HasComponent<Merchant>()) list.Add(entity);
        }
        return list;
    }

    private static void UpdateSoldier(World world, Entity unit, bool war, List<Entity> targets, HashSet<int> dead)
    {
        ref var soldier = ref unit.GetComponent<Soldier>();
        if (!soldier.Equipped) return; // still collecting equipment (the order system walks them to it)
        var type = world.Content.Units[soldier.Kind];
        ref var mover = ref unit.GetComponent<Mover>();
        var pos = unit.GetComponent<TilePosition>();
        if (!mover.Moving && mover.TicksPerTile != type.Def.TicksPerTile) mover.TicksPerTile = type.Def.TicksPerTile; // a march at a slower comrade's pace is over

        if (soldier.RoutUntil != 0)
        {
            if (world.Tick < soldier.RoutUntil)
            {
                Flee(world, unit);
                return;
            }
            soldier.RoutUntil = 0;
            soldier.Morale = Math.Max(soldier.Morale, Military.BaseMorale(world, soldier.Kind, soldier.Service) / 2);
            Movement.Stop(ref mover, pos);
        }

        int player = unit.GetComponent<Owner>().Player;
        ref var order = ref unit.GetComponent<Order>();
        var combat = world.Content.Military.Combat;
        bool look = war && (world.Tick + unit.Id) % combat.TargetIntervalTicks == 0;
        switch (order.Kind)
        {
            case OrderKind.Attack:
                if (!IsValidTarget(world, player, order.Target, type, dead, out var target)
                    || (order.Auto && Buildings.DistanceTo(world, target, pos.X, pos.Y) > type.Def.SightRadius * LeashFactor))
                {
                    Movement.Stop(ref mover, pos);
                    order = new Order { Public = true };
                    return;
                }
                Engage(world, unit, target, targets, dead);
                return;
            case OrderKind.AttackMove:
                if (order.Target != 0 && IsValidTarget(world, player, order.Target, type, dead, out var foe)
                    && Buildings.DistanceTo(world, foe, pos.X, pos.Y) <= type.Def.SightRadius * LeashFactor)
                {
                    Engage(world, unit, foe, targets, dead);
                    return;
                }
                order.Target = 0;
                if (look && TryAcquire(world, unit, type, type.Def.SightRadius, targets, dead, out foe))
                {
                    order.Target = foe.Id;
                    Engage(world, unit, foe, targets, dead);
                    return;
                }
                if (pos.X == order.TargetX && pos.Y == order.TargetY && !mover.Moving)
                    order = new Order { Public = true };
                else if (!mover.Moving || mover.GoalX != order.TargetX || mover.GoalY != order.TargetY)
                {
                    // Back on the march after a fight (or stopped short): head for its place again.
                    if (!Movement.SetGoal(world, ref mover, pos, order.TargetX, order.TargetY) && !mover.Moving
                        && !Breach(world, unit, type, targets, dead))
                        order = new Order { Public = true };
                }
                return;
            case OrderKind.Idle:
                if (look && TryAcquire(world, unit, type, type.Def.SightRadius, targets, dead, out foe))
                {
                    order = new Order
                    {
                        Kind = OrderKind.Attack, Target = foe.Id,
                        TargetType = foe.HasComponent<Building>() ? TargetType.Building : TargetType.Unit, Auto = true, Public = true,
                    };
                    Engage(world, unit, foe, targets, dead);
                }
                return;
        }
    }

    private static bool IsValidTarget(World world, int player, int id, UnitType type, HashSet<int> dead, out Entity target)
    {
        target = default;
        if (dead.Contains(id) || !world.TryGetEntity(id, out target) || !Combat.IsEnemy(world, player, target)) return false;
        return type.Role != UnitRole.Siege || target.HasComponent<Building>();
    }

    // The nearest enemy within `radius`: people (and caravans) before buildings, except for siege engines, which only
    // attack buildings. Walls are left alone unless they block the way (see Breach). Ties go to the lowest id.
    private static bool TryAcquire(World world, Entity unit, UnitType type, int radius, List<Entity> targets, HashSet<int> dead,
        out Entity best)
    {
        var pos = unit.GetComponent<TilePosition>();
        int player = unit.GetComponent<Owner>().Player;
        best = default;
        int bestScore = int.MaxValue;
        foreach (var candidate in targets)
        {
            if (dead.Contains(candidate.Id)) continue;
            bool building = candidate.HasComponent<Building>();
            if (type.Role == UnitRole.Siege && !building) continue;
            if (building && Buildings.TypeOf(world, candidate).Def.Wall) continue; // walls are fought only when in the way
            int distance = Buildings.DistanceTo(world, candidate, pos.X, pos.Y);
            if (distance > radius || !Combat.IsEnemy(world, player, candidate)) continue;
            int score = (building && type.Role != UnitRole.Siege ? 1000 : 0) + distance;
            if (score >= bestScore) continue;
            bestScore = score;
            best = candidate;
        }
        return bestScore != int.MaxValue;
    }

    // Closes in on the target and strikes it when in reach and ready.
    private static void Engage(World world, Entity unit, Entity target, List<Entity> targets, HashSet<int> dead)
    {
        ref var soldier = ref unit.GetComponent<Soldier>();
        var type = world.Content.Units[soldier.Kind];
        ref var mover = ref unit.GetComponent<Mover>();
        var pos = unit.GetComponent<TilePosition>();
        int distance = Buildings.DistanceTo(world, target, pos.X, pos.Y);
        if (distance <= type.Def.Range)
        {
            Movement.Stop(ref mover, pos);
            if (mover.Moving || world.Tick < soldier.ReadyTick) return; // finishing a step, or not ready yet
            soldier.ReadyTick = world.Tick + type.Def.AttackTicks;
            soldier.LastCombatTick = world.Tick;
            int damage = Combat.Damage(world, soldier.Kind, soldier.Service, target);
            Hit(world, unit.GetComponent<Owner>().Player, unit.Id, target, damage, dead);
            return;
        }

        var at = target.GetComponent<TilePosition>();
        int last = Buildings.SizeOf(world, target) - 1;
        int gx = Math.Clamp(pos.X, at.X, at.X + last), gy = Math.Clamp(pos.Y, at.Y, at.Y + last);
        bool replan = !mover.Moving || ((world.Tick + unit.Id) % ChaseReplanInterval == 0
                                        && Math.Max(Math.Abs(mover.GoalX - gx), Math.Abs(mover.GoalY - gy)) > 1);
        if (!replan || Movement.SetGoal(world, ref mover, pos, gx, gy) || mover.Moving) return;
        // Walls stand between the soldier and the target: march on it, breaking through on the way.
        unit.GetComponent<Order>() = new Order { Kind = OrderKind.AttackMove, TargetX = gx, TargetY = gy, Public = true };
        Breach(world, unit, type, targets, new HashSet<int>());
    }

    // A soldier stopped by walls attacks the nearest enemy wall or gate within reach of where they stand. Returns false
    // if there is none (the way is blocked by water or the like).
    private static bool Breach(World world, Entity unit, UnitType type, List<Entity> targets, HashSet<int> dead)
    {
        var pos = unit.GetComponent<TilePosition>();
        int player = unit.GetComponent<Owner>().Player;
        int radius = world.Content.Military.Combat.BreachRadius;
        Entity best = default;
        int bestDistance = int.MaxValue;
        foreach (var candidate in targets.Count > 0 ? targets : Targets(world))
        {
            if (dead.Contains(candidate.Id) || !candidate.HasComponent<Building>() || !Buildings.TypeOf(world, candidate).Def.Wall
                || !Buildings.IsComplete(candidate))
                continue;
            int distance = candidate.GetComponent<TilePosition>().DistanceTo(pos.X, pos.Y);
            if (distance > radius || distance >= bestDistance || !Combat.IsEnemy(world, player, candidate)) continue;
            best = candidate;
            bestDistance = distance;
        }
        if (best.IsNull) return false;
        unit.GetComponent<Order>().Target = best.Id;
        return true;
    }

    // One hit by `attackerId` of `player` on the target: people lose health (and soldiers morale), buildings take damage.
    private static void Hit(World world, int player, int attackerId, Entity target, int damage, HashSet<int> dead)
    {
        var at = target.GetComponent<TilePosition>();
        world.Emit(SimEventKind.Attacked, player, attackerId, at.X, at.Y, target.Id);
        if (target.HasComponent<Building>())
        {
            ref var building = ref target.GetComponent<Building>();
            building.Damage += damage;
            if (building.Damage < Buildings.TypeOf(world, target).Def.HitPoints) return;
            dead.Add(target.Id);
            Combat.Destroy(world, target, player);
            return;
        }
        if (target.HasComponent<Merchant>())
        {
            dead.Add(target.Id);
            Combat.Raid(world, target);
            return;
        }

        ref var citizen = ref target.GetComponent<Citizen>();
        citizen.Health -= damage;
        if (citizen.Health <= 0)
        {
            dead.Add(target.Id);
            Combat.Kill(world, target, player);
            return;
        }
        if (target.HasComponent<Soldier>())
        {
            target.GetComponent<Soldier>().LastCombatTick = world.Tick;
            Combat.LoseMorale(world, target, damage * world.Content.Military.Combat.MoraleLossPer100Damage / 100);
        }
        else if (target.GetComponent<Order>().Kind != OrderKind.ReturnToCamp)
        {
            // Civilians run for the camp (dropping their work).
            bool publicWork = target.GetComponent<Order>().Public;
            target.GetComponent<Order>() = new Order { Kind = OrderKind.ReturnToCamp, Public = publicWork };
        }
    }

    // A routed soldier runs to their own camp.
    private static void Flee(World world, Entity unit)
    {
        ref var mover = ref unit.GetComponent<Mover>();
        if (mover.Moving || !Bands.TryGetCamp(world, unit.GetComponent<Owner>().Player, out var camp)) return;
        var pos = unit.GetComponent<TilePosition>();
        var at = camp.GetComponent<TilePosition>();
        if (pos.DistanceTo(at.X, at.Y) > 1) Movement.SetGoal(world, ref mover, pos, at.X, at.Y);
    }

    // Towers shoot the nearest enemy person in range.
    private static void UpdateTowers(World world, List<Entity> targets, HashSet<int> dead)
    {
        foreach (var tower in World.InIdOrder(world.Store.Query<Building, Owner, TilePosition>()))
        {
            if (dead.Contains(tower.Id) || !Buildings.IsComplete(tower)) continue;
            var defence = Buildings.TypeOf(world, tower).Def.Defence;
            if (defence == null || world.Tick < tower.GetComponent<Building>().ReadyTick) continue;
            int player = tower.GetComponent<Owner>().Player;
            var pos = tower.GetComponent<TilePosition>();
            Entity best = default;
            int bestDistance = int.MaxValue;
            foreach (var candidate in targets)
            {
                if (dead.Contains(candidate.Id) || candidate.HasComponent<Building>()) continue;
                int distance = candidate.GetComponent<TilePosition>().DistanceTo(pos.X, pos.Y);
                if (distance > defence.Range || distance >= bestDistance || !Combat.IsEnemy(world, player, candidate)) continue;
                best = candidate;
                bestDistance = distance;
            }
            if (best.IsNull) continue;
            tower.GetComponent<Building>().ReadyTick = world.Tick + defence.AttackTicks;
            Hit(world, player, tower.Id, best, Combat.TowerDamage(world, defence, best), dead);
        }
    }

    private static void DecayLoot(World world)
    {
        foreach (var pile in World.InIdOrder(world.Store.Query<LootPile, Inventory>()))
        {
            if (world.Tick >= pile.GetComponent<LootPile>().DecayTick || pile.GetComponent<Inventory>().Amounts.All(a => a == 0))
                pile.DeleteEntity();
        }
    }
}
