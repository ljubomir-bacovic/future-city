using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Tests;

/// <summary>The precondition language of the emergence engine.</summary>
public class ConditionTests
{
    private static readonly Content.ContentDatabase Content = TestSupport.Content;

    private static Condition Parse(string text) => Condition.Parse(text, Content.ResolveFact);

    private static Facts Facts(int population = 0, int hut = 0, bool farming = false, int wood = 0)
    {
        var buildings = new int[Content.Buildings.Count];
        buildings[Content.BuildingIndex("hut")] = hut;
        var techs = new int[Content.Techs.Count];
        techs[Content.TechIndex("farming")] = farming ? 1 : 0;
        var store = new int[Content.Goods.Count];
        store[Content.GoodIndex("wood")] = wood;
        return new Facts
        {
            Population = population,
            Adults = population,
            Store = store,
            Gathered = new int[Content.Goods.Count],
            Produced = new int[Content.Goods.Count],
            Buildings = buildings,
            Techs = techs,
            Institutions = new int[Content.Institutions.Count],
            Classes = new int[Society.ClassIds.Length],
        };
    }

    [Theory]
    [InlineData("population >= 12", 12, true)]
    [InlineData("population >= 12", 11, false)]
    [InlineData("population > 2 * 5 + 1", 12, true)]
    [InlineData("population - 2 == 10", 12, true)]
    [InlineData("population / 4 != 3", 12, false)]
    [InlineData("!(population < 5) && (population < 10 || population == 12)", 12, true)]
    [InlineData("population >= 5 || store.wood >= 100", 1, false)]
    [InlineData("-population < 0", 1, true)]
    [InlineData("population / 0 == 0", 7, true)]
    public void Expressions_evaluate_with_integer_math(string text, int population, bool expected)
    {
        Assert.Equal(expected, Parse(text).IsMet(Facts(population)));
    }

    [Fact]
    public void An_empty_condition_always_holds()
    {
        Assert.Same(Condition.Always, Parse("  "));
        Assert.True(Parse("").IsMet(Facts()));
    }

    [Theory]
    [InlineData("population >=", "ends too early")]
    [InlineData("population >= 3 &&", "ends too early")]
    [InlineData("(population >= 3", "missing ')'")]
    [InlineData("population >= 3)", "unexpected ')'")]
    [InlineData("castles >= 1", "unknown fact 'castles'")]
    [InlineData("building.castle >= 1", "unknown fact 'building.castle'")]
    [InlineData("store.gold > 0", "unknown fact 'store.gold'")]
    [InlineData("population # 3", "unexpected '#'")]
    public void Bad_conditions_are_rejected_with_a_reason(string text, string reason)
    {
        var error = Assert.Throws<ConditionException>(() => Parse(text));
        Assert.Contains(reason, error.Message);
    }

    [Fact]
    public void Explain_lists_each_requirement_with_progress()
    {
        var condition = Parse("population >= 12 && building.hut >= 3 && tech.farming && store.wood >= population * 2");

        var parts = condition.Explain(Facts(population: 10, hut: 3, wood: 4), Content.FactLabel);

        Assert.Equal(
            [
                new ConditionPart("People", 10, 12, false),
                new ConditionPart("Hut built", 3, 3, true),
                new ConditionPart("Farming", 0, 1, false),
                new ConditionPart("Wood in store", 4, 20, false),
            ],
            parts);
    }

    [Fact]
    public void Shipped_preconditions_parse_and_reference_real_facts()
    {
        foreach (var tech in Content.Techs) Assert.False(tech.Preconditions.IsAlways, tech.Def.Id);
        Assert.True(Content.Eras[0].Preconditions.IsAlways, "every game starts in the first era");
        Assert.All(Content.Eras.Skip(1), e => Assert.False(e.Preconditions.IsAlways));
    }
}
