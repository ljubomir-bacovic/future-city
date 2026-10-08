using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Fields: seasons, growth, harvest, rot and soil fertility.</summary>
public class FarmingTests
{
    private const int Me = Players.Human;
    private static readonly Content.FieldDef FieldRules = TestSupport.Content.Buildings[TestSupport.Content.BuildingIndex("farm")].Def.Field!;

    private static (Simulation Sim, Entity Camp, Entity Farm) Setup(int fertility = 80)
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 10_000);
        GameSupport.SetFertility(sim, 13, 10, 3, fertility);
        var farm = GameSupport.Building(sim, "farm", 13, 10);
        return (sim, camp, farm);
    }

    private static void Farmers(Simulation sim, Entity farm, int count)
    {
        var people = Enumerable.Range(0, count).Select(_ => GameSupport.Adult(sim)).Select(p => p.Id).ToArray();
        sim.Enqueue(new AssignWork(people, farm.Id) { Player = Me });
    }

    [Fact]
    public void A_field_takes_its_fertility_from_the_soil()
    {
        var (_, _, farm) = Setup(fertility: 64);
        Assert.Equal(64, farm.GetComponent<Field>().Fertility);
        Assert.Equal(64, farm.GetComponent<Field>().NaturalFertility);
    }

    [Fact]
    public void Farmers_sow_in_spring_and_bring_in_the_harvest()
    {
        var (sim, camp, farm) = Setup();
        GameSupport.Civ(sim);
        Farmers(sim, farm, 3);

        GameSupport.RunUntil(sim, () => farm.GetComponent<Field>().Stage == FieldStage.Growing, 200);
        Assert.Equal("spring", Calendar.Season(sim.World).Id);
        GameSupport.RunUntil(sim, () => farm.GetComponent<Field>().Stage == FieldStage.Ripe, TestSupport.Content.Calendar.TicksPerYear);
        int crop = farm.GetComponent<Field>().Remaining;
        Assert.Equal(FieldRules.Yield * 80 / 100, crop);

        GameSupport.RunUntil(sim, () => farm.GetComponent<Field>().Stage == FieldStage.Fallow, TestSupport.Content.Calendar.TicksPerYear);
        Assert.NotEqual("winter", Calendar.Season(sim.World).Id); // brought in before it could rot
        GameSupport.RunUntil(sim, () => GameSupport.Amount(camp, "grain") == crop, TestSupport.Content.Calendar.TicksPerYear);
        Assert.Equal(crop, GameSupport.Civ(sim).GetComponent<Civilization>().Produced[GameSupport.Good("grain")]);
        Assert.InRange(farm.GetComponent<Field>().Fertility, 80 - FieldRules.FertilityPerHarvest, 79); // exhausted, then slowly recovering while fallow
    }

    [Fact]
    public void Nothing_is_sown_outside_the_sowing_seasons()
    {
        var (sim, _, farm) = Setup();
        GameSupport.RunToSeason(sim, "autumn");
        Farmers(sim, farm, 2);

        GameSupport.Run(sim, Calendar.TicksPerSeason(TestSupport.Content) * 2 - 1); // autumn and winter

        Assert.Equal(FieldStage.Fallow, farm.GetComponent<Field>().Stage);
        Assert.Equal(0, farm.GetComponent<Field>().Progress);
    }

    [Fact]
    public void Poor_soil_grows_slower_and_yields_less()
    {
        (int Ticks, int Crop) Grow(int fertility)
        {
            var (sim, _, farm) = Setup(fertility);
            farm.GetComponent<Field>().Stage = FieldStage.Growing;
            int ticks = GameSupport.RunUntil(sim, () => farm.GetComponent<Field>().Stage == FieldStage.Ripe, 3000);
            return (ticks, farm.GetComponent<Field>().Remaining);
        }

        var rich = Grow(90);
        var poor = Grow(45);
        Assert.True(poor.Ticks > rich.Ticks, $"poor {poor.Ticks} rich {rich.Ticks}");
        Assert.Equal(FieldRules.Yield * 45 / 100, poor.Crop);
        Assert.Equal(FieldRules.Yield * 90 / 100, rich.Crop);
    }

    [Fact]
    public void Ripe_crops_left_in_the_field_rot_in_winter()
    {
        var (sim, _, farm) = Setup();
        GameSupport.RunToSeason(sim, "autumn");
        farm.GetComponent<Field>() = farm.GetComponent<Field>() with { Stage = FieldStage.Ripe, Remaining = 50 };

        var events = new List<SimEvent>();
        GameSupport.RunUntil(sim, () => { events.AddRange(sim.World.Events); return Calendar.Season(sim.World).Id == "winter"; }, 200);
        events.AddRange(GameSupport.Run(sim, 1)); // the first winter tick

        Assert.Equal(FieldStage.Fallow, farm.GetComponent<Field>().Stage);
        Assert.Equal(0, farm.GetComponent<Field>().Remaining);
        Assert.Contains(events, e => e.Kind == SimEventKind.CropsRotted && e.Entity == farm.Id);
    }

    [Fact]
    public void Fallow_fields_recover_up_to_their_natural_fertility()
    {
        var (sim, _, farm) = Setup(fertility: 60);
        farm.GetComponent<Field>().Fertility = 50;

        GameSupport.Run(sim, FieldRules.FertilityRecoveryIntervalTicks * 5);
        Assert.InRange(farm.GetComponent<Field>().Fertility, 54, 56);

        GameSupport.Run(sim, FieldRules.FertilityRecoveryIntervalTicks * 20);
        Assert.Equal(60, farm.GetComponent<Field>().Fertility);
    }
}
