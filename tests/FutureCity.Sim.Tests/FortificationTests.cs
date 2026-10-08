using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Walls block movement, gates let their owner through, and attackers break through.</summary>
public class FortificationTests
{
    // A palisade across the whole map at x = 20, with player 1's gate at (20, 30).
    private static Simulation Walled()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 5, 5, food: 5000, player: 1);
        GameSupport.Camp(sim, 50, 50, food: 5000, player: 2);
        GameSupport.CivOf(sim, 1);
        GameSupport.CivOf(sim, 2);
        for (int y = 0; y < sim.World.Map.Height; y++)
            GameSupport.Building(sim, y == 30 ? "gate" : "palisade", 20, y, player: 1);
        return sim;
    }

    [Fact]
    public void A_wall_stops_everyone_but_the_owners_people_who_pass_through_their_gate()
    {
        var sim = Walled();
        var own = GameSupport.Adult(sim, 10, 30, player: 1);
        var stranger = GameSupport.Adult(sim, 10, 32, player: 2);
        sim.Enqueue(new MoveUnits([own.Id], 30, 30) { Player = 1 });
        sim.Enqueue(new MoveUnits([stranger.Id], 30, 32) { Player = 2 });
        GameSupport.Run(sim, 300);
        Assert.Equal(new TilePosition(30, 30), own.GetComponent<TilePosition>());
        Assert.True(stranger.GetComponent<TilePosition>().X < 20, "the wall kept them out");
        Assert.True(sim.World.CanReach(10, 32, 30, 32), "walls do not change what land is connected");
    }

    [Fact]
    public void Attackers_stopped_by_a_wall_break_through_it_and_march_on()
    {
        var sim = Walled();
        var ram = GameSupport.Soldier(sim, "ram", 10, 40, player: 2);
        GameSupport.War(sim);
        sim.Enqueue(new AttackMove([ram.Id], 30, 40) { Player = 2 });
        var events = GameSupport.Run(sim, 600);
        Assert.Contains(events, e => e.Kind == SimEventKind.BuildingDestroyed && e.Player == 1);
        Assert.True(ram.GetComponent<TilePosition>().X > 20, "through the breach");
    }

    [Fact]
    public void Walls_are_rebuilt_on_the_map_when_a_game_is_loaded()
    {
        var sim = Walled();
        var loaded = Persistence.SaveGame.Read(new MemoryStream(Persistence.SaveGame.ToBytes(sim)), TestSupport.Content);
        Assert.Equal(1, loaded.World.Map.GetWallOwner(20, 3));
        Assert.True(loaded.World.Map.IsGate(20, 30));
        Assert.False(loaded.World.Pathfinder.IsPassable(20, 3, 1));
        Assert.True(loaded.World.Pathfinder.IsPassable(20, 30, 1));
        Assert.False(loaded.World.Pathfinder.IsPassable(20, 30, 2));
    }
}
