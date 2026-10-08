using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// How short a civilization is of each good: the stock it wants per person minus what its stores hold, plus building
/// materials that sites still wait for. Food goods share one demand, counted in meals. Drives automatic jobs.
/// </summary>
public sealed class Demand
{
    private readonly int[] _byGood;

    private Demand(int[] byGood, int food, int[] store, Facts facts)
    {
        _byGood = byGood;
        Food = food;
        Store = store;
        Facts = facts;
    }

    /// <summary>Meals short of the band's food target.</summary>
    public int Food { get; }

    /// <summary>Units in the stores, by good.</summary>
    public IReadOnlyList<int> Store { get; }

    /// <summary>The facts the demand was computed from.</summary>
    public Facts Facts { get; }

    /// <summary>Units short of a good (for food goods, the meals short of the food target, if that is larger).</summary>
    public int ForGood(int good) => _byGood[good];

    /// <summary>Computes the player's current demand.</summary>
    public static Demand Of(World world, int player)
    {
        var content = world.Content;
        var facts = Civics.FactsOf(world, player);
        var store = facts.Store;
        int population = facts.Population;
        int food = Math.Max(0, content.Citizens.Jobs.FoodTargetPerCapita * population - facts.Food);

        var sitesNeed = new int[content.Goods.Count];
        foreach (var site in world.Store.Query<Construction, Building, Owner>().Entities)
        {
            if (site.GetComponent<Owner>().Player != player) continue;
            var missing = Buildings.MissingMaterials(world, site);
            for (int g = 0; g < missing.Length; g++) sitesNeed[g] += missing[g];
        }

        var byGood = new int[content.Goods.Count];
        for (int g = 0; g < byGood.Length; g++)
        {
            int wanted = content.Goods[g].TargetPerCapita * population + sitesNeed[g];
            byGood[g] = Math.Max(0, wanted - store[g]);
            if (content.Goods[g].Nutrition > 0) byGood[g] = Math.Max(byGood[g], food);
        }
        return new Demand(byGood, food, store, facts);
    }
}
