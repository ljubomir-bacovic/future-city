using Friflo.Engine.ECS;
using FutureCity.Content;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Content;
using FutureCity.Sim.Systems;

namespace FutureCity.Sim.Tests;

/// <summary>Shared helpers: real content plus a small test-only rule set that exercises RNG, entity churn and commands.</summary>
internal static class TestSupport
{
    private static readonly Lazy<ContentDatabase> LazyContent = new(() => ContentLoader.Load(GameContent.ReadAll()));

    public static ContentDatabase Content => LazyContent.Value;

    public static GameSetup Setup(ulong seed = 42, string mapSize = "small") => new() { Seed = seed, MapSize = mapSize };

    /// <summary>Config with the test commands and systems. Systems are stateless, so a fresh config per sim is fine.</summary>
    public static SimulationConfig Config() => new(
        new CommandRegistry()
            .Register<SpawnWanderers>("test.spawnWanderers")
            .Register<FeedWanderer>("test.feedWanderer")
            .Register<RecordOrder>("test.recordOrder"),
        [new WanderSystem(), new AgeSystem()]);

    public static Simulation NewSim(ulong seed = 42) => Simulation.NewGame(Content, Setup(seed), Config());

    /// <summary>A sim with the test commands but no systems, so only commands change the world.</summary>
    public static Simulation NewSimWithoutSystems() =>
        Simulation.NewGame(Content, Setup(), new SimulationConfig(Config().Commands, []));

    /// <summary>Runs a fixed scripted game: spawns, feeds and lets wanderers roam.</summary>
    public static Simulation RunScript(ulong seed, int ticks)
    {
        var sim = NewSim(seed);
        sim.Enqueue(new SpawnWanderers(10, 32, 32) { Player = 1 }, 1);
        sim.Enqueue(new SpawnWanderers(5, 10, 50) { Player = 2 }, 50);
        sim.Enqueue(new FeedWanderer(3, 40) { Player = 1 }, 120);
        sim.Step(ticks);
        return sim;
    }
}

[ComponentKey("wanderer")]
public struct Wanderer : IComponent
{
    public int X;
    public int Y;
    public int Energy;
}

[ComponentKey("age")]
public struct Age : IComponent
{
    public int Ticks;
}

/// <summary>Creates wanderers at a tile.</summary>
public sealed record SpawnWanderers(int Count, int X, int Y) : Command
{
    public override void Execute(World world)
    {
        for (int i = 0; i < Count; i++)
        {
            var entity = world.CreateEntity();
            entity.AddComponent(new Wanderer { X = X, Y = Y, Energy = 400 + world.Rng.NextInt(800) });
            entity.AddComponent(new Age());
        }
    }
}

/// <summary>Gives energy to one wanderer; ignored if it no longer exists.</summary>
public sealed record FeedWanderer(int EntityId, int Amount) : Command
{
    public override void Execute(World world)
    {
        if (!world.Store.TryGetEntityById(EntityId, out var entity) || entity.IsNull || !entity.HasComponent<Wanderer>())
            return;
        entity.GetComponent<Wanderer>().Energy += Amount;
    }
}

/// <summary>Records execution order into a shared list (test-only observation; not part of world state).</summary>
public sealed record RecordOrder(string Label) : Command
{
    public static readonly List<string> Executed = [];

    public override void Execute(World world) => Executed.Add(Label);
}

/// <summary>Moves wanderers randomly, spends energy, removes exhausted ones and occasionally splits one in two.</summary>
public sealed class WanderSystem : ISimSystem
{
    public void Update(World world)
    {
        var map = world.Map;
        foreach (var entity in World.InIdOrder(world.Store.Query<Wanderer>()))
        {
            ref var w = ref entity.GetComponent<Wanderer>();
            w.X = Math.Clamp(w.X + world.Rng.NextInt(-1, 2), 0, map.Width - 1);
            w.Y = Math.Clamp(w.Y + world.Rng.NextInt(-1, 2), 0, map.Height - 1);
            w.Energy--;
            if (w.Energy <= 0)
            {
                entity.DeleteEntity();
                continue;
            }
            if (world.Rng.Chance(1, 200))
            {
                var child = world.CreateEntity();
                child.AddComponent(new Wanderer { X = w.X, Y = w.Y, Energy = w.Energy / 2 + 5 });
                child.AddComponent(new Age());
                w.Energy -= w.Energy / 2;
            }
        }
    }
}

/// <summary>Ages every entity with an Age component (order-independent).</summary>
public sealed class AgeSystem : ISimSystem
{
    public void Update(World world)
    {
        foreach (var (ages, _) in world.Store.Query<Age>().Chunks)
        {
            foreach (ref var age in ages.Span)
                age.Ticks++;
        }
    }
}
