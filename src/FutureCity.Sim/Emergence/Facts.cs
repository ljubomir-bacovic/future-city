namespace FutureCity.Sim.Emergence;

/// <summary>
/// A snapshot of what is true about one civilization, the input of every <see cref="Condition"/>.
/// Facts describe the society (people, stores, buildings, knowledge), never the clock: nothing unlocks on a timer.
/// </summary>
public sealed class Facts
{
    /// <summary>Everyone in the civilization.</summary>
    public int Population { get; init; }
    /// <summary>People of working age.</summary>
    public int Adults { get; init; }
    /// <summary>Children.</summary>
    public int Children { get; init; }
    /// <summary>Meals in the stores.</summary>
    public int Food { get; init; }
    /// <summary>People the settlement can shelter.</summary>
    public int Shelter { get; init; }
    /// <summary>Index of the current era.</summary>
    public int Era { get; init; }
    /// <summary>Units in the stores, by good.</summary>
    public required int[] Store { get; init; }
    /// <summary>Units gathered from nature so far, by good.</summary>
    public required int[] Gathered { get; init; }
    /// <summary>Units produced by farms and workshops so far, by good.</summary>
    public required int[] Produced { get; init; }
    /// <summary>Completed buildings, by building kind.</summary>
    public required int[] Buildings { get; init; }
    /// <summary>1 for each known technology.</summary>
    public required int[] Techs { get; init; }
    /// <summary>1 for each established institution.</summary>
    public required int[] Institutions { get; init; }

    /// <summary>The value of one fact.</summary>
    public int Get(FactRef fact) => fact.Kind switch
    {
        FactKind.Population => Population,
        FactKind.Adults => Adults,
        FactKind.Children => Children,
        FactKind.Food => Food,
        FactKind.Shelter => Shelter,
        FactKind.Era => Era,
        FactKind.Store => Store[fact.Index],
        FactKind.Gathered => Gathered[fact.Index],
        FactKind.Produced => Produced[fact.Index],
        FactKind.Building => Buildings[fact.Index],
        FactKind.Tech => Techs[fact.Index],
        FactKind.Institution => Institutions[fact.Index],
        _ => 0,
    };
}
