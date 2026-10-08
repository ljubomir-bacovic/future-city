using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>Which player an entity belongs to.</summary>
[ComponentKey("owner")]
public struct Owner : IComponent
{
    /// <summary>Player id (see <see cref="Players"/>).</summary>
    public int Player;
}

/// <summary>A person: age, health, hunger, what they carry and their tool.</summary>
[ComponentKey("citizen")]
public struct Citizen : IComponent
{
    /// <summary>Tick of birth; may be negative for people older than the game.</summary>
    public long BirthTick;
    /// <summary>Health; the citizen dies at zero.</summary>
    public int Health;
    /// <summary>Hunger; at the content's maximum the citizen is starving.</summary>
    public int Hunger;
    /// <summary>Good being carried (index into <see cref="Content.ContentDatabase.Goods"/>); meaningless when <see cref="Carried"/> is 0.</summary>
    public int CarriedGood;
    /// <summary>Units being carried.</summary>
    public int Carried;
    /// <summary>Ticks of work left in the citizen's tool; 0 means no tool.</summary>
    public int ToolWear;
}

/// <summary>What a citizen was told to do.</summary>
public enum OrderKind
{
    /// <summary>Nothing to do.</summary>
    Idle,
    /// <summary>Walk to a tile, then idle.</summary>
    Move,
    /// <summary>Take goods from a source (plant, carcass, deposit or forest tile) and carry them to a store until it runs out.</summary>
    Gather,
    /// <summary>Chase and kill an animal, then butcher the carcass.</summary>
    Hunt,
    /// <summary>Walk to camp, drop off what is carried, then idle.</summary>
    ReturnToCamp,
    /// <summary>Bring materials to a construction site and build it.</summary>
    Build,
    /// <summary>Work at a farm, workshop or shrine.</summary>
    Work,
}

/// <summary>Progress within an order.</summary>
public enum OrderStage
{
    /// <summary>Walking to the target.</summary>
    Travel,
    /// <summary>At the target, working.</summary>
    Work,
    /// <summary>Carrying goods to a store.</summary>
    Deliver,
    /// <summary>Walking to a store to pick up <see cref="Order.Good"/>.</summary>
    Fetch,
    /// <summary>Carrying goods from a store to the target building.</summary>
    Supply,
}

/// <summary>What an order's target is.</summary>
public enum TargetType
{
    /// <summary>No target.</summary>
    None,
    /// <summary>A wild plant.</summary>
    Plant,
    /// <summary>A carcass.</summary>
    Carcass,
    /// <summary>A live animal.</summary>
    Animal,
    /// <summary>A raw material deposit.</summary>
    Deposit,
    /// <summary>A terrain tile with a resource (a forest tile); the target is (<see cref="Order.TargetX"/>, <see cref="Order.TargetY"/>).</summary>
    Tile,
    /// <summary>A building.</summary>
    Building,
}

/// <summary>A citizen's current order.</summary>
[ComponentKey("order")]
public struct Order : IComponent
{
    /// <summary>The order.</summary>
    public OrderKind Kind;
    /// <summary>Progress within the order.</summary>
    public OrderStage Stage;
    /// <summary>Target entity id, or 0 (also 0 for tile targets).</summary>
    public int Target;
    /// <summary>What the target is.</summary>
    public TargetType TargetType;
    /// <summary>Kind of the target (plant, animal or deposit index; terrain index for tiles), used to find a similar source when it runs out.</summary>
    public int TargetKind;
    /// <summary>Last known column of the target; workers look for a new source around here when it runs out.</summary>
    public int TargetX;
    /// <summary>Last known row of the target.</summary>
    public int TargetY;
    /// <summary>Work progress on the current piece of work, in hundredths of a tick of plain work.</summary>
    public int Timer;
    /// <summary>Good being fetched for the target building, during <see cref="OrderStage.Fetch"/>.</summary>
    public int Good;
    /// <summary>Whether the job was assigned automatically by demand (and may be withdrawn when no longer needed), not by the player.</summary>
    public bool Auto;
}

/// <summary>A band's camp: its fire and lean-tos, and its first store (it also has an <see cref="Inventory"/>).</summary>
[ComponentKey("camp")]
public struct Camp : IComponent
{
    /// <summary>People the camp can shelter.</summary>
    public int Shelter;
}
