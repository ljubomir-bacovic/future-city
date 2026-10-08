using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>Whether a player can recruit a kind of soldier now.</summary>
public enum Recruitment
{
    /// <summary>They can.</summary>
    Ok,
    /// <summary>The unit's requirements are not met yet.</summary>
    NotAvailable,
    /// <summary>Paid service needs coins (Coinage).</summary>
    NeedsCoins,
    /// <summary>No adult is free to serve.</summary>
    NoOne,
}

/// <summary>Facts and rules about soldiers, shared by commands, systems, the AI and the UI.</summary>
public static class Military
{
    /// <summary>Whether the entity is a soldier.</summary>
    public static bool IsSoldier(Entity entity) => entity.HasComponent<Soldier>();

    /// <summary>The unit kind of a soldier.</summary>
    public static UnitType TypeOf(World world, Entity soldier) => world.Content.Units[soldier.GetComponent<Soldier>().Kind];

    /// <summary>The player's soldiers.</summary>
    public static int Count(World world, int player)
    {
        int count = 0;
        foreach (var entity in world.Store.Query<Soldier, Owner>().Entities)
        {
            if (entity.GetComponent<Owner>().Player == player) count++;
        }
        return count;
    }

    /// <summary>Full health of a person: the unit's for a soldier, the citizens' otherwise.</summary>
    public static int MaxHealth(World world, Entity unit) =>
        unit.TryGetComponent<Soldier>(out var soldier) ? world.Content.Units[soldier.Kind].Def.Health : world.Content.Citizens.MaxHealth;

    /// <summary>A soldier's starting (and fully recovered) morale.</summary>
    public static int BaseMorale(World world, int kind, Service service) =>
        Math.Clamp(world.Content.Units[kind].Def.Morale + ServiceOf(world, service).MoraleBonus, 1, 100);

    /// <summary>The rules for a kind of service.</summary>
    public static ServiceDef ServiceOf(World world, Service service) =>
        service == Service.Paid ? world.Content.Military.Service.Paid : world.Content.Military.Service.Levy;

    /// <summary>Whether a soldier is fleeing.</summary>
    public static bool IsRouted(World world, in Soldier soldier) => world.Tick < soldier.RoutUntil;

    /// <summary>Whether the player can recruit <paramref name="kind"/> in <paramref name="service"/> now.</summary>
    public static Recruitment CanRecruit(World world, int player, int kind, Service service, Facts? facts = null)
    {
        if (kind < 0 || kind >= world.Content.Units.Count) return Recruitment.NotAvailable;
        if (!world.Content.Units[kind].Requires.IsMet(facts ?? Civics.FactsOf(world, player))) return Recruitment.NotAvailable;
        if (service == Service.Paid && !Economy.HasMoney(world, player)) return Recruitment.NeedsCoins;
        return Candidates(world, player).Count == 0 ? Recruitment.NoOne : Recruitment.Ok;
    }

    /// <summary>
    /// Adults who could be called up, in the order they are taken: idle people first, then people on jobs the chief or
    /// their family chose, then people on the player's own orders; by id within each group.
    /// </summary>
    public static List<Entity> Candidates(World world, int player)
    {
        var candidates = World.InIdOrder(world.Store.Query<Citizen, Order, Owner>())
            .Where(u => u.GetComponent<Owner>().Player == player && !u.HasComponent<Soldier>()
                        && Bands.IsAdult(world, u.GetComponent<Citizen>()))
            .ToList();
        static int Rank(Entity u)
        {
            var order = u.GetComponent<Order>();
            return order.Kind is OrderKind.Idle or OrderKind.Move ? 0 : order.Auto || !order.Public ? 1 : 2;
        }
        return candidates.OrderBy(Rank).ThenBy(u => u.Id).ToList();
    }

    /// <summary>
    /// Equipment the player's soldiers are still waiting for, by good. The treasury keeps and buys it like building
    /// materials, and the chief sends gatherers for it.
    /// </summary>
    public static int[] EquipmentWanted(World world, int player)
    {
        var wanted = new int[world.Content.Goods.Count];
        foreach (var entity in world.Store.Query<Soldier, Owner>().Entities)
        {
            var soldier = entity.GetComponent<Soldier>();
            if (soldier.Equipped || entity.GetComponent<Owner>().Player != player) continue;
            var equipment = world.Content.Units[soldier.Kind].Equipment;
            for (int g = 0; g < wanted.Length; g++) wanted[g] += equipment[g];
        }
        return wanted;
    }

    /// <summary>Makes a citizen a soldier who first goes to collect their equipment.</summary>
    internal static void Enlist(World world, Entity unit, int kind, Service service)
    {
        var def = world.Content.Units[kind].Def;
        ref var citizen = ref unit.GetComponent<Citizen>();
        citizen.Health = (int)((long)citizen.Health * def.Health / world.Content.Citizens.MaxHealth);
        // Adding a component is a structural change: `citizen` must not be used after it.
        unit.AddComponent(new Soldier { Kind = kind, Service = service, Morale = BaseMorale(world, kind, service) });
        unit.GetComponent<Mover>().TicksPerTile = def.TicksPerTile;
        unit.GetComponent<Order>() = new Order { Kind = OrderKind.Arm, Public = true };
    }

    /// <summary>Sends a soldier back to civilian life; their equipment is worn out and lost.</summary>
    internal static void Discharge(World world, Entity unit)
    {
        int max = MaxHealth(world, unit);
        var citizen = unit.GetComponent<Citizen>();
        unit.RemoveComponent<Soldier>(); // a structural change: component refs taken before it are stale
        unit.GetComponent<Citizen>().Health = Math.Max(1, (int)((long)citizen.Health * world.Content.Citizens.MaxHealth / max));
        ref var mover = ref unit.GetComponent<Mover>();
        mover.TicksPerTile = world.Content.Citizens.MoveTicksPerTile;
        Navigation.Movement.Stop(ref mover, unit.GetComponent<TilePosition>());
        // Whatever they carry (loot) still goes to the public stores.
        unit.GetComponent<Order>() = citizen.Carried > 0 ? new Order { Kind = OrderKind.ReturnToCamp, Public = true } : default;
    }
}
