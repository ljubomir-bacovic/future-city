namespace FutureCity.Sim;

/// <summary>
/// Kinds of work citizens do. Each civilization counts the ticks spent on each kind, and technologies grow out of
/// related activity (content refers to these by their lower-case names).
/// </summary>
public enum WorkKind
{
    /// <summary>Gathering wild plants.</summary>
    Forage,
    /// <summary>Hunting and butchering.</summary>
    Hunt,
    /// <summary>Felling trees.</summary>
    Woodcut,
    /// <summary>Breaking stone.</summary>
    Quarry,
    /// <summary>Digging clay.</summary>
    Dig,
    /// <summary>Constructing buildings.</summary>
    Build,
    /// <summary>Sowing and harvesting fields.</summary>
    Farm,
    /// <summary>Workshop production.</summary>
    Craft,
    /// <summary>Thinking at a shrine.</summary>
    Research,
}
