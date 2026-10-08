using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Hunger, eating, starvation, natural death and births.</summary>
public class CitizenTests
{
    private static readonly Content.CitizenRules Rules = TestSupport.Content.Citizens;

    [Fact]
    public void Hungry_citizens_eat_from_the_shared_store()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, food: 100);
        var person = GameSupport.Adult(sim, 30, 30); // far from camp: the store is shared by the whole band
        person.GetComponent<Citizen>().Hunger = Rules.EatAtHunger - Rules.HungerPerTick;

        sim.Step();

        Assert.Equal(100 - Rules.FoodPerMeal, camp.GetComponent<Camp>().Food);
        Assert.Equal(Rules.EatAtHunger - Rules.HungerPerMeal, person.GetComponent<Citizen>().Hunger);
    }

    [Fact]
    public void Citizens_eat_what_they_carry_when_the_store_is_empty()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, food: 0);
        var person = GameSupport.Adult(sim);
        person.GetComponent<Citizen>() = person.GetComponent<Citizen>() with { Hunger = Rules.EatAtHunger, CarriedFood = Rules.FoodPerMeal + 3 };

        sim.Step();

        Assert.Equal(3, person.GetComponent<Citizen>().CarriedFood);
    }

    [Fact]
    public void Without_food_citizens_starve_to_death()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, food: 0);
        var person = GameSupport.Adult(sim);
        int id = person.Id;

        int ticksToStarving = Rules.MaxHunger / Rules.HungerPerTick;
        int ticksToDeath = Rules.MaxHealth / Rules.StarvationDamagePerTick;
        var events = GameSupport.Run(sim, ticksToStarving + ticksToDeath + 5);

        Assert.False(sim.World.TryGetEntity(id, out _));
        Assert.Contains(events, e => e is { Kind: SimEventKind.DiedOfStarvation, Entity: var who } && who == id);
    }

    [Fact]
    public void Starving_citizens_recover_when_they_eat_again()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, food: 0);
        var person = GameSupport.Adult(sim);
        person.GetComponent<Citizen>() = person.GetComponent<Citizen>() with { Hunger = Rules.MaxHunger, Health = Rules.MaxHealth / 2 };

        GameSupport.Run(sim, 10);
        Assert.True(person.GetComponent<Citizen>().Health < Rules.MaxHealth / 2);

        camp.GetComponent<Camp>().Food = 1000;
        GameSupport.Run(sim, 10 + Rules.MaxHealth / Math.Max(1, Rules.HealthRegenPerTick));
        Assert.Equal(Rules.MaxHealth, person.GetComponent<Citizen>().Health);
    }

    [Fact]
    public void The_very_old_die_of_old_age_and_the_young_do_not()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, food: 100_000);
        var elder = Spawn.Citizen(sim.World, Players.Human, 10, 10, -(Rules.OldAgeYears + 100L) * Rules.TicksPerYear);
        var young = GameSupport.Adult(sim);
        int elderId = elder.Id;

        var events = GameSupport.Run(sim, Rules.TicksPerYear * 2);

        Assert.Contains(events, e => e.Kind == SimEventKind.DiedOfOldAge && e.Entity == elderId);
        Assert.True(sim.World.TryGetEntity(young.Id, out _));
    }

    [Fact]
    public void Children_cannot_be_given_orders()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, food: 1000);
        var child = Spawn.Citizen(sim.World, Players.Human, 10, 10, sim.World.Tick);
        sim.Enqueue(new MoveUnits([child.Id], 20, 20) { Player = Players.Human });
        GameSupport.Run(sim, 50);
        Assert.Equal(OrderKind.Idle, child.GetComponent<Order>().Kind);
        Assert.Equal(new TilePosition(10, 10), child.GetComponent<TilePosition>());
    }

    [Fact]
    public void A_fed_and_sheltered_band_has_children()
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, food: 100_000);
        GameSupport.Adult(sim);
        GameSupport.Adult(sim);

        var events = GameSupport.Run(sim, Rules.Growth.CheckIntervalTicks * 20);

        int births = events.Count(e => e.Kind == SimEventKind.Birth);
        Assert.True(births > 0);
        Assert.Equal(2 + births, Bands.CensusOf(sim.World, Players.Human).Total);
        Assert.True(camp.GetComponent<Camp>().Food <= 100_000 - births * Rules.Growth.BirthFoodCost);
    }

    [Theory]
    [InlineData(0, 100, 2)]       // no surplus
    [InlineData(100_000, 2, 2)]   // no room in the shelter
    [InlineData(100_000, 100, 1)] // nobody to have children with
    public void No_births_without_surplus_shelter_and_two_adults(int food, int shelter, int adults)
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, food: food);
        camp.GetComponent<Camp>().Shelter = shelter;
        for (int i = 0; i < adults; i++) GameSupport.Adult(sim);

        // Short enough that nobody gets hungry enough to eat.
        var events = GameSupport.Run(sim, Math.Min(Rules.EatAtHunger - 1, Rules.Growth.CheckIntervalTicks * 3));

        Assert.DoesNotContain(events, e => e.Kind == SimEventKind.Birth);
    }
}
