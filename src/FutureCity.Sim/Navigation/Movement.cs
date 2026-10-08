using FutureCity.Sim.Components;

namespace FutureCity.Sim.Navigation;

/// <summary>Starting, stopping and advancing <see cref="Mover"/>s. Used by commands and systems.</summary>
public static class Movement
{
    /// <summary>
    /// Sends the mover toward (goalX, goalY). Returns false if it cannot get any closer than it is
    /// (already there, or the goal is unreachable from here).
    /// </summary>
    public static bool SetGoal(World world, ref Mover mover, in TilePosition position, int goalX, int goalY)
    {
        mover.GoalX = goalX;
        mover.GoalY = goalY;
        bool midStep = IsMidStep(mover, position);
        // A step in progress is always finished, so the new route starts from the tile being entered.
        int fromX = midStep ? mover.NextX : position.X, fromY = midStep ? mover.NextY : position.Y;
        var route = world.Pathfinder.FindRoute(fromX, fromY, goalX, goalY);
        mover.SetRoute(route);
        mover.Moving = midStep || route.Count > 0;
        if (!mover.Moving) mover.Progress = 0;
        return route.Count > 0 || (midStep && mover.NextX == goalX && mover.NextY == goalY);
    }

    /// <summary>Stops after the current step (if any).</summary>
    public static void Stop(ref Mover mover, in TilePosition position)
    {
        bool midStep = IsMidStep(mover, position);
        mover.GoalX = midStep ? mover.NextX : position.X;
        mover.GoalY = midStep ? mover.NextY : position.Y;
        mover.SetRoute([]);
        mover.Moving = midStep;
    }

    /// <summary>Advances the mover by one tick.</summary>
    internal static void Advance(World world, ref Mover mover, ref TilePosition position)
    {
        if (!mover.Moving) return;
        if (!IsMidStep(mover, position) && !BeginStep(world, ref mover, position))
        {
            mover.Moving = false;
            mover.Progress = 0;
            mover.StepCost = 0;
            return;
        }

        mover.Progress += Mover.ProgressPerTick;
        if (mover.Progress < mover.StepCost) return;

        position.X = mover.NextX;
        position.Y = mover.NextY;
        mover.Progress -= mover.StepCost;
        if (position.X == mover.GoalX && position.Y == mover.GoalY)
        {
            mover.Moving = false;
            mover.Progress = 0;
            mover.StepCost = 0;
        }
    }

    private static bool IsMidStep(in Mover mover, in TilePosition position) =>
        mover.NextX != position.X || mover.NextY != position.Y;

    private static bool BeginStep(World world, ref Mover mover, in TilePosition position)
    {
        if (position.X == mover.GoalX && position.Y == mover.GoalY) return false;
        var pathfinder = world.Pathfinder;
        if (mover.RouteIndex >= mover.RouteLength)
        {
            // The cached segment is used up (long route) or was empty: plan the next one.
            var route = pathfinder.FindRoute(position.X, position.Y, mover.GoalX, mover.GoalY);
            if (route.Count == 0) return false;
            mover.SetRoute(route);
        }

        int dir = mover.GetStep(mover.RouteIndex++);
        if (!pathfinder.CanStep(position.X, position.Y, dir)) return false;
        mover.NextX = position.X + Pathfinder.Dx[dir];
        mover.NextY = position.Y + Pathfinder.Dy[dir];
        int cost = mover.TicksPerTile * Mover.ProgressPerTick * pathfinder.MoveCost(mover.NextX, mover.NextY) / 100;
        mover.StepCost = (dir & 1) == 0 ? cost : cost * 141 / 100;
        return true;
    }
}
