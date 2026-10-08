using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Ai;

/// <summary>
/// A simple stand-in player for balancing runs and tests until the rival AI arrives (Phase 5).
/// It plays by the same rules as a human: it reads the world and issues ordinary commands.
/// Idle adults are sent to well-stocked berry bushes near camp (spread out so they do not crowd one bush),
/// and a share of the band is kept hunting.
/// </summary>
public sealed class ForagingBot
{
    /// <summary>Creates a bot for <paramref name="player"/>.</summary>
    /// <param name="player">The player it controls.</param>
    /// <param name="huntersPercent">Share of adults to keep hunting, in percent.</param>
    public ForagingBot(int player, int huntersPercent = 35)
    {
        Player = player;
        HuntersPercent = huntersPercent;
    }

    /// <summary>The player it controls.</summary>
    public int Player { get; }

    /// <summary>Share of adults to keep hunting, in percent.</summary>
    public int HuntersPercent { get; }

    /// <summary>How often the bot looks at its band.</summary>
    public int IntervalTicks { get; init; } = 10;

    // Extra tiles of walking the bot accepts to avoid sending one more gatherer to an already worked bush.
    private const int CrowdingPenalty = 4;

    /// <summary>Looks at the band and queues orders for idle adults. Call once before each step.</summary>
    public void Act(Simulation sim)
    {
        var world = sim.World;
        if (world.Tick % IntervalTicks != 0) return;
        var idle = new List<int>();
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player == Player && Bands.IsAdult(world, unit.GetComponent<Citizen>())
                && unit.GetComponent<Order>().Kind == OrderKind.Idle)
                idle.Add(unit.Id);
        }
        SendToFood(sim, idle);
    }

    /// <summary>Queues orders sending <paramref name="units"/> to gather berries or hunt, keeping the hunter share.</summary>
    public void SendToFood(Simulation sim, IReadOnlyList<int> units)
    {
        var world = sim.World;
        if (units.Count == 0 || !Bands.TryGetCamp(world, Player, out var camp)) return;
        var campPos = camp.GetComponent<TilePosition>();
        var workersAt = new Dictionary<int, int>();
        int adults = 0, hunters = 0;
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Order, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player != Player || !Bands.IsAdult(world, unit.GetComponent<Citizen>())) continue;
            adults++;
            var order = unit.GetComponent<Order>();
            if (units.Contains(unit.Id)) continue; // about to get a new order
            if (order.Kind == OrderKind.Hunt || (order.Kind == OrderKind.Gather && order.TargetType == TargetType.Carcass)) hunters++;
            if (order.Kind == OrderKind.Gather) workersAt[order.Target] = workersAt.GetValueOrDefault(order.Target) + 1;
        }

        foreach (int id in units)
        {
            bool wantHunter = hunters * 100 < adults * HuntersPercent;
            if (wantHunter && TryHunt(world, campPos, id, sim)) { hunters++; continue; }
            if (TryGather(world, campPos, id, workersAt, sim)) continue;
            if (TryHunt(world, campPos, id, sim)) hunters++;
        }
    }

    // The reachable bush with at least a full load that is best by distance from camp and crowding.
    private bool TryGather(World world, TilePosition camp, int unit, Dictionary<int, int> workersAt, Simulation sim)
    {
        int minFood = world.Content.Citizens.CarryCapacity;
        int bestId = 0, bestScore = int.MaxValue;
        foreach (var plant in World.InIdOrder(world.Store.Query<Plant, TilePosition>()))
        {
            var pos = plant.GetComponent<TilePosition>();
            if (plant.GetComponent<Plant>().Food < minFood || !world.CanReach(camp.X, camp.Y, pos.X, pos.Y)) continue;
            int score = pos.DistanceTo(camp.X, camp.Y) + CrowdingPenalty * workersAt.GetValueOrDefault(plant.Id);
            if (score >= bestScore) continue;
            bestScore = score;
            bestId = plant.Id;
        }
        if (bestId == 0) return false;
        workersAt[bestId] = workersAt.GetValueOrDefault(bestId) + 1;
        sim.Enqueue(new Gather([unit], bestId) { Player = Player });
        return true;
    }

    private bool TryHunt(World world, TilePosition from, int unit, Simulation sim)
    {
        if (Sources.TryFindCarcass(world, from.X, from.Y, int.MaxValue, out var carcass))
        {
            sim.Enqueue(new Gather([unit], carcass.Id) { Player = Player });
            return true;
        }
        for (int kind = 0; kind < world.Content.Animals.Count; kind++)
        {
            if (!Sources.TryFindAnimal(world, kind, from.X, from.Y, int.MaxValue, out var animal)) continue;
            sim.Enqueue(new Hunt([unit], animal.Id) { Player = Player });
            return true;
        }
        return false;
    }
}
