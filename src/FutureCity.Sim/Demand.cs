using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// How short the public stores are of each good: the stock wanted per person minus what the stores hold, plus building
/// materials that sites still wait for. Food goods share one demand, counted in meals. Drives the chief's jobs.
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
        var store = Stores.Totals(world, player); // public stores: the shared stores, later the treasury's
        int population = facts.Population;
        int foodTarget = Economy.HasHouseholds(world, player)
            ? content.Economy.Treasury.FoodTargetPerCapita
            : content.Citizens.Jobs.FoodTargetPerCapita;
        int food = Math.Max(0, foodTarget * population - Stores.MealsIn(world, store));

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
            if (g == content.SilverGood && Economy.HasHouseholds(world, player)) wanted += content.Economy.Treasury.SilverTarget;
            byGood[g] = Math.Max(0, wanted - store[g]);
            if (content.Goods[g].Nutrition > 0) byGood[g] = Math.Max(byGood[g], food);
        }
        return new Demand(byGood, food, store, facts);
    }
}
