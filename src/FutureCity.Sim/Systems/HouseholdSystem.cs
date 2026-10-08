using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Families and the yearly round. Once Private property exists, every completed hut becomes a household, people
/// without a home move into the emptiest hut with room, and at the start of each season families burn firewood (a
/// family that runs out is cold). At the turn of each year the treasury closes its accounts and guilds set how many
/// new members each craft admits.
/// </summary>
public sealed class HouseholdSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        bool newYear = world.Tick > 0 && world.Tick % world.Content.Calendar.TicksPerYear == 0;
        bool newSeason = world.Tick % Calendar.TicksPerSeason(world.Content) == 0;
        bool check = world.Tick % world.Content.Citizens.Jobs.CheckIntervalTicks == 0;
        if (!newYear && !newSeason && !check) return;

        foreach (var civEntity in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            int player = civEntity.GetComponent<Owner>().Player;
            if (newYear) CloseYear(world, player, ref civEntity.GetComponent<Civilization>());
            if (!Economy.HasHouseholds(world, player)) continue;
            FoundHouseholds(world, player);
            MoveIn(world, player);
            if (newSeason) BurnFirewood(world, player);
            ref var civ = ref civEntity.GetComponent<Civilization>();
            if (Economy.HasGuilds(world, player) && civ.GuildCap.All(c => c == 0)) SetGuildCaps(world, player, ref civ);
        }
    }

    private static void CloseYear(World world, int player, ref Civilization civ)
    {
        Array.Copy(civ.Ledger, civ.LastLedger, civ.Ledger.Length);
        Array.Clear(civ.Ledger);
        civ.DeathsLastYear = civ.DeathsThisYear;
        civ.DeathsThisYear = 0;
        if (Economy.HasGuilds(world, player)) SetGuildCaps(world, player, ref civ);
    }

    // Each guilded craft admits this year's crafters plus a few new members.
    private static void SetGuildCaps(World world, int player, ref Civilization civ)
    {
        var crafters = Crafters(world, player);
        for (int k = 0; k < civ.GuildCap.Length; k++)
        {
            civ.GuildCap[k] = world.Content.Buildings[k].IsWorkshop
                ? crafters[k] + world.Content.Economy.Guilds.NewMembersPerYear
                : 0;
        }
    }

    /// <summary>People working at each kind of workplace, by building kind.</summary>
    public static int[] Crafters(World world, int player)
    {
        var counts = new int[world.Content.Buildings.Count];
        foreach (var unit in world.Store.Query<Citizen, Order, Owner>().Entities)
        {
            var order = unit.GetComponent<Order>();
            if (unit.GetComponent<Owner>().Player != player || order.Kind != OrderKind.Work) continue;
            if (world.TryGetEntity(order.Target, out var building) && building.HasComponent<Building>())
                counts[building.GetComponent<Building>().Kind]++;
        }
        return counts;
    }

    private static void FoundHouseholds(World world, int player)
    {
        foreach (var building in World.InIdOrder(world.Store.Query<Building, Owner>()))
        {
            if (building.GetComponent<Owner>().Player != player || building.HasComponent<Household>()
                || !Buildings.IsComplete(building) || Buildings.TypeOf(world, building).Def.Shelter <= 0)
                continue;
            Households.Found(world, building);
        }
    }

    // People without a (standing) home move into the hut with the fewest people that still has room.
    private static void MoveIn(World world, int player)
    {
        var homes = Households.Of(world, player);
        if (homes.Count == 0) return;
        var members = Households.Members(world, player);
        var count = homes.ToDictionary(h => h.Id, h => members.GetValueOrDefault(h.Id)?.Count ?? 0);
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player != player) continue;
            ref var citizen = ref unit.GetComponent<Citizen>();
            if (citizen.Home != 0 && count.ContainsKey(citizen.Home)) continue;
            citizen.Home = 0;
            Entity best = default;
            foreach (var home in homes)
            {
                int room = Buildings.TypeOf(world, home).Def.Shelter - count[home.Id];
                if (room > 0 && (best.IsNull || count[home.Id] < count[best.Id])) best = home;
            }
            if (best.IsNull) continue; // the camp it is
            citizen.Home = best.Id;
            count[best.Id]++;
        }
    }

    private static void BurnFirewood(World world, int player)
    {
        int perMember = Calendar.Season(world).FirewoodPerMember;
        int wood = world.Content.FirewoodGood;
        var members = Households.Members(world, player);
        foreach (var home in Households.Of(world, player))
        {
            int people = members.GetValueOrDefault(home.Id)?.Count ?? 0;
            int need = perMember * people;
            var stock = home.GetComponent<Inventory>().Amounts;
            int burned = Math.Min(need, stock[wood]);
            stock[wood] -= burned;
            home.GetComponent<Household>().Cold = burned < need;
        }
    }
}
