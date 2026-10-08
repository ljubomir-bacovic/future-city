using FutureCity.Sim.Components;

namespace FutureCity.Sim.Navigation;

/// <summary>Starting, stopping and advancing <see cref="Mover"/>s. Used by commands and systems.</summary>
public static class Movement
{
    /// <summary>A marching unit leaves the group's flow field this close to where the group is heading.</summary>
    public const int FlowArrivalRadius = 4;

    /// <summary>
    /// Sends the mover toward (goalX, goalY). Returns false if it cannot get any closer than it is
    /// (already there, or the goal is unreachable from here).
    /// </summary>
    public static bool SetGoal(World world, ref Mover mover, in TilePosition position, int goalX, int goalY)
    {
        mover.GoalX = goalX;
        mover.GoalY = goalY;
        mover.UseFlow = false;
        bool midStep = IsMidStep(mover, position);
        // A step in progress is always finished, so the new route starts from the tile being entered.
        int fromX = midStep ? mover.NextX : position.X, fromY = midStep ? mover.NextY : position.Y;
        var route = world.Pathfinder.FindRoute(fromX, fromY, goalX, goalY, mover.Gates);
        mover.SetRoute(route);
        mover.Moving = midStep || route.Count > 0;
        if (!mover.Moving) mover.Progress = 0;
        return route.Count > 0 || (midStep && mover.NextX == goalX && mover.NextY == goalY);
    }

    /// <summary>
    /// Sends a group member toward its place (goalX, goalY) in a formation around (flowX, flowY): it follows the group's
    /// shared flow field until it is near, then walks to its own place.
    /// </summary>
    public static void March(World world, ref Mover mover, in TilePosition position, int goalX, int goalY, int flowX, int flowY)
    {
        if (!SetGoal(world, ref mover, position, goalX, goalY)) return; // already there, or cannot get any closer
        if (position.DistanceTo(flowX, flowY) <= FlowArrivalRadius) return; // close enough to walk straight to its place
        mover.UseFlow = true;
        mover.FlowX = flowX;
        mover.FlowY = flowY;
        mover.SetRoute([]);
    }

    /// <summary>Stops after the current step (if any).</summary>
    public static void Stop(ref Mover mover, in TilePosition position)
    {
        bool midStep = IsMidStep(mover, position);
        mover.GoalX = midStep ? mover.NextX : position.X;
        mover.GoalY = midStep ? mover.NextY : position.Y;
        mover.SetRoute([]);
        mover.UseFlow = false;
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
        int dir = -1;
        if (mover.UseFlow)
        {
            if (position.DistanceTo(mover.FlowX, mover.FlowY) > FlowArrivalRadius)
                dir = world.FlowField(mover.FlowX, mover.FlowY, mover.Gates).Direction(position.X, position.Y);
            if (dir < 0)
            {
                // Near the group's destination (or the field leads nowhere): walk on to its own place.
                mover.UseFlow = false;
                mover.SetRoute([]);
            }
        }
        if (dir < 0)
        {
            if (mover.RouteIndex >= mover.RouteLength)
            {
                // The cached segment is used up (long route) or was empty: plan the next one.
                var route = pathfinder.FindRoute(position.X, position.Y, mover.GoalX, mover.GoalY, mover.Gates);
                if (route.Count == 0) return false;
                mover.SetRoute(route);
            }
            dir = mover.GetStep(mover.RouteIndex++);
        }
        if (!pathfinder.CanStep(position.X, position.Y, dir, mover.Gates)) return false;
        mover.NextX = position.X + Pathfinder.Dx[dir];
        mover.NextY = position.Y + Pathfinder.Dy[dir];
        int cost = mover.TicksPerTile * Mover.ProgressPerTick * pathfinder.MoveCost(mover.NextX, mover.NextY) / 100;
        mover.StepCost = (dir & 1) == 0 ? cost : cost * 141 / 100;
        return true;
    }
}
