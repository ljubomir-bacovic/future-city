using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>A wild food plant such as a berry bush.</summary>
[ComponentKey("plant")]
public struct Plant : IComponent
{
    /// <summary>Index into <see cref="Content.ContentDatabase.Plants"/>.</summary>
    public int Kind;
    /// <summary>Food on the plant now.</summary>
    public int Food;
    /// <summary>When stripped bare, the tick at which it starts to regrow.</summary>
    public long DormantUntil;
}

/// <summary>A live game animal.</summary>
[ComponentKey("animal")]
public struct Animal : IComponent
{
    /// <summary>Index into <see cref="Content.ContentDatabase.Animals"/>.</summary>
    public int Kind;
    /// <summary>Center of the animal's home range.</summary>
    public int HomeX;
    /// <summary>Center of the animal's home range.</summary>
    public int HomeY;
}

/// <summary>A killed animal: meat that spoils over time.</summary>
[ComponentKey("carcass")]
public struct Carcass : IComponent
{
    /// <summary>Index into <see cref="Content.ContentDatabase.Animals"/>.</summary>
    public int Kind;
    /// <summary>Meat left.</summary>
    public int Food;
}
