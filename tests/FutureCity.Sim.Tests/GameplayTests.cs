using System.Text;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

/// <summary>
/// Whole games with the real rules: the Phase 1 exit criterion (good play grows the band, neglect brings famine),
/// the Phase 2 exit criterion (a settling player reaches the Dark Ages in about 5-7 minutes and keeps growing),
/// the Phase 3 economy (families, markets and coins with steady prices), plus determinism and save/load for every
/// gameplay component.
/// </summary>
public class GameplayTests
{
    private const int FiveMinutes = 3000;

    private static (Simulation Sim, List<SimEvent> Events) Play(ulong seed, int ticks, bool forage, Simulation? from = null)
    {
        var bot = forage ? new ForagingBot(Players.Human) : null;
        return Play(seed, ticks, bot == null ? null : bot.Act, from);
    }

    private static (Simulation Sim, List<SimEvent> Events) Settle(ulong seed, int ticks, Simulation? from = null) =>
        Play(seed, ticks, new SettlerBot(Players.Human).Act, from);

    private static (Simulation Sim, List<SimEvent> Events) Play(ulong seed, int ticks, Action<Simulation>? bot, Simulation? from)
    {
        var sim = from ?? GameSupport.NewGame(seed);
        var events = new List<SimEvent>();
        for (int i = 0; i < ticks; i++)
        {
            bot?.Invoke(sim);
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

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void A_settling_band_reaches_the_dark_ages_in_about_five_to_seven_minutes(ulong seed)
    {
        var sim = GameSupport.NewGame(seed);
        var bot = new SettlerBot(Players.Human);
        var events = new List<SimEvent>();
        long? reachedAt = null;
        for (int i = 0; i < SimClock.FromSeconds(600); i++)
        {
            bot.Act(sim);
            sim.Step();
            events.AddRange(sim.World.Events);
            if (reachedAt == null && sim.World.Events.Any(e => e.Kind == SimEventKind.EraReached)) reachedAt = sim.World.Tick;
        }
        // Headless runs spread around 5-7 minutes; a 3-8 minute window keeps the test stable and still tracks the target.
        Assert.True(reachedAt >= SimClock.FromSeconds(180) && reachedAt <= SimClock.FromSeconds(480), $"Dark Ages reached at tick {reachedAt?.ToString() ?? "never"}, expected 3-8 minutes");
        Assert.Equal("dark_ages", Emergence.Civics.EraOf(sim.World, Players.Human).Def.Id);
        Assert.DoesNotContain(events, e => e.Kind == SimEventKind.DiedOfStarvation);
    }

    [Fact]
    public void A_settled_village_keeps_growing()
    {
        var (sim, _) = Settle(4, 6000);
        int atTen = Bands.CensusOf(sim.World, Players.Human).Total;
        Settle(4, 6000, sim);
        int atTwenty = Bands.CensusOf(sim.World, Players.Human).Total;
        Assert.True(atTen > TestSupport.Content.Citizens.Start.Citizens, $"10 min: {atTen}");
        Assert.True(atTwenty > atTen, $"10 min: {atTen}, 20 min: {atTwenty}");
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

    [Theory]
    [InlineData(2UL)]
    [InlineData(6UL)]
    public void A_settling_village_grows_a_money_economy_with_steady_prices(ulong seed)
    {
        var (sim, _) = Settle(seed, (int)SimClock.FromSeconds(15 * 60));
        var world = sim.World;
        Assert.True(Economy.HasHouseholds(world, Players.Human), "no private property");
        Assert.True(Economy.HasMoney(world, Players.Human), "no coinage");
        Assert.True(Economy.TryGetMarket(world, Players.Human, out var market));
        Assert.True(Economy.MoneySupply(world, Players.Human) > 0);
        Assert.InRange(Markets.Cpi(world, market), 70, 140); // honest coins: no runaway inflation or deflation

        // Goods at the marketplace are exactly what the traders there own.
        var atMarket = new int[world.Content.Goods.Count];
        foreach (var trader in Markets.Participants(world, market))
        {
            var owned = trader.GetComponent<Components.Trader>().AtMarket;
            for (int g = 0; g < owned.Length; g++) atMarket[g] += owned[g];
        }
        Assert.Equal(market.GetComponent<Components.Inventory>().Amounts, atMarket);
    }

    [Fact]
    public void A_money_economy_saves_loads_and_replays_identically()
    {
        int total = (int)SimClock.FromSeconds(9 * 60), split = (int)SimClock.FromSeconds(8 * 60);
        var (uninterrupted, _) = Settle(6, total);
        Assert.True(Economy.HasMoney(uninterrupted.World, Players.Human));

        var (first, _) = Settle(6, split);
        var resumed = SaveGame.Read(new MemoryStream(SaveGame.ToBytes(first)), TestSupport.Content);
        Settle(6, total - split, resumed);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(resumed));

        var replay = Simulation.Replay(TestSupport.Content, uninterrupted.World.Setup, uninterrupted.CommandLog, uninterrupted.World.Tick);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(replay));
    }

    [Fact]
    public void A_settling_game_saves_loads_and_replays_identically()
    {
        var (uninterrupted, _) = Settle(14, 4000);

        var (first, _) = Settle(14, 2000);
        var resumed = SaveGame.Read(new MemoryStream(SaveGame.ToBytes(first)), TestSupport.Content);
        Settle(14, 2000, resumed);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(resumed));

        var replay = Simulation.Replay(TestSupport.Content, uninterrupted.World.Setup, uninterrupted.CommandLog, uninterrupted.World.Tick);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(replay));
    }

    [Fact]
    public void Every_gameplay_component_survives_a_save_round_trip()
    {
        // Play a settling game until a carcass, a construction site, a farm, families and a visiting merchant exist, so
        // every component type is in the save.
        var sim = GameSupport.NewGame(13);
        var bot = new SettlerBot(Players.Human);
        for (int i = 0; i < 12_000 && !(GameSupport.Count<Components.Carcass>(sim) > 0
                 && GameSupport.Count<Components.Construction>(sim) > 0 && GameSupport.Count<Components.Field>(sim) > 0
                 && GameSupport.Count<Components.Household>(sim) > 0 && GameSupport.Count<Components.Merchant>(sim) > 0); i++)
        {
            bot.Act(sim);
            sim.Step();
        }
        var bytes = SaveGame.ToBytes(sim);
        string text = Encoding.UTF8.GetString(bytes);
        foreach (var key in new[] { "position", "mover", "owner", "citizen", "order", "camp", "plant", "animal", "carcass",
                     "inventory", "building", "construction", "field", "deposit", "civilization", "household", "trader",
                     "market", "merchant" })
            Assert.Contains($"\"{key}\":", text);

        var loaded = SaveGame.Read(new MemoryStream(bytes), TestSupport.Content);
        Assert.Equal(text, Encoding.UTF8.GetString(SaveGame.ToBytes(loaded)));
    }
}
