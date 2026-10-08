using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim;

/// <summary>Facts and rules about soldiers, shared by commands, systems, the AI and the UI.</summary>
public static class Military
{
    /// <summary>Whether the entity is a soldier.</summary>
    public static bool IsSoldier(Entity entity) => entity.HasComponent<Soldier>();

    /// <summary>The unit kind of a soldier.</summary>
    public static UnitType TypeOf(World world, Entity soldier) => world.Content.Units[soldier.GetComponent<Soldier>().Kind];

    /// <summary>The player's soldiers.</summary>
    public static int Count(World world, int player)
    {
        int count = 0;
        foreach (var entity in world.Store.Query<Soldier, Owner>().Entities)
        {
            if (entity.GetComponent<Owner>().Player == player) count++;
        }
        return count;
    }
}
