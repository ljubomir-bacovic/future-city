using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>
/// A player's public stores: the camp and completed storehouses. Until Private property they hold everything and
/// are shared: anyone eats from or takes out of any of them, and workers drop goods at the nearest one. Afterwards
/// they are the treasury's: tribute and public work fill them, public works and the hungry draw on them, and
/// families keep their own goods at home (see <see cref="Households"/>).
/// </summary>
public static class Stores
{
    /// <summary>Whether <paramref name="entity"/> is a store: a camp or a completed storage building.</summary>
    public static bool IsStore(World world, Entity entity) =>
        entity.HasComponent<Inventory>()
        && (entity.HasComponent<Camp>()
            || (entity.TryGetComponent<Building>(out var building) && !entity.HasComponent<Construction>()
                && world.Content.Buildings[building.Kind].Def.Storage));

    /// <summary>The player's stores in id order.</summary>
    public static List<Entity> Of(World world, int player)
    {
        var stores = new List<Entity>();
        foreach (var entity in World.InIdOrder(world.Store.Query<Inventory, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player == player && IsStore(world, entity)) stores.Add(entity);
        }
        return stores;
    }

    /// <summary>Units of each good in all of the player's stores, indexed by good.</summary>
    public static int[] Totals(World world, int player)
    {
        var totals = new int[world.Content.Goods.Count];
        foreach (var store in Of(world, player))
        {
            var amounts = store.GetComponent<Inventory>().Amounts;
            for (int g = 0; g < totals.Length; g++) totals[g] += amounts[g];
        }
        return totals;
    }

    /// <summary>Units of one good in all of the player's stores.</summary>
    public static int Total(World world, int player, int good) => Totals(world, player)[good];

    /// <summary>Meals (whole rations) the player's stores hold, counting every food by its nutrition.</summary>
    public static int Meals(World world, int player) => MealsIn(world, Totals(world, player));

    /// <summary>Meals held by a set of goods amounts.</summary>
    public static int MealsIn(World world, IReadOnlyList<int> amounts)
    {
        long points = 0;
        foreach (int good in world.Content.FoodGoods)
            points += (long)amounts[good] * world.Content.Nutrition(good);
        return (int)Math.Min(int.MaxValue, points / 100);
    }

    /// <summary>
    /// The nearest store of the player reachable from (x, y). With <paramref name="good"/> &gt;= 0, only stores
    /// holding some of that good count. Ties go to the lowest id.
    /// </summary>
    public static bool TryFindNearest(World world, int player, int x, int y, out Entity store, int good = -1)
    {
        store = default;
        int bestDistance = int.MaxValue;
        foreach (var candidate in Of(world, player))
        {
            if (good >= 0 && candidate.GetComponent<Inventory>().Amounts[good] <= 0) continue;
            var pos = candidate.GetComponent<TilePosition>();
            if (!world.CanReach(x, y, pos.X, pos.Y)) continue;
            int distance = Buildings.DistanceTo(world, candidate, x, y);
            if (distance >= bestDistance) continue;
            store = candidate;
            bestDistance = distance;
        }
        return bestDistance != int.MaxValue;
    }

    /// <summary>Takes up to <paramref name="amount"/> units of a good from the player's stores (in id order); returns how many were taken.</summary>
    public static int Take(World world, int player, int good, int amount)
    {
        int taken = 0;
        foreach (var store in Of(world, player))
        {
            if (taken >= amount) break;
            var amounts = store.GetComponent<Inventory>().Amounts;
            int take = Math.Min(amount - taken, amounts[good]);
            amounts[good] -= take;
            taken += take;
        }
        return taken;
    }

    /// <summary>
    /// Eats or spends food worth <paramref name="nutrition"/> points (100 = one meal) from the player's stores, best
    /// food first. Returns the points actually taken, which may be less if food runs out or a little more when a
    /// whole unit of a poor food covers the rest.
    /// </summary>
    public static int TakeFood(World world, int player, int nutrition)
    {
        int eaten = 0;
        foreach (int good in world.Content.FoodGoods)
        {
            if (eaten >= nutrition) break;
            int value = world.Content.Nutrition(good);
            int units = (nutrition - eaten + value - 1) / value;
            eaten += Take(world, player, good, units) * value;
        }
        return eaten;
    }

    /// <summary>Like <see cref="TakeFood"/>, but from one holder's inventory (a family home).</summary>
    public static int TakeFoodFrom(World world, Entity holder, int nutrition)
    {
        var amounts = holder.GetComponent<Inventory>().Amounts;
        int eaten = 0;
        foreach (int good in world.Content.FoodGoods)
        {
            if (eaten >= nutrition) break;
            int value = world.Content.Nutrition(good);
            int units = Math.Min(amounts[good], (nutrition - eaten + value - 1) / value);
            amounts[good] -= units;
            eaten += units * value;
        }
        return eaten;
    }

    /// <summary>Adds goods to an inventory.</summary>
    public static void Put(Entity holder, int good, int amount) => holder.GetComponent<Inventory>().Amounts[good] += amount;
}
