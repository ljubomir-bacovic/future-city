using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Moving, gathering, hunting and returning to camp.</summary>
public class OrderTests
{
    private const int Me = Players.Human;

    [Fact]
    public void Move_walks_to_the_tile_at_the_expected_speed()
    {
        var sim = GameSupport.Plain();
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new MoveUnits([person.Id], 20, 10) { Player = Me });

        int ticks = GameSupport.RunUntil(sim, () => person.GetComponent<TilePosition>() == new TilePosition(20, 10), 100);

        int expected = 10 * sim.World.Content.Citizens.MoveTicksPerTile;
        Assert.InRange(ticks, expected, expected + 2);
        GameSupport.Run(sim, 2);
        Assert.Equal(OrderKind.Idle, person.GetComponent<Order>().Kind);
        Assert.False(person.GetComponent<Mover>().Moving);
    }

    [Fact]
    public void Moving_off_the_map_stops_at_the_edge()
    {
        var sim = GameSupport.Plain();
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new MoveUnits([person.Id], -500, 10) { Player = Me });
        GameSupport.RunUntil(sim, () => person.GetComponent<TilePosition>() == new TilePosition(0, 10), 100);
    }

    [Fact]
    public void Long_routes_beyond_the_cached_segment_still_arrive()
    {
        var sim = GameSupport.Plain();
        // A serpentine of walls with gaps at alternating ends makes the route far longer than one cached segment.
        for (int wall = 0; wall < 5; wall++)
        {
            int x = 8 + wall * 8;
            for (int y = 0; y < 64; y++)
            {
                bool gap = wall % 2 == 0 ? y >= 60 : y <= 3;
                if (!gap) GameSupport.SetTerrain(sim, "water", x, y);
            }
        }
        var person = GameSupport.Adult(sim, 2, 2);
        sim.Enqueue(new MoveUnits([person.Id], 50, 2) { Player = Me });

        GameSupport.RunUntil(sim, () => person.GetComponent<TilePosition>() == new TilePosition(50, 2), 5000);
    }

    [Fact]
    public void A_group_spreads_out_around_the_target()
    {
        var sim = GameSupport.Plain();
        var group = Enumerable.Range(0, 5).Select(_ => GameSupport.Adult(sim, 10, 10)).ToArray();
        sim.Enqueue(new MoveUnits(group.Select(g => g.Id).ToArray(), 20, 20) { Player = Me });

        GameSupport.Run(sim, 200);

        var spots = group.Select(g => g.GetComponent<TilePosition>()).ToList();
        Assert.Equal(spots.Count, spots.Distinct().Count());
        Assert.All(spots, s => Assert.True(s.DistanceTo(20, 20) <= 1));
    }

    [Fact]
    public void Gatherers_carry_food_to_camp_until_the_source_runs_out()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var bush = GameSupport.Plant(sim, 15, 10, food: 25);
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new Gather([person.Id], bush.Id) { Player = Me });

        GameSupport.RunUntil(sim, () => person.GetComponent<Order>().Kind == OrderKind.Idle, 1000);

        Assert.Equal(0, bush.GetComponent<Plant>().Food);
        Assert.True(GameSupport.Berries(camp) >= 25, "everything picked (including any regrowth) reaches camp");
        Assert.Equal(0, person.GetComponent<Citizen>().Carried);
    }

    [Fact]
    public void Gatherers_move_on_to_a_similar_source_nearby()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        var first = GameSupport.Plant(sim, 15, 10, food: 3);
        var second = GameSupport.Plant(sim, 17, 12, food: 50);
        var person = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new Gather([person.Id], first.Id) { Player = Me });

        GameSupport.RunUntil(sim, () => second.GetComponent<Plant>().Food < 50, 500);

        Assert.Equal(OrderKind.Gather, person.GetComponent<Order>().Kind);
        Assert.Equal(second.Id, person.GetComponent<Order>().Target);
    }

    [Fact]
    public void Hunters_kill_butcher_and_bring_home_the_meat()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var deer = GameSupport.Deer(sim, 18, 10);
        var hunters = new[] { GameSupport.Adult(sim, 10, 10), GameSupport.Adult(sim, 10, 10) };
        sim.Enqueue(new Hunt(hunters.Select(h => h.Id).ToArray(), deer.Id) { Player = Me });

        var events = new List<SimEvent>();
        GameSupport.RunUntil(sim, () => { events.AddRange(sim.World.Events); return GameSupport.Amount(camp, "meat") > 0; }, 1500);

        Assert.Contains(events, e => e.Kind == SimEventKind.AnimalKilled && e.Entity == deer.Id);
        Assert.False(deer.HasComponent<Animal>());
        Assert.All(hunters, h => Assert.Equal(OrderKind.Gather, h.GetComponent<Order>().Kind));
    }

    [Fact]
    public void Return_to_camp_drops_off_what_is_carried()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var person = GameSupport.Adult(sim, 25, 25);
        person.GetComponent<Citizen>() = person.GetComponent<Citizen>() with { CarriedGood = GameSupport.BerriesGood, Carried = 7 };
        sim.Enqueue(new ReturnToCamp([person.Id]) { Player = Me });

        GameSupport.RunUntil(sim, () => GameSupport.Berries(camp) == 7, 300);
        GameSupport.Run(sim, 1);
        Assert.Equal(OrderKind.Idle, person.GetComponent<Order>().Kind);
        Assert.True(person.GetComponent<TilePosition>().DistanceTo(10, 10) <= 1);
    }

    [Fact]
    public void Orders_for_other_players_units_and_missing_targets_are_ignored()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        var theirs = GameSupport.Adult(sim, 10, 10, player: 2);
        var mine = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new MoveUnits([theirs.Id, 9999], 20, 20) { Player = Me });
        sim.Enqueue(new Gather([mine.Id], 9999) { Player = Me });
        sim.Enqueue(new Hunt([mine.Id], theirs.Id) { Player = Me });

        GameSupport.Run(sim, 5);

        Assert.Equal(OrderKind.Idle, theirs.GetComponent<Order>().Kind);
        Assert.Equal(OrderKind.Idle, mine.GetComponent<Order>().Kind);
    }
}
