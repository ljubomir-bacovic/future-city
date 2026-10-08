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
