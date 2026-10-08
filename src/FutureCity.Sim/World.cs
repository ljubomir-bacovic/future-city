using Friflo.Engine.ECS;
using FutureCity.Sim.Content;
using FutureCity.Sim.Map;
using FutureCity.Sim.Navigation;
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

    /// <summary>Notable things that happened during the last tick. Output only: cleared every tick and never saved.</summary>
    public IReadOnlyList<SimEvent> Events => EventList;

    internal List<SimEvent> EventList { get; } = [];

    /// <summary>Route planner over <see cref="Map"/>. Holds only scratch buffers, no game state.</summary>
    internal Pathfinder Pathfinder => _pathfinder ??= new Pathfinder(Map, Content);

    private Pathfinder? _pathfinder;

    /// <summary>Connected-area label of every tile (0 = blocked), derived from <see cref="Map"/>. Not game state.</summary>
    internal int[] RegionLabels
    {
        get
        {
            if (_regionLabels == null || _regionVersion != Map.Version)
            {
                _regionLabels = Regions.Label(Map, Pathfinder, out _);
                _regionVersion = Map.Version;
            }
            return _regionLabels;
        }
    }

    private int[]? _regionLabels;
    private int _regionVersion;

    /// <summary>Whether a unit standing on (x1, y1) could walk to (x2, y2).</summary>
    public bool CanReach(int x1, int y1, int x2, int y2)
    {
        if (!Map.Contains(x1, y1) || !Map.Contains(x2, y2)) return false;
        var labels = RegionLabels;
        int a = labels[y1 * Map.Width + x1];
        return a != 0 && a == labels[y2 * Map.Width + x2];
    }

    /// <summary>Returns the live entity with the given id, if it exists.</summary>
    public bool TryGetEntity(int id, out Entity entity)
    {
        if (id > 0 && Store.TryGetEntityById(id, out entity) && !entity.IsNull)
            return true;
        entity = default;
        return false;
    }

    internal void Emit(SimEventKind kind, int player, int entity, int x, int y) =>
        EventList.Add(new SimEvent(kind, player, entity, x, y));

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
