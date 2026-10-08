using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Placing buildings, construction and what finished buildings provide.</summary>
public class BuildingTests
{
    private const int Me = Players.Human;

    private static int Kind(string id) => TestSupport.Content.BuildingIndex(id);

    // What a hut costs, from content.
    private static int HutCost(string good) => TestSupport.Content.Buildings[Kind("hut")].Cost[GameSupport.Good(good)];

    [Fact]
    public void Placement_follows_the_rules()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        GameSupport.Building(sim, "storehouse", 20, 20);
        GameSupport.Plant(sim, 30, 30, food: 10);
        GameSupport.SetTerrain(sim, "water", 40, 10);
        var world = sim.World;

        Assert.Equal(Placement.Ok, Buildings.CanPlace(world, Me, Kind("hut"), 13, 10));
        Assert.Equal(Placement.Occupied, Buildings.CanPlace(world, Me, Kind("hut"), 11, 11)); // next to the fire
        Assert.Equal(Placement.Occupied, Buildings.CanPlace(world, Me, Kind("hut"), 21, 21)); // inside the storehouse
        Assert.Equal(Placement.Occupied, Buildings.CanPlace(world, Me, Kind("storehouse"), 29, 29)); // over a bush
        Assert.Equal(Placement.BadTerrain, Buildings.CanPlace(world, Me, Kind("hut"), 40, 10));
        Assert.Equal(Placement.OffMap, Buildings.CanPlace(world, Me, Kind("storehouse"), world.Map.Width - 1, 5));
        Assert.Equal(Placement.NotAvailable, Buildings.CanPlace(world, Me, Kind("farm"), 30, 10)); // needs farming
    }

    [Fact]
    public void Places_people_cannot_reach_are_refused()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        for (int i = 38; i <= 42; i++)
        {
            GameSupport.SetTerrain(sim, "water", i, 38);
            GameSupport.SetTerrain(sim, "water", i, 42);
            GameSupport.SetTerrain(sim, "water", 38, i);
            GameSupport.SetTerrain(sim, "water", 42, i);
        }
        Assert.Equal(Placement.Unreachable, Buildings.CanPlace(sim.World, Me, Kind("hut"), 40, 40));
    }

    [Fact]
    public void Placing_a_building_marks_out_a_site_only_where_allowed()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        sim.Enqueue(new PlaceBuilding("hut", 14, 10) { Player = Me });
        sim.Enqueue(new PlaceBuilding("hut", 14, 10) { Player = Me }); // same spot: now occupied
        sim.Enqueue(new PlaceBuilding("farm", 20, 20) { Player = Me }); // not available yet
        sim.Enqueue(new PlaceBuilding("castle", 25, 25) { Player = Me }); // unknown
        sim.Step();

        var site = Assert.Single(sim.World.Store.Query<Building>().Entities.ToList());
        Assert.False(Buildings.IsComplete(site));
        Assert.Equal(new TilePosition(14, 10), site.GetComponent<TilePosition>());
    }

    [Fact]
    public void Builders_fetch_materials_and_finish_the_building()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        GameSupport.SetAmount(camp, "wood", 20);
        GameSupport.SetAmount(camp, "clay", 10);
        sim.Enqueue(new PlaceBuilding("hut", 16, 12) { Player = Me });
        sim.Step();
        var hut = sim.World.Store.Query<Building>().Entities.First();
        var builders = new[] { GameSupport.Adult(sim, 10, 10), GameSupport.Adult(sim, 10, 10) };
        sim.Enqueue(new Build(builders.Select(b => b.Id).ToArray(), hut.Id) { Player = Me });

        var events = new List<SimEvent>();
        GameSupport.RunUntil(sim, () => { events.AddRange(sim.World.Events); return Buildings.IsComplete(hut); }, 1500);
        GameSupport.Run(sim, 2);

        Assert.Contains(events, e => e.Kind == SimEventKind.BuildingCompleted && e.Entity == hut.Id && e.Detail == Kind("hut"));
        Assert.Equal(20 - HutCost("wood"), GameSupport.Amount(camp, "wood"));
        Assert.Equal(10 - HutCost("clay"), GameSupport.Amount(camp, "clay"));
        Assert.All(hut.GetComponent<Inventory>().Amounts, a => Assert.Equal(0, a));
        Assert.All(builders, b => Assert.Equal(OrderKind.Idle, b.GetComponent<Order>().Kind));
        Assert.Equal(TestSupport.Content.Citizens.Camp.Shelter + TestSupport.Content.Buildings[Kind("hut")].Def.Shelter,
            Buildings.ShelterOf(sim.World, Me));
    }

    [Fact]
    public void Construction_waits_for_missing_materials()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        GameSupport.SetAmount(camp, "wood", 20); // but no clay
        var hut = GameSupport.Building(sim, "hut", 16, 12, complete: false);
        var builder = GameSupport.Adult(sim, 10, 10);
        sim.Enqueue(new Build([builder.Id], hut.Id) { Player = Me });

        GameSupport.Run(sim, 800);

        Assert.False(Buildings.IsComplete(hut));
        Assert.Equal(HutCost("wood"), GameSupport.Amount(hut, "wood"));
        Assert.InRange(Buildings.ConstructionPercent(sim.World, hut), 1, 49);
        Assert.Equal(OrderKind.Build, builder.GetComponent<Order>().Kind);

        GameSupport.SetAmount(camp, "clay", 10);
        GameSupport.RunUntil(sim, () => Buildings.IsComplete(hut), 1500);
    }

    [Fact]
    public void Cancelling_a_site_returns_the_materials()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10);
        var site = GameSupport.Building(sim, "hut", 16, 12, complete: false);
        GameSupport.SetAmount(site, "wood", 12);
        var finished = GameSupport.Building(sim, "hut", 20, 12);
        sim.Enqueue(new CancelBuilding(site.Id) { Player = Me });
        sim.Enqueue(new CancelBuilding(finished.Id) { Player = Me }); // finished buildings stay
        sim.Step();

        Assert.False(sim.World.TryGetEntity(site.Id, out _));
        Assert.True(sim.World.TryGetEntity(finished.Id, out _));
        Assert.Equal(12, GameSupport.Amount(camp, "wood"));
    }

    [Fact]
    public void Huts_make_room_for_children()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 100_000);
        camp.GetComponent<Camp>().Shelter = 2;
        GameSupport.Adult(sim);
        GameSupport.Adult(sim);
        var growth = TestSupport.Content.Citizens.Growth;

        Assert.DoesNotContain(GameSupport.Run(sim, growth.CheckIntervalTicks * 5), e => e.Kind == SimEventKind.Birth);

        GameSupport.Building(sim, "hut", 14, 10);
        Assert.Contains(GameSupport.Run(sim, growth.CheckIntervalTicks * 30), e => e.Kind == SimEventKind.Birth);
    }

    [Fact]
    public void Workplaces_take_only_as_many_workers_as_they_have_jobs()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 10, 10);
        var shrine = GameSupport.Building(sim, "shrine", 14, 10);
        var people = Enumerable.Range(0, 4).Select(_ => GameSupport.Adult(sim)).ToArray();
        sim.Enqueue(new AssignWork(people.Select(p => p.Id).ToArray(), shrine.Id) { Player = Me });
        sim.Step();

        Assert.Equal(TestSupport.Content.Buildings[Kind("shrine")].Def.Workers, Buildings.WorkersAt(sim.World, shrine.Id));
    }
}
