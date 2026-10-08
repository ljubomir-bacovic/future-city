using Friflo.Engine.ECS;
using FutureCity.Sim.Content;
using FutureCity.Sim.Map;
using FutureCity.Sim.Random;

namespace FutureCity.Sim;

/// <summary>
/// The complete game state: entities, map, random generator and tick counter.
/// Everything in here is saved and restored by <see cref="Persistence.SaveGame"/>.
/// </summary>
public sealed class World
{
    internal World(ContentDatabase content, GameSetup setup, TileMap map, Pcg32 rng, long tick, int nextEntityId)
    {
        Content = content;
        Setup = setup;
        Map = map;
        Rng = rng;
        Tick = tick;
        NextEntityId = nextEntityId;
        // UsePidAsId: entity ids are allocated by the world (never by Friflo) so they are deterministic and survive save/load.
        Store = new EntityStore(PidType.UsePidAsId);
    }

    /// <summary>Loaded game content (read-only).</summary>
    public ContentDatabase Content { get; }

    /// <summary>The setup this game was started with.</summary>
    public GameSetup Setup { get; }

    /// <summary>The terrain grid.</summary>
    public TileMap Map { get; }

    /// <summary>The simulation's only random generator.</summary>
    public Pcg32 Rng { get; }

    /// <summary>The ECS store. Create entities with <see cref="CreateEntity"/>, never with <c>Store.CreateEntity()</c>.</summary>
    public EntityStore Store { get; }

    /// <summary>Number of ticks completed so far.</summary>
    public long Tick { get; internal set; }

    /// <summary>Id the next created entity will get. Ids are never reused.</summary>
    public int NextEntityId { get; private set; }

    /// <summary>Creates a new entity with the next deterministic id.</summary>
    public Entity CreateEntity()
    {
        int id = NextEntityId;
        NextEntityId = checked(id + 1);
        return Store.CreateEntity(id);
    }

    /// <summary>
    /// Returns the entities matching <paramref name="query"/> sorted by id.
    /// ECS storage order is not stable across save/load, so any system whose result depends on
    /// iteration order (e.g. it uses the RNG or resolves conflicts) must iterate in id order.
    /// </summary>
    public static List<Entity> InIdOrder(ArchetypeQuery query)
    {
        var list = new List<Entity>(query.Count);
        foreach (var entity in query.Entities)
            list.Add(entity);
        list.Sort((a, b) => a.Id.CompareTo(b.Id));
        return list;
    }
}
