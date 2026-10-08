using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>
/// Families and their homes. Once Private property exists every completed hut is a household that owns the goods in
/// it; people live in a hut with room or, failing that, at the camp, where the treasury feeds them.
/// </summary>
public static class Households
{
    /// <summary>The player's households in id order.</summary>
    public static List<Entity> Of(World world, int player)
    {
        var homes = new List<Entity>();
        foreach (var entity in World.InIdOrder(world.Store.Query<Household, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player == player) homes.Add(entity);
        }
        return homes;
    }

    /// <summary>The citizen's family home, if they have one and families own their goods.</summary>
    public static bool TryGetHome(World world, Entity unit, out Entity home)
    {
        home = default;
        int id = unit.GetComponent<Citizen>().Home;
        if (id != 0 && world.TryGetEntity(id, out var found) && found.HasComponent<Household>()
            && found.GetComponent<Owner>().Player == unit.GetComponent<Owner>().Player)
            home = found;
        return !home.IsNull;
    }

    /// <summary>
    /// The family home a worker works for, or none if the work is for the treasury: the order is public, the
    /// citizen lives at the camp, or families do not own goods yet.
    /// </summary>
    public static bool TryGetEmployer(World world, Entity unit, out Entity home)
    {
        home = default;
        return !unit.GetComponent<Order>().Public && TryGetHome(world, unit, out home);
    }

    /// <summary>The members of every household of the player, by home entity id, in id order.</summary>
    public static Dictionary<int, List<Entity>> Members(World world, int player)
    {
        var members = new Dictionary<int, List<Entity>>();
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player != player) continue;
            int home = unit.GetComponent<Citizen>().Home;
            if (home == 0) continue;
            if (!members.TryGetValue(home, out var list)) members[home] = list = [];
            list.Add(unit);
        }
        return members;
    }

    /// <summary>Makes a completed hut a family home.</summary>
    internal static void Found(World world, Entity hut)
    {
        int goods = world.Content.Goods.Count;
        if (!hut.HasComponent<Household>()) hut.AddComponent(new Household { TributeOwed = new int[goods] });
        if (!hut.HasComponent<Trader>()) hut.AddComponent(Traders.NewTrader(world.Content));
    }
}
