using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Renewable resources: regrowth, breeding, carrying capacity, overhunting and spoilage.</summary>
public class NatureTests
{
    private static readonly Content.PlantDef Bush = TestSupport.Content.Plants[0];
    private static readonly Content.AnimalDef DeerDef = TestSupport.Content.Animals[0];

    [Fact]
    public void Plants_regrow_slowly_up_to_their_maximum()
    {
        var sim = GameSupport.Plain();
        var bush = GameSupport.Plant(sim, 10, 10, food: 10);

        GameSupport.Run(sim, Bush.RegrowIntervalTicks * 4);
        Assert.Equal(10 + 4 * Bush.RegrowAmount, bush.GetComponent<Plant>().Food);

        GameSupport.Run(sim, Bush.RegrowIntervalTicks * Bush.MaxFood);
        Assert.Equal(Bush.MaxFood, bush.GetComponent<Plant>().Food);
    }

    [Fact]
    public void A_stripped_plant_stays_bare_until_its_dormancy_ends()
    {
        var sim = GameSupport.Plain();
        var bush = GameSupport.Plant(sim, 10, 10, food: 0);
        bush.GetComponent<Plant>().DormantUntil = Bush.DormantTicks;

        GameSupport.Run(sim, Bush.DormantTicks - 1);
        Assert.Equal(0, bush.GetComponent<Plant>().Food);
        GameSupport.Run(sim, 2);
        Assert.True(bush.GetComponent<Plant>().Food > 0);
    }

    [Fact]
    public void Herds_grow_toward_the_carrying_capacity_and_stop()
    {
        var sim = GameSupport.Plain();
        for (int i = 0; i < 3; i++) GameSupport.Deer(sim, 30, 30);

        GameSupport.Run(sim, DeerDef.BreedIntervalTicks * 40);
        int grown = GameSupport.Count<Animal>(sim);
        GameSupport.Run(sim, DeerDef.BreedIntervalTicks * 40);
        int later = GameSupport.Count<Animal>(sim);

        Assert.InRange(grown, 4, DeerDef.LocalCapacity);
        Assert.Equal(DeerDef.LocalCapacity, later);
    }

    [Fact]
    public void A_lone_animal_does_not_breed()
    {
        var sim = GameSupport.Plain();
        GameSupport.Deer(sim, 30, 30);
        GameSupport.Run(sim, DeerDef.BreedIntervalTicks * 10);
        Assert.Equal(1, GameSupport.Count<Animal>(sim));
    }

    [Fact]
    public void Overhunting_wipes_out_a_herd_for_good()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 20, 20, food: 100_000);
        var herd = Enumerable.Range(0, 4).Select(i => GameSupport.Deer(sim, 28 + i, 28)).ToArray();
        var hunters = Enumerable.Range(0, 8).Select(_ => GameSupport.Adult(sim, 20, 20)).ToArray();
        sim.Enqueue(new Hunt(hunters.Select(h => h.Id).ToArray(), herd[0].Id) { Player = Players.Human });

        // A large party kills faster than the herd breeds; hunters keep taking the nearest deer until none are left.
        GameSupport.RunUntil(sim, () => GameSupport.Count<Animal>(sim) == 0, 6000);
        GameSupport.Run(sim, DeerDef.BreedIntervalTicks * 10);
        Assert.Equal(0, GameSupport.Count<Animal>(sim));
    }

    [Fact]
    public void Carcasses_spoil_and_disappear()
    {
        var sim = GameSupport.Plain();
        var carcass = sim.World.CreateEntity();
        carcass.AddComponent(new TilePosition(5, 5));
        carcass.AddComponent(new Carcass { Kind = 0, Food = 3 });

        GameSupport.Run(sim, DeerDef.CarcassDecayIntervalTicks * 3 + 1);

        Assert.False(sim.World.TryGetEntity(carcass.Id, out _));
    }
}
