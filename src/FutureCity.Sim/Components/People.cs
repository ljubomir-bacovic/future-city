using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>Which player an entity belongs to.</summary>
[ComponentKey("owner")]
public struct Owner : IComponent
{
    /// <summary>Player id (see <see cref="Players"/>).</summary>
    public int Player;
}

/// <summary>A person: age, health, hunger and what they carry.</summary>
[ComponentKey("citizen")]
public struct Citizen : IComponent
{
    /// <summary>Tick of birth; may be negative for people older than the game.</summary>
    public long BirthTick;
    /// <summary>Health; the citizen dies at zero.</summary>
    public int Health;
    /// <summary>Hunger; at the content's maximum the citizen is starving.</summary>
    public int Hunger;
    /// <summary>Food being carried back to camp.</summary>
    public int CarriedFood;
}

/// <summary>What a citizen was told to do.</summary>
public enum OrderKind
{
    /// <summary>Nothing to do.</summary>
    Idle,
    /// <summary>Walk to a tile, then idle.</summary>
    Move,
    /// <summary>Gather food from a plant or carcass, carrying it to camp until the source runs out.</summary>
    Gather,
    /// <summary>Chase and kill an animal, then butcher the carcass.</summary>
    Hunt,
    /// <summary>Walk to camp, drop off food, then idle.</summary>
    ReturnToCamp,
}

/// <summary>Progress within a work order.</summary>
public enum OrderStage
{
    /// <summary>Walking to the target.</summary>
    Travel,
    /// <summary>At the target, working.</summary>
    Work,
    /// <summary>Carrying food to camp.</summary>
    Deliver,
}

/// <summary>A citizen's current order.</summary>
[ComponentKey("order")]
public struct Order : IComponent
{
    /// <summary>The order.</summary>
    public OrderKind Kind;
    /// <summary>Progress within the order.</summary>
    public OrderStage Stage;
    /// <summary>Target entity id (plant, carcass or animal), or 0.</summary>
    public int Target;
    /// <summary>Kind of the target (plant or animal index), used to find a similar source when it runs out.</summary>
    public int TargetKind;
    /// <summary>Whether the target is a plant (otherwise an animal or carcass).</summary>
    public bool TargetIsPlant;
    /// <summary>Last known column of the target; workers look for a new source around here when it runs out.</summary>
    public int TargetX;
    /// <summary>Last known row of the target.</summary>
    public int TargetY;
    /// <summary>Ticks spent on the current piece of work.</summary>
    public int Timer;
}

/// <summary>A band's camp: the shared food store and shelter.</summary>
[ComponentKey("camp")]
public struct Camp : IComponent
{
    /// <summary>Food in the shared store.</summary>
    public int Food;
    /// <summary>People the camp can shelter.</summary>
    public int Shelter;
}
