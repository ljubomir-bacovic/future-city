using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Helpers for gameplay tests that run the real game rules.</summary>
internal static class GameSupport
{
    public static Simulation NewGame(ulong seed = 42, string mapSize = "small") =>
        Simulation.NewGame(TestSupport.Content, TestSupport.Setup(seed, mapSize));

    /// <summary>An all-grass map with no entities, running the real systems: tests place exactly what they need.</summary>
    public static Simulation Plain(ulong seed = 1)
    {
        var sim = NewGame(seed);
        var world = sim.World;
        int grass = world.Content.TerrainIndex("grass");
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
                world.Map.SetTerrain(x, y, grass);
        }
        foreach (var entity in World.InIdOrder(world.Store.Query()))
            entity.DeleteEntity();
        return sim;
    }

    public static void SetTerrain(Simulation sim, string terrain, int x, int y) =>
        sim.World.Map.SetTerrain(x, y, sim.World.Content.TerrainIndex(terrain));

    public static Entity Camp(Simulation sim, int x = 10, int y = 10, int food = 0, int player = Players.Human) =>
        Spawn.Camp(sim.World, player, x, y, food);

    /// <summary>A 25-year-old, fed citizen.</summary>
    public static Entity Adult(Simulation sim, int x = 10, int y = 10, int player = Players.Human) =>
        Spawn.Citizen(sim.World, player, x, y, sim.World.Tick - 25L * sim.World.Content.Citizens.TicksPerYear);

    public static Entity Plant(Simulation sim, int x, int y, int food)
    {
        var plant = Spawn.Plant(sim.World, sim.World.Content.PlantIndex("berry_bush"), x, y);
        plant.GetComponent<Plant>().Food = food;
        return plant;
    }

    public static Entity Deer(Simulation sim, int x, int y) =>
        Spawn.Animal(sim.World, sim.World.Content.AnimalIndex("deer"), x, y, x, y);

    /// <summary>Steps and returns every event raised on the way.</summary>
    public static List<SimEvent> Run(Simulation sim, int ticks)
    {
        var events = new List<SimEvent>();
        for (int i = 0; i < ticks; i++)
        {
            sim.Step();
            events.AddRange(sim.World.Events);
        }
        return events;
    }

    /// <summary>Steps until <paramref name="done"/> holds; fails the test after <paramref name="maxTicks"/>.</summary>
    public static int RunUntil(Simulation sim, Func<bool> done, int maxTicks)
    {
        for (int i = 1; i <= maxTicks; i++)
        {
            sim.Step();
            if (done()) return i;
        }
        Assert.Fail($"Condition not met within {maxTicks} ticks.");
        return maxTicks;
    }

    public static int Count<T>(Simulation sim) where T : struct, IComponent => sim.World.Store.Query<T>().Count;
}
