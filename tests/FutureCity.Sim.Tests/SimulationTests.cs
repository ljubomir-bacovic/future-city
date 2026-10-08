using FutureCity.Sim.Commands;

namespace FutureCity.Sim.Tests;

[Collection(nameof(RecordOrder))]
public class SimulationTests
{
    [Fact]
    public void New_game_starts_at_tick_zero_with_the_chosen_map()
    {
        var sim = TestSupport.NewSim();
        Assert.Equal(0, sim.World.Tick);
        var size = TestSupport.Content.MapSize("small");
        Assert.Equal(size.Width, sim.World.Map.Width);
        Assert.Equal(size.Height, sim.World.Map.Height);
        Assert.Equal(sim.World.NextEntityId - 1, sim.World.Store.Count); // the populated wilds and the band
    }

    [Fact]
    public void Step_advances_the_tick()
    {
        var sim = TestSupport.NewSim();
        sim.Step();
        sim.Step(9);
        Assert.Equal(10, sim.World.Tick);
    }

    [Fact]
    public void Unknown_map_size_is_rejected()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            Simulation.NewGame(TestSupport.Content, TestSupport.Setup(mapSize: "galactic"), TestSupport.Config()));
    }

    [Fact]
    public void Commands_execute_on_their_tick_and_are_logged()
    {
        var sim = TestSupport.NewSimWithoutSystems();
        int initial = sim.World.Store.Count;
        sim.Enqueue(new SpawnWanderers(3, 5, 5));          // next tick (1)
        sim.Enqueue(new SpawnWanderers(2, 5, 5), 5);
        sim.Step();
        Assert.Equal(initial + 3, sim.World.Store.Count);
        sim.Step(3);
        Assert.Equal(initial + 3, sim.World.Store.Count);
        sim.Step();
        Assert.Equal(initial + 5, sim.World.Store.Count);

        Assert.Empty(sim.PendingCommands);
        Assert.Equal([1L, 5L], sim.CommandLog.Select(c => c.Tick));
    }

    [Fact]
    public void Commands_in_one_tick_run_by_player_then_submission_order()
    {
        RecordOrder.Executed.Clear();
        var sim = TestSupport.NewSim();
        sim.Enqueue(new RecordOrder("p2-first") { Player = 2 });
        sim.Enqueue(new RecordOrder("p1-first") { Player = 1 });
        sim.Enqueue(new RecordOrder("p2-second") { Player = 2 });
        sim.Enqueue(new RecordOrder("p1-second") { Player = 1 });
        sim.Step();
        Assert.Equal(["p1-first", "p1-second", "p2-first", "p2-second"], RecordOrder.Executed);
    }

    [Fact]
    public void Commands_for_past_ticks_are_rejected()
    {
        var sim = TestSupport.NewSim();
        sim.Step(3);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Enqueue(new SpawnWanderers(1, 0, 0), 3));
    }

    [Fact]
    public void Unregistered_commands_are_rejected()
    {
        var sim = TestSupport.NewSim();
        Assert.Throws<InvalidOperationException>(() => sim.Enqueue(new UnregisteredCommand()));
    }

    [Fact]
    public void Entity_ids_are_sequential_and_never_reused()
    {
        var sim = TestSupport.NewSim();
        int first = sim.World.NextEntityId;
        var a = sim.World.CreateEntity();
        var b = sim.World.CreateEntity();
        a.DeleteEntity();
        var c = sim.World.CreateEntity();
        Assert.Equal([first, first + 1, first + 2], new[] { a.Id, b.Id, c.Id });
    }

    private sealed record UnregisteredCommand : Command
    {
        public override void Execute(World world) { }
    }
}

[CollectionDefinition(nameof(RecordOrder), DisableParallelization = true)]
public class RecordOrderCollection;
