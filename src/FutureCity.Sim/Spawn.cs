using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>Creates game entities with their full set of components.</summary>
public static class Spawn
{
    /// <summary>Creates a player's civilization record (era, knowledge, activity).</summary>
    public static Entity Civilization(World world, int player)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new Owner { Player = player });
        entity.AddComponent(Civics.NewCivilization(world.Content));
        entity.AddComponent(Traders.NewTrader(world.Content)); // the treasury
        return entity;
    }

    /// <summary>Creates a band's camp: shelter and the first store.</summary>
    public static Entity Camp(World world, int player, int x, int y)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Owner { Player = player });
        entity.AddComponent(new Camp { Shelter = world.Content.Citizens.Camp.Shelter });
        entity.AddComponent(Inventory.Empty(world.Content.Goods.Count));
        return entity;
    }

    /// <summary>Creates a healthy, fed citizen born at <paramref name="birthTick"/>.</summary>
    public static Entity Citizen(World world, int player, int x, int y, long birthTick)
    {
        var rules = world.Content.Citizens;
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Mover(rules.MoveTicksPerTile, x, y));
        entity.AddComponent(new Owner { Player = player });
        entity.AddComponent(new Citizen
        {
            BirthTick = birthTick, Health = rules.MaxHealth, Happiness = world.Content.Economy.Happiness.Base,
        });
        entity.AddComponent(new Order());
        return entity;
    }

    /// <summary>Creates a fully grown wild plant.</summary>
    public static Entity Plant(World world, int kind, int x, int y)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Plant { Kind = kind, Food = world.Content.Plants[kind].MaxFood });
        return entity;
    }

    /// <summary>Creates a wild animal that roams around (homeX, homeY).</summary>
    public static Entity Animal(World world, int kind, int x, int y, int homeX, int homeY)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Mover(world.Content.Animals[kind].MoveTicksPerTile, x, y));
        entity.AddComponent(new Animal { Kind = kind, HomeX = homeX, HomeY = homeY });
        return entity;
    }

    /// <summary>Creates a full raw material deposit.</summary>
    public static Entity Deposit(World world, int kind, int x, int y)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Deposit { Kind = kind, Amount = world.Content.Deposits[kind].Amount });
        return entity;
    }

    /// <summary>
    /// Creates a building with its top corner at (x, y): a construction site, or a finished building when
    /// <paramref name="complete"/> is set (for tests and scenarios). Does not check placement rules.
    /// </summary>
    public static Entity Building(World world, int player, int kind, int x, int y, bool complete = false)
    {
        var type = world.Content.Buildings[kind];
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Owner { Player = player });
        entity.AddComponent(new Building { Kind = kind });
        entity.AddComponent(Inventory.Empty(world.Content.Goods.Count));
        if (!complete) entity.AddComponent(new Construction());
        if (type.Def.Market) entity.AddComponent(Markets.NewMarket(world.Content));
        if (type.Def.Field != null)
        {
            int fertility = AverageFertility(world, x, y, type.Def.Size);
            entity.AddComponent(new Field { Fertility = fertility, NaturalFertility = fertility });
        }
        return entity;
    }

    private static int AverageFertility(World world, int x, int y, int size)
    {
        int sum = 0, tiles = 0;
        for (int ty = y; ty < y + size; ty++)
        {
            for (int tx = x; tx < x + size; tx++)
            {
                if (!world.Map.Contains(tx, ty)) continue;
                sum += world.Map.GetFertility(tx, ty);
                tiles++;
            }
        }
        return tiles == 0 ? 0 : sum / tiles;
    }
}
