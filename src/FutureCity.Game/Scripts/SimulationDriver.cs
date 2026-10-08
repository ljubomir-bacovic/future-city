using System;
using FutureCity.Sim;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Advances the simulation in fixed ticks from real time, scaled by game speed.
/// This is the only place the game calls <see cref="Simulation.Step()"/>; no game rules live here.
/// </summary>
public partial class SimulationDriver : Node
{
    /// <summary>Selectable game speeds (multiples of real time).</summary>
    public const int MinSpeed = 1, MaxSpeed = 4;

    // If a frame takes very long (window drag, breakpoint), drop the backlog instead of fast-forwarding.
    private const int MaxTicksPerFrame = 8 * MaxSpeed;
    private const double TickSeconds = 1.0 / SimClock.TicksPerSecond;

    private double _accumulator;

    /// <summary>The running game, or null before one is started.</summary>
    public Simulation? Simulation { get; private set; }

    /// <summary>Whether time is stopped. Commands can still be queued while paused.</summary>
    public bool Paused { get; private set; }

    /// <summary>Current game speed, <see cref="MinSpeed"/>–<see cref="MaxSpeed"/>.</summary>
    public int Speed { get; private set; } = MinSpeed;

    /// <summary>Fraction of the way to the next tick (0–1), for interpolating rendered positions.</summary>
    public double Alpha { get; private set; }

    /// <summary>
    /// The player this screen controls. With several civilizations on one map (hot-seat testing), <see cref="SwitchPlayer"/>
    /// hands control to the next one.
    /// </summary>
    public int Player { get; private set; } = Players.Human;

    /// <summary>Raised when control passes to another player.</summary>
    public event Action? PlayerChanged;

    /// <summary>Hands control to the next civilization on the map (back to the first after the last).</summary>
    public void SwitchPlayer()
    {
        if (Simulation == null || Simulation.World.Setup.Civilizations < 2) return;
        Player = Player % Simulation.World.Setup.Civilizations + 1;
        PlayerChanged?.Invoke();
    }

    /// <summary>Called before every tick, e.g. to let a computer player queue commands.</summary>
    public Action<Simulation>? BeforeStep { get; set; }

    /// <summary>Raised when a different simulation is started or loaded.</summary>
    public event Action? SimulationChanged;

    /// <summary>Raised after every tick.</summary>
    public event Action? Ticked;

    /// <summary>Raised when pause or speed changes.</summary>
    public event Action? TimeControlsChanged;

    /// <summary>Starts running <paramref name="simulation"/>, replacing any current one.</summary>
    public void Start(Simulation simulation)
    {
        Simulation = simulation;
        Player = Players.Human;
        _accumulator = 0;
        Alpha = 0;
        SimulationChanged?.Invoke();
    }

    /// <summary>Pauses or resumes time.</summary>
    public void SetPaused(bool paused)
    {
        if (Paused == paused) return;
        Paused = paused;
        TimeControlsChanged?.Invoke();
    }

    /// <summary>Sets game speed and resumes time.</summary>
    public void SetSpeed(int speed)
    {
        Speed = Math.Clamp(speed, MinSpeed, MaxSpeed);
        Paused = false;
        TimeControlsChanged?.Invoke();
    }

    public override void _Process(double delta)
    {
        if (Simulation == null || Paused) return;

        _accumulator += delta * Speed;
        int steps = 0;
        while (_accumulator >= TickSeconds && steps < MaxTicksPerFrame)
        {
            BeforeStep?.Invoke(Simulation);
            Simulation.Step();
            _accumulator -= TickSeconds;
            steps++;
            Ticked?.Invoke();
        }
        if (steps == MaxTicksPerFrame)
            _accumulator = Math.Min(_accumulator, TickSeconds);
        Alpha = _accumulator / TickSeconds;
    }
}
