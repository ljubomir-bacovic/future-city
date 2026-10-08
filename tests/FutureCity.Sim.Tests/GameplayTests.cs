using System.Text;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

/// <summary>
/// Whole games with the real rules: the Phase 1 exit criterion (good play grows the band, neglect brings famine)
/// plus determinism and save/load for every gameplay component.
/// </summary>
public class GameplayTests
{
    private const int FiveMinutes = 3000;

    private static (Simulation Sim, List<SimEvent> Events) Play(ulong seed, int ticks, bool forage, Simulation? from = null)
    {
        var sim = from ?? GameSupport.NewGame(seed);
        var bot = forage ? new ForagingBot(Players.Human) : null;
        var events = new List<SimEvent>();
        for (int i = 0; i < ticks; i++)
        {
            bot?.Act(sim);
            sim.Step();
            events.AddRange(sim.World.Events);
        }
        return (sim, events);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Good_foraging_grows_the_band_in_five_minutes(ulong seed)
    {
        var (sim, events) = Play(seed, FiveMinutes, forage: true);
        Assert.DoesNotContain(events, e => e.Kind == SimEventKind.DiedOfStarvation);
        Assert.True(Bands.CensusOf(sim.World, Players.Human).Total > TestSupport.Content.Citizens.Start.Citizens);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void A_neglected_band_suffers_famine_within_five_minutes(ulong seed)
    {
        var (sim, events) = Play(seed, FiveMinutes, forage: false);
        Assert.Contains(events, e => e.Kind == SimEventKind.DiedOfStarvation);
        Assert.True(Bands.CensusOf(sim.World, Players.Human).Total < TestSupport.Content.Citizens.Start.Citizens);
    }

    [Fact]
    public void Same_seed_and_play_give_identical_games()
    {
        Assert.Equal(SaveGame.StateHash(Play(9, 2000, true).Sim), SaveGame.StateHash(Play(9, 2000, true).Sim));
        Assert.NotEqual(SaveGame.StateHash(Play(9, 2000, true).Sim), SaveGame.StateHash(Play(10, 2000, true).Sim));
    }

    [Fact]
    public void Replaying_the_command_log_reproduces_a_played_game()
    {
        var (original, _) = Play(11, 2000, forage: true);
        var replay = Simulation.Replay(TestSupport.Content, original.World.Setup, original.CommandLog, original.World.Tick);
        Assert.Equal(SaveGame.StateHash(original), SaveGame.StateHash(replay));
    }

    [Fact]
    public void Save_load_and_continue_matches_an_uninterrupted_game()
    {
        var (uninterrupted, _) = Play(12, 2400, forage: true);

        var (first, _) = Play(12, 1000, forage: true);
        var resumed = SaveGame.Read(new MemoryStream(SaveGame.ToBytes(first)), TestSupport.Content);
        Play(12, 1400, forage: true, from: resumed);

        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(resumed));
    }

    [Fact]
    public void Every_gameplay_component_survives_a_save_round_trip()
    {
        // Play until a carcass exists, so every component type is in the save.
        var sim = GameSupport.NewGame(13);
        var bot = new ForagingBot(Players.Human);
        for (int i = 0; i < FiveMinutes && GameSupport.Count<Components.Carcass>(sim) == 0; i++)
        {
            bot.Act(sim);
            sim.Step();
        }
        var bytes = SaveGame.ToBytes(sim);
        string text = Encoding.UTF8.GetString(bytes);
        foreach (var key in new[] { "position", "mover", "owner", "citizen", "order", "camp", "plant", "animal", "carcass" })
            Assert.Contains($"\"{key}\":", text);

        var loaded = SaveGame.Read(new MemoryStream(bytes), TestSupport.Content);
        Assert.Equal(text, Encoding.UTF8.GetString(SaveGame.ToBytes(loaded)));
    }
}
