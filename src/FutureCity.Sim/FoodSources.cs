using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>Finding wild food a unit can walk to: plants with food on them, carcasses and live animals.</summary>
public static class FoodSources
{
    /// <summary>Whether <paramref name="entity"/> is a plant or carcass that still has food to gather.</summary>
    public static bool HasGatherableFood(Entity entity) =>
        (entity.TryGetComponent<Plant>(out var plant) && plant.Food > 0)
        || (entity.TryGetComponent<Carcass>(out var carcass) && carcass.Food > 0);

    /// <summary>Nearest reachable plant of <paramref name="kind"/> with at least <paramref name="minFood"/> food, within <paramref name="maxDistance"/> tiles.</summary>
    public static bool TryFindPlant(World world, int kind, int x, int y, int maxDistance, out Entity found, int minFood = 1) =>
        TryFindNearest(world, world.Store.Query<Plant, TilePosition>(), x, y, maxDistance,
            e => e.GetComponent<Plant>() is var p && p.Kind == kind && p.Food >= Math.Max(1, minFood), out found);

    /// <summary>Nearest carcass with meat left, within <paramref name="maxDistance"/> tiles.</summary>
    public static bool TryFindCarcass(World world, int x, int y, int maxDistance, out Entity found) =>
        TryFindNearest(world, world.Store.Query<Carcass, TilePosition>(), x, y, maxDistance,
            e => e.GetComponent<Carcass>().Food > 0, out found);

    /// <summary>Nearest live animal of <paramref name="kind"/>, within <paramref name="maxDistance"/> tiles.</summary>
    public static bool TryFindAnimal(World world, int kind, int x, int y, int maxDistance, out Entity found) =>
        TryFindNearest(world, world.Store.Query<Animal, TilePosition>(), x, y, maxDistance,
            e => e.GetComponent<Animal>().Kind == kind, out found);

    // Ties go to the lowest id, so the choice never depends on storage order.
    private static bool TryFindNearest(World world, ArchetypeQuery query, int x, int y, int maxDistance,
        Func<Entity, bool> match, out Entity found)
    {
        found = default;
        int bestDistance = int.MaxValue, bestId = int.MaxValue;
        foreach (var entity in query.Entities)
        {
            var pos = entity.GetComponent<TilePosition>();
            int distance = pos.DistanceTo(x, y);
            if (distance > maxDistance || distance > bestDistance || (distance == bestDistance && entity.Id > bestId)) continue;
            if (!match(entity) || !world.CanReach(x, y, pos.X, pos.Y)) continue;
            found = entity;
            bestDistance = distance;
            bestId = entity.Id;
        }
        return bestId != int.MaxValue;
    }
}
