using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>Why a building cannot be placed.</summary>
public enum Placement
{
    /// <summary>It can be placed.</summary>
    Ok,
    /// <summary>The player does not meet the building's requirements yet.</summary>
    NotAvailable,
    /// <summary>Part of the footprint is off the map.</summary>
    OffMap,
    /// <summary>Part of the footprint is on terrain that cannot be built on.</summary>
    BadTerrain,
    /// <summary>Something already stands there.</summary>
    Occupied,
    /// <summary>The player's people cannot walk there.</summary>
    Unreachable,
    /// <summary>The player already has (or is building) one, and only one is allowed (a marketplace).</summary>
    OnlyOne,
}

/// <summary>Facts and rules about buildings, shared by commands, systems, the AI and the UI.</summary>
public static class Buildings
{
    /// <summary>The type of a building entity.</summary>
    public static BuildingType TypeOf(World world, Entity building) =>
        world.Content.Buildings[building.GetComponent<Building>().Kind];

    /// <summary>Whether a building is finished (no construction site left).</summary>
    public static bool IsComplete(Entity building) => !building.HasComponent<Construction>();

    /// <summary>Footprint side length of an entity: the building size, or 1 for anything else.</summary>
    public static int SizeOf(World world, Entity entity) =>
        entity.TryGetComponent<Building>(out var b) ? world.Content.Buildings[b.Kind].Def.Size : 1;

    /// <summary>Chebyshev distance in tiles from (x, y) to the nearest tile of the entity's footprint.</summary>
    public static int DistanceTo(World world, Entity entity, int x, int y)
    {
        var pos = entity.GetComponent<TilePosition>();
        int last = SizeOf(world, entity) - 1;
        int dx = x < pos.X ? pos.X - x : x > pos.X + last ? x - pos.X - last : 0;
        int dy = y < pos.Y ? pos.Y - y : y > pos.Y + last ? y - pos.Y - last : 0;
        return Math.Max(dx, dy);
    }

    /// <summary>Checks whether <paramref name="player"/> may place a building of <paramref name="kind"/> with its top corner at (x, y).</summary>
    public static Placement CanPlace(World world, int player, int kind, int x, int y)
    {
        var type = world.Content.Buildings[kind];
        if (!type.Requires.IsMet(Civics.FactsOf(world, player))) return Placement.NotAvailable;
        if (type.Def.Market && HasAny(world, player, kind)) return Placement.OnlyOne;
        int size = type.Def.Size;
        var map = world.Map;
        for (int ty = y; ty < y + size; ty++)
        {
            for (int tx = x; tx < x + size; tx++)
            {
                if (!map.Contains(tx, ty)) return Placement.OffMap;
                if (!world.Content.Terrains[map.GetTerrain(tx, ty)].Buildable) return Placement.BadTerrain;
            }
        }
        foreach (var entity in world.Store.Query<TilePosition>().Entities)
        {
            if (!BlocksBuilding(entity)) continue;
            var pos = entity.GetComponent<TilePosition>();
            // A camp keeps a free ring around its fire; anything else only blocks its own footprint.
            int margin = entity.HasComponent<Camp>() ? 1 : 0;
            int other = SizeOf(world, entity);
            if (pos.X - margin <= x + size - 1 && x <= pos.X + other - 1 + margin
                && pos.Y - margin <= y + size - 1 && y <= pos.Y + other - 1 + margin)
                return Placement.Occupied;
        }
        if (!Bands.TryGetCamp(world, player, out var camp)) return Placement.Unreachable;
        var campPos = camp.GetComponent<TilePosition>();
        return world.CanReach(campPos.X, campPos.Y, x, y) ? Placement.Ok : Placement.Unreachable;
    }

    // People and animals move out of the way; everything else that stands on a tile blocks building there.
    private static bool BlocksBuilding(Entity entity) =>
        entity.HasComponent<Building>() || entity.HasComponent<Camp>() || entity.HasComponent<Plant>()
        || entity.HasComponent<Deposit>() || entity.HasComponent<Carcass>();

    /// <summary>The player's completed buildings of each kind, indexed by building kind.</summary>
    public static int[] CountCompleted(World world, int player)
    {
        var counts = new int[world.Content.Buildings.Count];
        foreach (var entity in world.Store.Query<Building, Owner>().Entities)
        {
            if (entity.GetComponent<Owner>().Player == player && IsComplete(entity))
                counts[entity.GetComponent<Building>().Kind]++;
        }
        return counts;
    }

    /// <summary>People the player's camp and completed buildings can shelter.</summary>
    public static int ShelterOf(World world, int player)
    {
        int shelter = 0;
        foreach (var camp in world.Store.Query<Camp, Owner>().Entities)
        {
            if (camp.GetComponent<Owner>().Player == player) shelter += camp.GetComponent<Camp>().Shelter;
        }
        var counts = CountCompleted(world, player);
        for (int kind = 0; kind < counts.Length; kind++)
            shelter += counts[kind] * world.Content.Buildings[kind].Def.Shelter;
        return shelter;
    }

    /// <summary>Whether the player has a building of this kind, finished or under construction.</summary>
    public static bool HasAny(World world, int player, int kind)
    {
        foreach (var entity in world.Store.Query<Building, Owner>().Entities)
        {
            if (entity.GetComponent<Building>().Kind == kind && entity.GetComponent<Owner>().Player == player) return true;
        }
        return false;
    }

    /// <summary>Citizens whose order is to work at or build <paramref name="building"/>.</summary>
    public static int WorkersAt(World world, int building)
    {
        int workers = 0;
        foreach (var unit in world.Store.Query<Order>().Entities)
        {
            var order = unit.GetComponent<Order>();
            if (order.Target == building && order.Kind is OrderKind.Work or OrderKind.Build) workers++;
        }
        return workers;
    }

    /// <summary>Construction materials still to be delivered, by good (all zero once everything is on site).</summary>
    public static int[] MissingMaterials(World world, Entity site)
    {
        var cost = TypeOf(world, site).Cost;
        var delivered = site.GetComponent<Inventory>().Amounts;
        var missing = new int[cost.Count];
        for (int g = 0; g < missing.Length; g++)
            missing[g] = Math.Max(0, cost[g] - delivered[g]);
        return missing;
    }

    /// <summary>Construction progress in percent (materials count for the first half, work for the second).</summary>
    public static int ConstructionPercent(World world, Entity site)
    {
        if (IsComplete(site)) return 100;
        var type = TypeOf(world, site);
        int needed = type.Cost.Sum(), missing = MissingMaterials(world, site).Sum();
        int materials = needed == 0 ? 50 : 50 * (needed - missing) / needed;
        int work = 50 * site.GetComponent<Construction>().Work / (type.Def.BuildWork * Labor.PerTick);
        return Math.Clamp(materials + work, 0, 99);
    }
}
