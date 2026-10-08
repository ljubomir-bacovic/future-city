using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Tests;

/// <summary>The emergence engine: discoveries from activity and research, institutions and eras.</summary>
public class EmergenceTests
{
    private const int Me = Players.Human;
    private static readonly Content.ContentDatabase Content = TestSupport.Content;
    private static int Interval => Content.Progress.EvaluationIntervalTicks;
    private static int Tech(string id) => Content.TechIndex(id);

    // A settled village that meets farming's and toolmaking's preconditions.
    private static (Simulation Sim, Entity Civ, Entity Camp) Village(int people = 10)
    {
        var sim = GameSupport.Plain();
        var civ = GameSupport.Civ(sim);
        var camp = GameSupport.Camp(sim, 10, 10, food: 100_000);
        for (int i = 0; i < people; i++) GameSupport.Adult(sim, 10, 10);
        GameSupport.Building(sim, "hut", 13, 10);
        GameSupport.Building(sim, "hut", 15, 10);
        GameSupport.Building(sim, "storehouse", 13, 13);
        ref var record = ref civ.GetComponent<Civilization>();
        record.Gathered[GameSupport.Good("berries")] = 1000;
        record.Gathered[GameSupport.Good("wood")] = 1000;
        record.Gathered[GameSupport.Good("stone")] = 1000;
        return (sim, civ, camp);
    }

    [Fact]
    public void Work_counts_toward_a_technology_only_once_its_preconditions_hold()
    {
        var sim = GameSupport.Plain();
        var civ = GameSupport.Civ(sim);
        GameSupport.Camp(sim, 10, 10, food: 1000);
        civ.GetComponent<Civilization>().Work[(int)WorkKind.Forage] += 100_000;

        GameSupport.Run(sim, Interval * 3);

        Assert.Equal(0, civ.GetComponent<Civilization>().TechProgress[Tech("farming")]);
        Assert.False(Civics.Knows(sim.World, Me, Tech("farming")));
    }

    [Fact]
    public void Related_work_leads_to_discovery()
    {
        var (sim, civ, _) = Village();
        civ.GetComponent<Civilization>().Work[(int)WorkKind.Forage] += 100_000;

        var events = GameSupport.Run(sim, Interval + 1);

        Assert.True(Civics.Knows(sim.World, Me, Tech("farming")));
        Assert.Contains(events, e => e.Kind == SimEventKind.TechDiscovered && e.Detail == Tech("farming"));
        Assert.False(Civics.Knows(sim.World, Me, Tech("toolmaking")), "foraging teaches nothing about tools");
    }

    [Fact]
    public void Some_progress_gives_a_chance_of_an_early_discovery()
    {
        var (sim, civ, _) = Village();
        int cost = Content.Techs[Tech("farming")].Def.Cost;
        // Just under the full cost: discovery is likely soon, but not certain on the first evaluation.
        civ.GetComponent<Civilization>().TechProgress[Tech("farming")] = cost * 9 / 10;
        GameSupport.RunUntil(sim, () => Civics.Knows(sim.World, Me, Tech("farming")), Interval * 200);
    }

    [Fact]
    public void Shrine_research_goes_to_the_chosen_focus()
    {
        var (sim, civ, _) = Village();
        sim.Enqueue(new SetResearchFocus("toolmaking") { Player = Me });
        GameSupport.Run(sim, 1);
        civ.GetComponent<Civilization>().Work[(int)WorkKind.Research] += 200;

        GameSupport.Run(sim, Interval);

        var record = civ.GetComponent<Civilization>();
        Assert.Equal(Tech("toolmaking"), record.ResearchFocus);
        Assert.True(record.TechProgress[Tech("toolmaking")] >= 200 || record.Techs[Tech("toolmaking")] == 1);
        Assert.Equal(0, record.TechProgress[Tech("farming")]);
    }

    [Fact]
    public void Shrine_workers_produce_research()
    {
        var (sim, civ, _) = Village();
        var shrine = GameSupport.Building(sim, "shrine", 17, 13);
        var elder = GameSupport.Adult(sim, 16, 13);
        sim.Enqueue(new AssignWork([elder.Id], shrine.Id) { Player = Me });

        GameSupport.Run(sim, 100);

        Assert.True(civ.GetComponent<Civilization>().Work[(int)WorkKind.Research] > 50);
    }

    [Fact]
    public void An_institution_is_established_by_choice_and_costs_food()
    {
        var (sim, civ, camp) = Village(people: 12);
        var chiefdom = Content.InstitutionIndex("chiefdom");
        Assert.False(Civics.CanEstablish(sim.World, Me, chiefdom, Civics.FactsOf(sim.World, Me)), "needs three huts");
        sim.Enqueue(new EstablishInstitution("chiefdom") { Player = Me });
        GameSupport.Run(sim, 1);
        Assert.False(Civics.Has(sim.World, Me, chiefdom));

        GameSupport.Building(sim, "hut", 17, 10);
        int before = GameSupport.Berries(camp);
        sim.Enqueue(new EstablishInstitution("chiefdom") { Player = Me });
        var events = GameSupport.Run(sim, 1);

        Assert.True(Civics.Has(sim.World, Me, chiefdom));
        Assert.True(Civics.HasAutoJobs(sim.World, Me));
        Assert.Contains(events, e => e.Kind == SimEventKind.InstitutionEstablished && e.Detail == chiefdom);
        Assert.True(before - GameSupport.Berries(camp) >= Content.Institutions[chiefdom].Def.FoodCost);
    }

    [Fact]
    public void The_era_changes_when_its_conditions_hold()
    {
        var (sim, civ, _) = Village(people: 14);
        var darkAges = Content.Eras[1];
        Assert.Contains(Civics.Explain(sim.World, darkAges.Preconditions, Civics.FactsOf(sim.World, Me)), p => !p.Met);

        GameSupport.Learn(sim, "farming");
        GameSupport.Learn(sim, "toolmaking");
        GameSupport.Establish(sim);
        GameSupport.Building(sim, "farm", 20, 20);
        GameSupport.Building(sim, "farm", 24, 20);
        var events = GameSupport.Run(sim, Interval + 1);

        Assert.Equal(1, civ.GetComponent<Civilization>().Era);
        Assert.Contains(events, e => e.Kind == SimEventKind.EraReached && e.Detail == 1);
        Assert.All(Civics.Explain(sim.World, darkAges.Preconditions, Civics.FactsOf(sim.World, Me)), p => Assert.True(p.Met));
    }

    [Fact]
    public void Buildings_unlock_with_knowledge()
    {
        var (sim, _, _) = Village();
        int farm = Content.BuildingIndex("farm");
        Assert.Equal(Placement.NotAvailable, Buildings.CanPlace(sim.World, Me, farm, 30, 30));
        GameSupport.Learn(sim, "farming");
        Assert.Equal(Placement.Ok, Buildings.CanPlace(sim.World, Me, farm, 30, 30));
    }
}
