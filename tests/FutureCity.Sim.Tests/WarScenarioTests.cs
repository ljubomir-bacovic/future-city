using System.Text;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

/// <summary>
/// Phase 4 exit criterion: two civilizations fight a war, and the loser's economy visibly suffers. Both are played by
/// stand-in bots that only issue commands: a settler that defends itself, and a neighbour that raids it.
/// </summary>
public class WarScenarioTests
{
    private const ulong Seed = 42;
    private static readonly long AttackAt = SimClock.FromSeconds(600);

    // Plays a two-civilization game; with `war`, player 2 raids player 1 at 10:00.
    private static Simulation Play(bool war, int ticks, Simulation? sim = null)
    {
        sim ??= GameSupport.NewGame(Seed, civilizations: 2);
        var defender = new SettlerBot(1);
        Action<Simulation> raider = war ? new WarBot(2, 1, AttackAt).Act : new SettlerBot(2).Act;
        for (int i = 0; i < ticks; i++)
        {
            defender.Act(sim);
            raider(sim);
            sim.Step();
        }
        return sim;
    }

    // The value of everything a civilization's families and treasury hold, at the goods' starting values.
    private static long Wealth(World world, int player)
    {
        var holdings = Economy.Holdings(world, player);
        return Enumerable.Range(0, holdings.Length).Sum(g => (long)holdings[g] * world.Content.Goods[g].Value)
               + Economy.MoneySupply(world, player);
    }

    [Fact]
    public void A_raided_civilization_is_poorer_after_the_war_than_in_peace()
    {
        // The raid starts at 10:00 and peace comes about four minutes later; compare a minute after that. (Given time,
        // a village recovers: by 20:00 the gap has mostly closed.)
        int ticks = (int)SimClock.FromSeconds(15 * 60);
        var peace = Play(war: false, ticks).World;
        var war = Play(war: true, ticks).World;

        var raided = war.Store.Query<Civilization, Owner>().Entities.First(e => e.GetComponent<Owner>().Player == 1)
            .GetComponent<Civilization>();
        var calm = peace.Store.Query<Civilization, Owner>().Entities.First(e => e.GetComponent<Owner>().Player == 1)
            .GetComponent<Civilization>();
        Assert.True(raided.Plundered > 0, "the raiders carried goods off");
        Assert.True(Wealth(war, 1) < Wealth(peace, 1) * 9 / 10, $"wealth {Wealth(war, 1)} at war vs {Wealth(peace, 1)} in peace");
        Assert.True(raided.Trades < calm.Trades, $"trades {raided.Trades} at war vs {calm.Trades} in peace");
        Assert.True(Bands.CensusOf(war, 1).Total <= Bands.CensusOf(peace, 1).Total);
        Assert.Equal(Relation.Peace, Relations.Between(war, 1, 2)); // the war ended in peace
    }

    [Fact]
    public void A_war_saves_loads_and_replays_identically()
    {
        int total = (int)(AttackAt + SimClock.FromSeconds(90)), split = (int)(AttackAt + SimClock.FromSeconds(45));
        var uninterrupted = Play(war: true, total);
        Assert.True(Relations.AtWar(uninterrupted.World, 1, 2));

        var first = Play(war: true, split);
        var resumed = SaveGame.Read(new MemoryStream(SaveGame.ToBytes(first)), TestSupport.Content);
        // Replaying the command log from the start gives the same state.
        var replay = Simulation.Replay(TestSupport.Content, uninterrupted.World.Setup, uninterrupted.CommandLog, uninterrupted.World.Tick);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(replay));

        // Continuing a loaded game with the same commands gives the same state.
        foreach (var entry in uninterrupted.CommandLog.Where(c => c.Tick > split)) resumed.Enqueue(entry.Command, entry.Tick);
        resumed.Step(total - split);
        Assert.Equal(SaveGame.StateHash(uninterrupted), SaveGame.StateHash(resumed));
    }

    [Fact]
    public void Every_military_component_survives_a_save_round_trip()
    {
        var sim = Play(war: true, (int)(AttackAt + SimClock.FromSeconds(120)));
        if (GameSupport.Count<LootPile>(sim) == 0) Combat.Spill(sim.World, 1, 1, Enumerable.Repeat(1, sim.World.Content.Goods.Count).ToArray());
        var bytes = SaveGame.ToBytes(sim);
        string text = Encoding.UTF8.GetString(bytes);
        foreach (var key in new[] { "soldier", "lootPile", "diplomacy" })
            Assert.Contains($"\"{key}\":", text);
        var loaded = SaveGame.Read(new MemoryStream(bytes), TestSupport.Content);
        Assert.Equal(text, Encoding.UTF8.GetString(SaveGame.ToBytes(loaded)));
    }
}
