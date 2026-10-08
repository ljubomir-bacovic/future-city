namespace FutureCity.Sim.Commands;

/// <summary>
/// The only way the player or the AI changes the world. A command is plain data (it is saved and
/// replayed) plus the rule that applies it. Commands may arrive stale, so <see cref="Execute"/>
/// must validate against the current world and do nothing when the command no longer makes sense.
/// </summary>
public abstract record Command
{
    /// <summary>Id of the player (human or AI) who issued the command.</summary>
    public int Player { get; init; }

    /// <summary>Applies the command at the start of its tick.</summary>
    public abstract void Execute(World world);
}

/// <summary>A command bound to the tick it executes on.</summary>
/// <param name="Tick">The tick during which the command executes.</param>
/// <param name="Sequence">Submission order; breaks ties between commands of one player in one tick.</param>
/// <param name="Command">The command.</param>
public readonly record struct ScheduledCommand(long Tick, long Sequence, Command Command);
