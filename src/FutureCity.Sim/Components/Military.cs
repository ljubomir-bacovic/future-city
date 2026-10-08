using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>How a soldier serves.</summary>
public enum Service
{
    /// <summary>Called up from the families; unpaid, fed from the public stores.</summary>
    Levy,
    /// <summary>Serves for a wage in coins (needs Coinage).</summary>
    Paid,
}

/// <summary>
/// A citizen under arms. The soldier keeps the <see cref="Citizen"/> component (age, hunger, health, family): recruiting
/// takes a worker from their family, and disbanding gives them back.
/// </summary>
[ComponentKey("soldier")]
public struct Soldier : IComponent
{
    /// <summary>Index into <see cref="Content.ContentDatabase.Units"/>.</summary>
    public int Kind;
    /// <summary>Levy or paid.</summary>
    public Service Service;
    /// <summary>Whether the soldier has collected their equipment; until then they cannot fight.</summary>
    public bool Equipped;
    /// <summary>Will to fight, 0-100; below the rout threshold the soldier flees.</summary>
    public int Morale;
    /// <summary>Tick of the soldier's next possible hit.</summary>
    public long ReadyTick;
    /// <summary>While the tick is before this, the soldier is routed and flees home.</summary>
    public long RoutUntil;
    /// <summary>Tick the soldier last hit or was hit (morale recovers only out of combat).</summary>
    public long LastCombatTick;
    /// <summary>Pay checks in a row the treasury could not pay (paid soldiers).</summary>
    public int UnpaidChecks;
    /// <summary>Where the soldier left their post or line of march to fight (column); they do not chase far from it.</summary>
    public int PostX;
    /// <summary>Where the soldier left their post or line of march to fight (row).</summary>
    public int PostY;
}

/// <summary>Goods spilled on the ground: the ruins of a destroyed building or the cargo of a raided caravan. Has an <see cref="Inventory"/>.</summary>
[ComponentKey("lootPile")]
public struct LootPile : IComponent
{
    /// <summary>Tick at which whatever is left has rotted, burned or been carried off by others.</summary>
    public long DecayTick;
}
