using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>
/// A player's civilization as a whole: era, knowledge, institutions and the activity counters the emergence engine
/// reads. One entity per player (with an <see cref="Owner"/>). Arrays are indexed by tech, institution, good or
/// <see cref="WorkKind"/>.
/// </summary>
[ComponentKey("civilization")]
public struct Civilization : IComponent
{
    /// <summary>Index of the current era.</summary>
    public int Era;
    /// <summary>Technology the player chose to research at shrines, or -1.</summary>
    public int ResearchFocus;
    /// <summary>1 for each known technology.</summary>
    public int[] Techs;
    /// <summary>Progress toward each technology.</summary>
    public int[] TechProgress;
    /// <summary>1 for each established institution.</summary>
    public int[] Institutions;
    /// <summary>Units gathered from nature so far, by good.</summary>
    public int[] Gathered;
    /// <summary>Units made by farms and workshops so far, by good.</summary>
    public int[] Produced;
    /// <summary>Ticks of work done so far, by work kind.</summary>
    public int[] Work;
    /// <summary><see cref="Work"/> at the last emergence evaluation; the difference is the recent activity.</summary>
    public int[] LastWork;
}
