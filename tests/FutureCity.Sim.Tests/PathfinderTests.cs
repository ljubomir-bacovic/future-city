using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Tests;

public class PathfinderTests
{
    private static List<(int X, int Y)> Walk(int x, int y, IReadOnlyList<int> route)
    {
        var tiles = new List<(int, int)>();
        foreach (int dir in route)
        {
            x += Pathfinder.Dx[dir];
            y += Pathfinder.Dy[dir];
            tiles.Add((x, y));
        }
        return tiles;
    }

    [Fact]
    public void Open_ground_gives_the_direct_route()
    {
        var pf = GameSupport.Plain().World.Pathfinder;
        Assert.Equal(Enumerable.Repeat(0, 10), pf.FindRoute(2, 2, 12, 2));
        Assert.Equal(Enumerable.Repeat(1, 5), pf.FindRoute(2, 2, 7, 7));
        Assert.Empty(pf.FindRoute(4, 4, 4, 4));
    }

    [Fact]
    public void Route_goes_through_the_gap_in_a_wall()
    {
        var sim = GameSupport.Plain();
        for (int y = 0; y < 30; y++)
        {
            if (y != 20) GameSupport.SetTerrain(sim, "water", 10, y);
        }
        var tiles = Walk(5, 5, sim.World.Pathfinder.FindRoute(5, 5, 15, 5));
        Assert.Equal((15, 5), tiles[^1]);
        Assert.Contains((10, 20), tiles);
        Assert.All(tiles, t => Assert.True(sim.World.Pathfinder.IsWalkable(t.X, t.Y)));
    }

    [Fact]
    public void Unreachable_goal_leads_to_the_closest_reachable_tile()
    {
        var sim = GameSupport.Plain();
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0) GameSupport.SetTerrain(sim, "water", 20 + dx, 20 + dy);
            }
        }
        var tiles = Walk(5, 20, sim.World.Pathfinder.FindRoute(5, 20, 20, 20));
        Assert.Equal((18, 20), tiles[^1]);
        Assert.False(sim.World.CanReach(5, 20, 20, 20));
    }

    [Fact]
    public void Diagonal_steps_do_not_cut_blocked_corners()
    {
        var sim = GameSupport.Plain();
        GameSupport.SetTerrain(sim, "water", 6, 5);
        var route = sim.World.Pathfinder.FindRoute(5, 5, 6, 6);
        Assert.NotEqual([1], route);
        Assert.Equal((6, 6), Walk(5, 5, route)[^1]);
    }

    [Fact]
    public void Slow_forest_is_walked_around_when_the_detour_is_cheaper()
    {
        var sim = GameSupport.Plain();
        for (int x = 9; x <= 11; x++) GameSupport.SetTerrain(sim, "forest", x, 5);
        var tiles = Walk(5, 5, sim.World.Pathfinder.FindRoute(5, 5, 15, 5));
        Assert.Equal((15, 5), tiles[^1]);
        Assert.DoesNotContain(tiles, t => t.Y == 5 && t.X is >= 9 and <= 11);
    }

    [Fact]
    public void Routes_are_deterministic()
    {
        var a = GameSupport.NewGame(7).World.Pathfinder.FindRoute(3, 3, 60, 58).ToList();
        var b = GameSupport.NewGame(7).World.Pathfinder.FindRoute(3, 3, 60, 58).ToList();
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }
}
