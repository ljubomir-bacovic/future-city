using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

/// <summary>Same seed + same commands must always give an identical state hash.</summary>
public class DeterminismTests
{
    private const int Ticks = 600;

    [Fact]
    public void Same_seed_and_commands_give_identical_state()
    {
        var a = TestSupport.RunScript(seed: 42, Ticks);
        var b = TestSupport.RunScript(seed: 42, Ticks);
        Assert.True(a.World.Store.Count > 0, "the script should leave some entities alive");
        Assert.Equal(SaveGame.StateHash(a), SaveGame.StateHash(b));
    }

    [Fact]
    public void Different_seeds_give_different_states()
    {
        Assert.NotEqual(SaveGame.StateHash(TestSupport.RunScript(1, Ticks)), SaveGame.StateHash(TestSupport.RunScript(2, Ticks)));
    }

    [Fact]
    public void Different_commands_give_different_states()
    {
        var a = TestSupport.RunScript(42, Ticks);
        var b = TestSupport.NewSim(42);
        b.Enqueue(new SpawnWanderers(10, 32, 32) { Player = 1 }, 1);
        b.Step(Ticks);
        Assert.NotEqual(SaveGame.StateHash(a), SaveGame.StateHash(b));
    }

    [Fact]
    public void Replaying_the_command_log_reproduces_the_game()
    {
        var original = TestSupport.RunScript(42, Ticks);
        var replay = Simulation.Replay(TestSupport.Content, original.World.Setup, original.CommandLog, Ticks, TestSupport.Config());
        Assert.Equal(SaveGame.StateHash(original), SaveGame.StateHash(replay));
    }

    [Fact]
    public void Save_load_and_continue_matches_an_uninterrupted_run()
    {
        var uninterrupted = TestSupport.RunScript(42, Ticks);

        var first = TestSupport.RunScript(42, 0);
        first.Step(Ticks / 3);          // stop mid-game, with a command still pending
        var bytes = SaveGame.ToBytes(first);
        var resumed = SaveGame.Read(new MemoryStream(bytes), TestSupport.Content, TestSupport.Config());
        resumed.Step(Ticks - Ticks / 3);

        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(resumed));
    }
}
