using FutureCity.Sim.Commands;
using FutureCity.Sim.Content;
using FutureCity.Sim.Map;
using FutureCity.Sim.Random;

namespace FutureCity.Sim;

/// <summary>
/// Runs a game: owns the <see cref="World"/>, queues commands and advances fixed ticks.
/// Same content + setup + command log always produces the same world.
/// </summary>
public sealed class Simulation
{
    private readonly List<ScheduledCommand> _pending;
    private readonly List<ScheduledCommand> _log;

    internal Simulation(World world, SimulationConfig config, IEnumerable<ScheduledCommand> pending,
        IEnumerable<ScheduledCommand> log, long nextCommandSequence)
    {
        World = world;
        Config = config;
        _pending = pending.ToList();
        _log = log.ToList();
        NextCommandSequence = nextCommandSequence;
    }

    /// <summary>The game state.</summary>
    public World World { get; }

    /// <summary>Commands and systems this simulation runs with.</summary>
    public SimulationConfig Config { get; }

    /// <summary>Commands waiting for their tick.</summary>
    public IReadOnlyList<ScheduledCommand> PendingCommands => _pending;

    /// <summary>Every command executed so far, in execution order. Setup + this log replays the game.</summary>
    public IReadOnlyList<ScheduledCommand> CommandLog => _log;

    internal long NextCommandSequence { get; private set; }

    /// <summary>Starts a new game.</summary>
    public static Simulation NewGame(ContentDatabase content, GameSetup setup, SimulationConfig? config = null)
    {
        content.MapSize(setup.MapSize); // fail fast on an unknown map size
        var rng = new Pcg32(setup.Seed);
        var generated = MapGenerator.Generate(setup, content, rng);
        var world = new World(content, setup, generated.Map, rng, tick: 0, nextEntityId: 1);
        WorldPopulator.Populate(world, generated.StartX, generated.StartY);
        return new Simulation(world, config ?? SimulationConfig.CreateDefault(), [], [], 0);
    }

    /// <summary>Rebuilds a game from its setup and command log, stopping after <paramref name="untilTick"/>.</summary>
    public static Simulation Replay(ContentDatabase content, GameSetup setup, IEnumerable<ScheduledCommand> log,
        long untilTick, SimulationConfig? config = null)
    {
        var sim = NewGame(content, setup, config);
        foreach (var entry in log)
        {
            if (entry.Tick <= untilTick)
                sim.Enqueue(entry.Command, entry.Tick);
        }
        sim.Step(checked((int)untilTick));
        return sim;
    }

    /// <summary>Queues a command for the next tick.</summary>
    public void Enqueue(Command command) => Enqueue(command, World.Tick + 1);

    /// <summary>Queues a command for a specific future tick (used for replays and, later, network input delay).</summary>
    public void Enqueue(Command command, long tick)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (tick <= World.Tick)
            throw new ArgumentOutOfRangeException(nameof(tick), $"Tick {tick} has already run (current tick {World.Tick}).");
        if (!Config.Commands.IsRegistered(command))
            throw new InvalidOperationException($"Command type {command.GetType().Name} is not registered.");
        _pending.Add(new ScheduledCommand(tick, NextCommandSequence++, command));
    }

    /// <summary>Advances the world by one tick: commands first, then every system in order.</summary>
    public void Step()
    {
        long tick = World.Tick + 1;
        World.EventList.Clear();

        var due = _pending.Where(c => c.Tick == tick).ToList();
        if (due.Count > 0)
        {
            _pending.RemoveAll(c => c.Tick == tick);
            due.Sort(static (a, b) =>
            {
                int byPlayer = a.Command.Player.CompareTo(b.Command.Player);
                return byPlayer != 0 ? byPlayer : a.Sequence.CompareTo(b.Sequence);
            });
            foreach (var scheduled in due)
            {
                scheduled.Command.Execute(World);
                _log.Add(scheduled);
            }
        }

        foreach (var system in Config.Systems)
            system.Update(World);

        World.Tick = tick;
    }

    /// <summary>Advances the world by <paramref name="ticks"/> ticks.</summary>
    public void Step(int ticks)
    {
        for (int i = 0; i < ticks; i++)
            Step();
    }
}
