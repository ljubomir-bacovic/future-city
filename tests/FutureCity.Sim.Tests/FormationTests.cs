using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Tests;

/// <summary>Flow fields, formations and group pace.</summary>
public class FormationTests
{
    [Fact]
    public void A_flow_field_leads_every_reachable_tile_to_the_goal_around_obstacles_and_is_deterministic()
    {
        var sim = GameSupport.Plain();
        for (int row = 5; row < 40; row++) GameSupport.SetTerrain(sim, "water", 20, row); // a lake to walk around
        var world = sim.World;
        var field = FlowField.Build(world.Map, world.Pathfinder, 30, 20, gates: 1);
        int x = 10, y = 20, steps = 0;
        while ((x, y) != (30, 20) && steps++ < 200)
        {
            int dir = field.Direction(x, y);
            Assert.True(dir >= 0, $"stuck at ({x}, {y})");
            x += Pathfinder.Dx[dir];
            y += Pathfinder.Dy[dir];
            Assert.NotEqual(world.Content.TerrainIndex("water"), world.Map.GetTerrain(x, y));
        }
        Assert.Equal((30, 20), (x, y));

        var again = FlowField.Build(world.Map, world.Pathfinder, 30, 20, gates: 1);
        for (int ty = 0; ty < world.Map.Height; ty++)
        {
            for (int tx = 0; tx < world.Map.Width; tx++)
                Assert.Equal(field.Direction(tx, ty), again.Direction(tx, ty));
        }
    }

    [Fact]
    public void A_line_puts_melee_in_front_and_archers_behind_facing_the_march()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 5, 5);
        var archers = new[] { GameSupport.Soldier(sim, "archer", 10, 10), GameSupport.Soldier(sim, "archer", 10, 11) };
        var spears = new[] { GameSupport.Soldier(sim, "spearman", 10, 12), GameSupport.Soldier(sim, "spearman", 10, 13) };
        var all = archers.Concat(spears).ToList();
        var slots = Formations.Slots(sim.World, all, 40, 11, Formation.Line);
        Assert.Equal(4, slots.Distinct().Count());
        int frontX = slots.Skip(2).Min(s => s.X), backX = slots.Take(2).Max(s => s.X);
        Assert.True(frontX > backX, $"spears at x>={frontX} should be ahead of archers at x<={backX} when marching east");
    }

    [Fact]
    public void A_group_marches_at_the_pace_of_its_slowest_member_and_ends_in_formation()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 5, 5);
        var group = new List<Entity>
        {
            GameSupport.Soldier(sim, "clubman", 10, 10), GameSupport.Soldier(sim, "clubman", 10, 11),
            GameSupport.Soldier(sim, "clubman", 10, 12), GameSupport.Soldier(sim, "ram", 10, 13),
        };
        sim.Enqueue(new MoveUnits(group.Select(u => u.Id).ToArray(), 40, 30) { Player = Players.Human });
        GameSupport.Run(sim, 2);
        int ram = sim.World.Content.Units[sim.World.Content.UnitIndex("ram")].Def.TicksPerTile;
        Assert.All(group, u => Assert.Equal(ram, u.GetComponent<Mover>().TicksPerTile));
        Assert.All(group, u => Assert.True(u.GetComponent<Mover>().UseFlow, "marching on the shared flow field"));

        GameSupport.RunUntil(sim, () => group.All(u => !u.GetComponent<Mover>().Moving), 2000);
        Assert.All(group, u => Assert.True(u.GetComponent<TilePosition>().DistanceTo(40, 30) <= 2));
        Assert.Equal(4, group.Select(u => u.GetComponent<TilePosition>()).Distinct().Count());
        GameSupport.Run(sim, 1);
        Assert.Equal(sim.World.Content.Units[0].Def.TicksPerTile, group[0].GetComponent<Mover>().TicksPerTile);
    }
}
