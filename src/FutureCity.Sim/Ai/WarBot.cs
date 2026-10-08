using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Ai;

/// <summary>
/// A stand-in player that settles like <see cref="SettlerBot"/> and then raids a neighbour, for balancing war and testing
/// its economic consequences until the rival AI arrives (Phase 5). Shortly before <see cref="AttackAt"/> it calls up a
/// third of its adults (paid soldiers once it has coins, levies before) with the best weapons it can make; at
/// <see cref="AttackAt"/> it declares war and marches on the target's camp, where its soldiers loot stores and homes and
/// burn buildings. After <see cref="WarTicks"/> it offers peace and, once peace is made, sends its soldiers home. It
/// uses ordinary commands only.
/// </summary>
public sealed class WarBot
{
    private readonly SettlerBot _settler;

    /// <summary>Creates a bot for <paramref name="player"/> that attacks <paramref name="target"/> at tick <paramref name="attackAt"/>.</summary>
    public WarBot(int player, int target, long attackAt)
    {
        Player = player;
        Target = target;
        AttackAt = attackAt;
        _settler = new SettlerBot(player) { Defends = false }; // it runs its own army
    }

    /// <summary>The player it controls.</summary>
    public int Player { get; }

    /// <summary>The player it attacks.</summary>
    public int Target { get; }

    /// <summary>Tick at which it declares war.</summary>
    public long AttackAt { get; }

    /// <summary>How long it fights before offering peace.</summary>
    public long WarTicks { get; init; } = SimClock.FromSeconds(240);

    /// <summary>How long before the attack it calls up its soldiers.</summary>
    public long MusterTicks { get; init; } = SimClock.FromSeconds(40);

    /// <summary>Share of its adults it calls up, in percent.</summary>
    public int ArmyPercent { get; init; } = 30;

    private const int IntervalTicks = 20;

    // It goes to war only with at least this many adults and this many meals in store per person.
    private const int MinAdults = 8;
    private const int MealsPerHead = 10;

    // Tick at which it declared (or will declare) war; -1 before it musters.
    private long _warStart = -1;

    // Soldiers within this distance of the target's camp raid; farther away they march on it.
    private const int RaidRadius = 14;

    /// <summary>Looks at the world and queues commands. Call once before each step.</summary>
    public void Act(Simulation sim)
    {
        _settler.Act(sim);
        var world = sim.World;
        if (world.Tick % IntervalTicks != 0) return;
        long tick = world.Tick;
        // A prudent raider goes to war only with food in store and enough hands to spare; otherwise it waits.
        var facts = Civics.FactsOf(world, Player);
        bool ready = facts.Adults >= MinAdults && facts.Food >= facts.Population * MealsPerHead;
        if (_warStart < 0 && tick >= AttackAt - MusterTicks && ready && Military.Count(world, Player) == 0)
        {
            Muster(sim);
            _warStart = Math.Max(tick + MusterTicks, AttackAt);
        }
        if (_warStart >= 0 && tick >= _warStart && tick < _warStart + IntervalTicks) sim.Enqueue(new DeclareWar(Target) { Player = Player });
        bool atWar = Relations.AtWar(world, Player, Target);
        if (atWar && tick < _warStart + WarTicks) Raid(sim);
        else if (atWar && Relations.Pending(world, Target, Player).Kind != ProposalKind.Peace)
            sim.Enqueue(new Propose(Target, ProposalKind.Peace) { Player = Player });
        else if (!atWar && _warStart >= 0 && tick > _warStart && Military.Count(world, Player) > 0)
            sim.Enqueue(new Disband(Soldiers(world).Select(s => s.Id).ToArray()) { Player = Player });
    }

    private void Muster(Simulation sim)
    {
        var world = sim.World;
        var facts = Civics.FactsOf(world, Player);
        int count = Math.Max(3, facts.Adults * ArmyPercent / 100);
        var service = CanPay(world, count) ? Service.Paid : Service.Levy;
        // A few archers if it knows archery; spears for as many as the public stores have tools for, clubs for the rest.
        int archers = Military.CanRecruit(world, Player, world.Content.UnitIndex("archer"), Service.Levy, facts) == Recruitment.Ok
            ? count / 3 : 0;
        int spears = Military.CanRecruit(world, Player, world.Content.UnitIndex("spearman"), Service.Levy, facts) == Recruitment.Ok
            ? Math.Min(count - archers, Stores.Total(world, Player, world.Content.ToolGood)) : 0;
        if (Bands.TryGetCamp(world, Player, out var camp) && Bands.TryGetCamp(world, Target, out var enemy))
        {
            // Gather on the side of the camp facing the enemy.
            var a = camp.GetComponent<TilePosition>();
            var b = enemy.GetComponent<TilePosition>();
            sim.Enqueue(new SetRallyPoint(a.X + Math.Sign(b.X - a.X) * 4, a.Y + Math.Sign(b.Y - a.Y) * 4) { Player = Player });
        }
        if (spears > 0) sim.Enqueue(new Recruit("spearman", spears, service) { Player = Player });
        if (count - archers - spears > 0) sim.Enqueue(new Recruit("clubman", count - archers - spears, service) { Player = Player });
        if (archers > 0) sim.Enqueue(new Recruit("archer", archers, service) { Player = Player });
    }

    // Whether the treasury holds the wages of `count` paid soldiers for the whole war.
    private bool CanPay(World world, int count)
    {
        if (!Economy.HasMoney(world, Player) || !Civics.TryGet(world, Player, out var civ)) return false;
        long wage = (long)civ.GetComponent<Civilization>().PublicWage * world.Content.Military.Service.Paid.WagePercent / 100;
        return civ.GetComponent<Trader>().Coins >= wage * count * (WarTicks + MusterTicks) / 100;
    }

    // Soldiers far from the target march on its camp; near it, half of them loot (people first carry off goods),
    // the rest burn buildings. Soldiers already busy are left alone.
    private void Raid(Simulation sim)
    {
        var world = sim.World;
        if (!Bands.TryGetCamp(world, Target, out var enemyCamp)) return;
        var at = enemyCamp.GetComponent<TilePosition>();
        var marchers = new List<int>();
        foreach (var soldier in Soldiers(world))
        {
            var s = soldier.GetComponent<Soldier>();
            var order = soldier.GetComponent<Order>();
            if (!s.Equipped || Military.IsRouted(world, s) || order.Kind is OrderKind.Attack or OrderKind.Loot or OrderKind.Arm
                || (order.Kind is OrderKind.AttackMove or OrderKind.Move && soldier.GetComponent<Mover>().Moving))
                continue;
            var pos = soldier.GetComponent<TilePosition>();
            if (pos.DistanceTo(at.X, at.Y) > RaidRadius)
            {
                marchers.Add(soldier.Id);
                continue;
            }
            bool looter = soldier.Id % 2 == 0 && Military.TypeOf(world, soldier).Role != Content.UnitRole.Siege;
            if (looter && TryNearest(world, pos, e => Combat.CanLoot(world, Player, e) && Combat.RichestGood(world, e) >= 0, out var store))
                sim.Enqueue(new Loot([soldier.Id], store.Id) { Player = Player });
            else if (TryNearest(world, pos, e => e.HasComponent<Building>() && !Buildings.TypeOf(world, e).Def.Wall
                                                 && Combat.IsEnemy(world, Player, e), out var building))
                sim.Enqueue(new Attack([soldier.Id], building.Id) { Player = Player });
        }
        if (marchers.Count > 0) sim.Enqueue(new AttackMove(marchers.ToArray(), at.X, at.Y) { Player = Player });
    }

    private List<Entity> Soldiers(World world) =>
        World.InIdOrder(world.Store.Query<Soldier, Owner>()).Where(s => s.GetComponent<Owner>().Player == Player).ToList();

    private static bool TryNearest(World world, TilePosition from, Func<Entity, bool> match, out Entity best)
    {
        best = default;
        int bestDistance = int.MaxValue;
        foreach (var entity in World.InIdOrder(world.Store.Query<TilePosition, Inventory>()))
        {
            if (!match(entity)) continue;
            int distance = Buildings.DistanceTo(world, entity, from.X, from.Y);
            if (distance >= bestDistance) continue;
            best = entity;
            bestDistance = distance;
        }
        return bestDistance != int.MaxValue;
    }
}
