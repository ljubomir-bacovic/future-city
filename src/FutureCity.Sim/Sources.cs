using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim;

/// <summary>
/// Things in nature that goods are taken from: plants, carcasses, deposits and terrain tiles (forest), and live
/// animals to hunt. Finding them only considers places a unit can walk to; ties go to the lowest id or scan order.
/// </summary>
public static class Sources
{
    /// <summary>Whether <paramref name="entity"/> is a plant, carcass or deposit with something left to take.</summary>
    public static bool HasGoods(Entity entity) =>
        (entity.TryGetComponent<Plant>(out var plant) && plant.Food > 0)
        || (entity.TryGetComponent<Carcass>(out var carcass) && carcass.Food > 0)
        || (entity.TryGetComponent<Deposit>(out var deposit) && deposit.Amount > 0);

    /// <summary>The target type of a source entity (plant, carcass or deposit), or <see cref="TargetType.None"/>.</summary>
    public static TargetType TypeOf(Entity entity) =>
        entity.HasComponent<Plant>() ? TargetType.Plant
        : entity.HasComponent<Carcass>() ? TargetType.Carcass
        : entity.HasComponent<Deposit>() ? TargetType.Deposit
        : TargetType.None;

    /// <summary>The kind index of a source entity (plant, animal or deposit kind).</summary>
    public static int KindOf(Entity entity) =>
        entity.TryGetComponent<Plant>(out var p) ? p.Kind
        : entity.TryGetComponent<Carcass>(out var c) ? c.Kind
        : entity.TryGetComponent<Deposit>(out var d) ? d.Kind
        : 0;

    /// <summary>What a source entity yields: good, work per unit and kind of work.</summary>
    public static SourceInfo InfoOf(ContentDatabase content, Entity entity)
    {
        if (entity.TryGetComponent<Plant>(out var plant))
            return new SourceInfo(content.PlantGood(plant.Kind), content.Plants[plant.Kind].TicksPerFood, WorkKind.Forage);
        if (entity.TryGetComponent<Carcass>(out var carcass))
            return new SourceInfo(content.AnimalGood(carcass.Kind), content.Animals[carcass.Kind].ButcherTicksPerFood, WorkKind.Hunt);
        return content.DepositSource(entity.GetComponent<Deposit>().Kind);
    }

    /// <summary>What the terrain tile at (x, y) yields, if it has anything left.</summary>
    public static SourceInfo? TileInfo(World world, int x, int y) =>
        world.Map.Contains(x, y) && world.Map.GetResource(x, y) > 0 ? world.Content.TerrainSource(world.Map.GetTerrain(x, y)) : null;

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

    /// <summary>Nearest deposit of <paramref name="kind"/> with something left, within <paramref name="maxDistance"/> tiles.</summary>
    public static bool TryFindDeposit(World world, int kind, int x, int y, int maxDistance, out Entity found) =>
        TryFindNearest(world, world.Store.Query<Deposit, TilePosition>(), x, y, maxDistance,
            e => e.GetComponent<Deposit>() is var d && d.Kind == kind && d.Amount > 0, out found);

    /// <summary>Nearest deposit yielding <paramref name="good"/>, within <paramref name="maxDistance"/> tiles.</summary>
    public static bool TryFindDepositOf(World world, int good, int x, int y, int maxDistance, out Entity found) =>
        TryFindNearest(world, world.Store.Query<Deposit, TilePosition>(), x, y, maxDistance,
            e => e.GetComponent<Deposit>() is var d && d.Amount > 0 && world.Content.DepositSource(d.Kind).Good == good, out found);

    /// <summary>
    /// Nearest reachable tile within <paramref name="maxDistance"/> whose terrain resource yields <paramref name="good"/>
    /// (pass a terrain index in <paramref name="terrain"/> to require that terrain, or -1 for any).
    /// Searches ring by ring in scan order, so ties are deterministic.
    /// </summary>
    public static bool TryFindTile(World world, int good, int terrain, int x, int y, int maxDistance, out int foundX, out int foundY)
    {
        var map = world.Map;
        int maxRing = Math.Min(maxDistance, Math.Max(map.Width, map.Height));
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int ty = y - ring; ty <= y + ring; ty++)
            {
                for (int tx = x - ring; tx <= x + ring; tx++)
                {
                    if (Math.Max(Math.Abs(tx - x), Math.Abs(ty - y)) != ring || !map.Contains(tx, ty)) continue;
                    if (terrain >= 0 && map.GetTerrain(tx, ty) != terrain) continue;
                    if (TileInfo(world, tx, ty) is not { } info || info.Good != good || !world.CanReach(x, y, tx, ty)) continue;
                    (foundX, foundY) = (tx, ty);
                    return true;
                }
            }
        }
        foundX = foundY = 0;
        return false;
    }

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
