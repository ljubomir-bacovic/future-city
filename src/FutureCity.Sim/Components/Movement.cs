using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>The tile an entity stands on.</summary>
[ComponentKey("position")]
public record struct TilePosition : IComponent
{
    /// <summary>Tile column.</summary>
    public int X;
    /// <summary>Tile row.</summary>
    public int Y;

    /// <summary>Creates a position.</summary>
    public TilePosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Chebyshev distance in tiles (diagonal steps count as one).</summary>
    public readonly int DistanceTo(int x, int y) => Math.Max(Math.Abs(X - x), Math.Abs(Y - y));
}

/// <summary>
/// Walks an entity tile by tile toward <see cref="GoalX"/>, <see cref="GoalY"/>.
/// The route is a cached segment of up to <see cref="MaxCachedSteps"/> steps packed into four longs
/// (3 bits per step), so it saves like any other component; longer routes are re-planned when the segment runs out.
/// </summary>
[ComponentKey("mover")]
public struct Mover : IComponent
{
    /// <summary>Steps that fit into the cached route.</summary>
    public const int MaxCachedSteps = 4 * StepsPerWord;
    internal const int StepsPerWord = 21;

    /// <summary>Progress units gained per tick; a step costs ticks-per-tile × this (more on slow terrain and diagonals).</summary>
    public const int ProgressPerTick = 100;

    /// <summary>Ticks to walk one tile of open grassland.</summary>
    public int TicksPerTile;
    /// <summary>Whether the entity is walking.</summary>
    public bool Moving;
    /// <summary>Destination column.</summary>
    public int GoalX;
    /// <summary>Destination row.</summary>
    public int GoalY;
    /// <summary>Tile being walked into (equals the position between steps).</summary>
    public int NextX;
    /// <summary>Tile being walked into (equals the position between steps).</summary>
    public int NextY;
    /// <summary>Progress into the current step.</summary>
    public int Progress;
    /// <summary>Total progress the current step needs.</summary>
    public int StepCost;
    /// <summary>Packed route steps 0–20.</summary>
    public long Route0;
    /// <summary>Packed route steps 21–41.</summary>
    public long Route1;
    /// <summary>Packed route steps 42–62.</summary>
    public long Route2;
    /// <summary>Packed route steps 63–83.</summary>
    public long Route3;
    /// <summary>Number of steps in the cached route.</summary>
    public int RouteLength;
    /// <summary>Index of the next step to take.</summary>
    public int RouteIndex;

    /// <summary>Creates an idle mover with the given speed.</summary>
    public Mover(int ticksPerTile, int x, int y)
    {
        this = default;
        TicksPerTile = ticksPerTile;
        GoalX = NextX = x;
        GoalY = NextY = y;
    }

    /// <summary>Fraction of the current step done, in thousandths (for interpolated rendering).</summary>
    public readonly int StepPermille() => StepCost == 0 ? 0 : Math.Min(1000, Progress * 1000 / StepCost);

    internal readonly int GetStep(int index)
    {
        long word = (index / StepsPerWord) switch { 0 => Route0, 1 => Route1, 2 => Route2, _ => Route3 };
        return (int)((word >> (index % StepsPerWord * 3)) & 7);
    }

    internal void SetRoute(IReadOnlyList<int> steps)
    {
        Route0 = Route1 = Route2 = Route3 = 0;
        RouteLength = Math.Min(steps.Count, MaxCachedSteps);
        RouteIndex = 0;
        for (int i = 0; i < RouteLength; i++)
        {
            long bits = (long)steps[i] << (i % StepsPerWord * 3);
            switch (i / StepsPerWord)
            {
                case 0: Route0 |= bits; break;
                case 1: Route1 |= bits; break;
                case 2: Route2 |= bits; break;
                default: Route3 |= bits; break;
            }
        }
    }
}
