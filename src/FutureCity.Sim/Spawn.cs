using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim;

/// <summary>Creates game entities with their full set of components.</summary>
public static class Spawn
{
    /// <summary>Creates a band's camp with its shared food store.</summary>
    public static Entity Camp(World world, int player, int x, int y, int food)
    {
        var entity = world.CreateEntity();
        entity.AddComponent(new TilePosition(x, y));
        entity.AddComponent(new Owner { Player = player });
        entity.AddComponent(new Camp { Food = food, Shelter = world.Content.Citizens.Camp.Shelter });
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
        entity.AddComponent(new Citizen { BirthTick = birthTick, Health = rules.MaxHealth });
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
}
