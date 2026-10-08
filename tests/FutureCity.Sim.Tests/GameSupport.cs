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

    /// <summary>A camp holding <paramref name="food"/> berries.</summary>
    public static Entity Camp(Simulation sim, int x = 10, int y = 10, int food = 0, int player = Players.Human)
    {
        var camp = Spawn.Camp(sim.World, player, x, y);
        Stores.Put(camp, BerriesGood, food);
        return camp;
    }

    public static int BerriesGood => TestSupport.Content.GoodIndex("berries");

    public static int Good(string id) => TestSupport.Content.GoodIndex(id);

    /// <summary>Units of a good held by a store, site or workshop.</summary>
    public static int Amount(Entity holder, string good) => holder.GetComponent<Inventory>().Amounts[Good(good)];

    public static int Berries(Entity holder) => Amount(holder, "berries");

    public static void SetAmount(Entity holder, string good, int amount) => holder.GetComponent<Inventory>().Amounts[Good(good)] = amount;

    /// <summary>The human player's civilization record (tests on a plain map create it on demand).</summary>
    public static Entity Civ(Simulation sim) =>
        Emergence.Civics.TryGet(sim.World, Players.Human, out var civ) ? civ : Spawn.Civilization(sim.World, Players.Human);

    public static Entity Building(Simulation sim, string id, int x, int y, bool complete = true, int player = Players.Human) =>
        Spawn.Building(sim.World, player, sim.World.Content.BuildingIndex(id), x, y, complete);

    public static Entity Deposit(Simulation sim, string id, int x, int y) =>
        Spawn.Deposit(sim.World, sim.World.Content.DepositIndex(id), x, y);

    /// <summary>A 25-year-old, fed citizen.</summary>
    public static Entity Adult(Simulation sim, int x = 10, int y = 10, int player = Players.Human) =>
        Spawn.Citizen(sim.World, player, x, y, sim.World.Tick - 25L * sim.World.Content.Calendar.TicksPerYear);

    public static Entity Plant(Simulation sim, int x, int y, int food)
    {
        var plant = Spawn.Plant(sim.World, sim.World.Content.PlantIndex("berry_bush"), x, y);
        plant.GetComponent<Plant>().Food = food;
        return plant;
    }

    public static Entity Deer(Simulation sim, int x, int y) =>
        Spawn.Animal(sim.World, sim.World.Content.AnimalIndex("deer"), x, y, x, y);

    /// <summary>Sets the soil fertility of a square of tiles.</summary>
    public static void SetFertility(Simulation sim, int x, int y, int size, int fertility)
    {
        for (int ty = y; ty < y + size; ty++)
        {
            for (int tx = x; tx < x + size; tx++)
                sim.World.Map.SetFertility(tx, ty, fertility);
        }
    }

    /// <summary>Steps until the given season begins.</summary>
    public static void RunToSeason(Simulation sim, string season)
    {
        int perSeason = Calendar.TicksPerSeason(sim.World.Content);
        RunUntil(sim, () => Calendar.Season(sim.World).Id == season && sim.World.Tick % perSeason == 0,
            sim.World.Content.Calendar.TicksPerYear + 1);
    }

    /// <summary>Gives the player an established institution (default: chiefdom, which brings automatic jobs).</summary>
    public static void Establish(Simulation sim, string institution = "chiefdom") =>
        Civ(sim).GetComponent<Civilization>().Institutions[sim.World.Content.InstitutionIndex(institution)] = 1;

    /// <summary>Teaches the player a technology.</summary>
    public static void Learn(Simulation sim, string tech) =>
        Civ(sim).GetComponent<Civilization>().Techs[sim.World.Content.TechIndex(tech)] = 1;

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

    /// <summary>A completed hut that is a family home (Private property is not established by this).</summary>
    public static Entity Home(Simulation sim, int x, int y)
    {
        var hut = Building(sim, "hut", x, y);
        Households.Found(sim.World, hut);
        return hut;
    }

    /// <summary>Makes a citizen a member of a household.</summary>
    public static Entity LivesIn(this Entity unit, Entity home)
    {
        unit.GetComponent<Citizen>().Home = home.Id;
        return unit;
    }

    /// <summary>A completed marketplace.</summary>
    public static Entity Marketplace(Simulation sim, int x, int y) => Building(sim, "marketplace", x, y);

    /// <summary>Puts goods at the marketplace for a trader, as if carried there.</summary>
    public static void AtMarket(Entity market, Entity trader, string good, int amount) =>
        Markets.Deposit(market, trader, Good(good), amount);

    public static int AtMarket(Entity trader, string good) => trader.GetComponent<Trader>().AtMarket[Good(good)];

    public static ref Trader TraderOf(Entity entity) => ref entity.GetComponent<Trader>();

    /// <summary>Establishes Private property (and the chiefdom it builds on); with <paramref name="money"/>, Coinage too.</summary>
    public static void Economy(Simulation sim, bool money = false)
    {
        Establish(sim);
        Establish(sim, "property");
        if (money) Establish(sim, "coinage");
    }
}
