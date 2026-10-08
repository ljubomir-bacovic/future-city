using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Private property: family homes, goods carried home, tribute, eating at home and firewood.</summary>
public class OwnershipTests
{
    [Fact]
    public void People_move_into_huts_with_room_once_property_exists()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 1000);
        var a = GameSupport.Building(sim, "hut", 14, 10);
        var b = GameSupport.Building(sim, "hut", 14, 14);
        var people = Enumerable.Range(0, 10).Select(_ => GameSupport.Adult(sim)).ToArray();

        GameSupport.Run(sim, 40);
        Assert.All(people, p => Assert.Equal(0, p.GetComponent<Citizen>().Home)); // no property yet: everyone shares

        GameSupport.Economy(sim);
        GameSupport.Run(sim, 40);
        Assert.True(a.HasComponent<Household>() && b.HasComponent<Household>());
        var homes = people.Select(p => p.GetComponent<Citizen>().Home).ToArray();
        Assert.Equal(4, homes.Count(h => h == a.Id)); // a hut shelters 4
        Assert.Equal(4, homes.Count(h => h == b.Id));
        Assert.Equal(2, homes.Count(h => h == 0));    // the rest stay at the camp
    }

    [Fact]
    public void A_family_worker_carries_loads_home_and_every_so_often_one_to_the_treasury_as_tribute()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 1000);
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.Economy(sim);
        GameSupport.Civ(sim).GetComponent<Civilization>().TributePercent = 50;
        var rock = GameSupport.Deposit(sim, "stone_outcrop", 12, 14);
        var worker = GameSupport.Adult(sim, 14, 11).LivesIn(home);
        sim.Enqueue(new Gather([worker.Id], rock.Id) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        worker.GetComponent<Order>().Public = false; // working for the family, not on the chief's orders

        // 50% tribute: two loads go home, the third pays what is owed.
        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "stone") >= 10, 2000);
        Assert.Equal(20, GameSupport.Amount(home, "stone"));
        Assert.Equal(10, GameSupport.Amount(camp, "stone"));
        Assert.Equal(0, home.GetComponent<Household>().TributeOwed[GameSupport.Good("stone")]);
        Assert.Equal(10, GameSupport.Civ(sim).GetComponent<Civilization>().Ledger[(int)LedgerEntry.Tribute]);
    }

    [Fact]
    public void Without_property_a_hut_dweller_still_brings_everything_to_the_shared_store()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 1000);
        var hut = GameSupport.Building(sim, "hut", 14, 10);
        var rock = GameSupport.Deposit(sim, "stone_outcrop", 12, 14);
        var worker = GameSupport.Adult(sim, 14, 11);
        worker.GetComponent<Citizen>().Home = hut.Id;
        sim.Enqueue(new Gather([worker.Id], rock.Id) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        worker.GetComponent<Order>().Public = false;

        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "stone") >= 20, 2000);
        Assert.Equal(0, GameSupport.Amount(hut, "stone"));
    }

    [Fact]
    public void Families_eat_at_home_first_and_the_public_stores_feed_them_when_home_is_empty()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 100);
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.SetAmount(home, "berries", 15);
        GameSupport.Economy(sim);
        var member = GameSupport.Adult(sim, 14, 11).LivesIn(home);
        var rules = sim.World.Content.Citizens;
        member.GetComponent<Citizen>().Hunger = rules.EatAtHunger;

        GameSupport.Run(sim, 1); // one meal: 10 berries from home
        Assert.Equal(5, GameSupport.Berries(home));
        Assert.Equal(100, GameSupport.Berries(camp));

        member.GetComponent<Citizen>().Hunger = rules.EatAtHunger;
        GameSupport.Run(sim, 1); // 5 left at home, the rest from the chief's stores
        Assert.Equal(0, GameSupport.Berries(home));
        Assert.Equal(95, GameSupport.Berries(camp));
    }

    [Fact]
    public void Public_workers_are_fed_from_the_public_stores()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 100);
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.SetAmount(home, "berries", 50);
        GameSupport.Economy(sim);
        var builder = GameSupport.Adult(sim, 14, 11).LivesIn(home);
        var site = GameSupport.Building(sim, "storehouse", 20, 20, complete: false);
        sim.Enqueue(new Build([builder.Id], site.Id) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        builder.GetComponent<Citizen>().Hunger = sim.World.Content.Citizens.EatAtHunger;

        GameSupport.Run(sim, 1);
        Assert.Equal(90, GameSupport.Berries(camp)); // rations
        Assert.Equal(50, GameSupport.Berries(home));
    }

    [Fact]
    public void Families_burn_firewood_each_season_and_are_cold_without_it()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10, food: 10_000);
        var warm = GameSupport.Home(sim, 14, 10);
        var cold = GameSupport.Home(sim, 14, 14);
        GameSupport.SetAmount(warm, "wood", 50);
        GameSupport.Economy(sim);
        for (int i = 0; i < 2; i++)
        {
            GameSupport.Adult(sim).LivesIn(warm);
            GameSupport.Adult(sim).LivesIn(cold);
        }

        GameSupport.RunToSeason(sim, "winter");
        GameSupport.Run(sim, 1);
        int perMember = sim.World.Content.Calendar.Seasons.Single(s => s.Id == "winter").FirewoodPerMember;
        Assert.True(perMember > 0);
        Assert.False(warm.GetComponent<Household>().Cold);
        Assert.True(cold.GetComponent<Household>().Cold);
        Assert.True(GameSupport.Amount(warm, "wood") <= 50 - 2 * perMember);
    }
}
