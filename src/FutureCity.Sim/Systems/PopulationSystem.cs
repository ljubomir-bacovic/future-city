using Friflo.Engine.ECS;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Births. A band grows when it has a food surplus (stores large enough for everyone plus the cost of a child),
/// shelter to spare (camp and huts) and at least two adults. The child is born at camp and works once grown up.
/// Once families own their goods, children are born into families: the best-fed family with room in its hut and
/// food (at home and at the marketplace) beyond what it wants to keep, plus the cost of a child. At most one child is born per band per check.
/// </summary>
public sealed class PopulationSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var growth = world.Content.Citizens.Growth;
        if (world.Tick % growth.CheckIntervalTicks != 0) return;

        foreach (var campEntity in World.InIdOrder(world.Store.Query<Camp, Owner, TilePosition>()))
        {
            int player = campEntity.GetComponent<Owner>().Player;
            if (!Bands.TryGetCamp(world, player, out var main) || main.Id != campEntity.Id) continue; // one check per band
            var census = Bands.CensusOf(world, player);
            if (census.Adults < 2 || census.Total >= Math.Min(Buildings.ShelterOf(world, player), growth.PopulationCap)) continue;

            Entity family = default;
            if (Economy.HasHouseholds(world, player))
            {
                if (!TryFindFamily(world, player, out family)) continue;
            }
            else if (Economy.Meals(world, player) < census.Total * growth.FoodPerCapita + growth.BirthFoodCost)
            {
                continue;
            }
            if (!world.Rng.Chance(growth.BirthChancePercent, 100)) continue;

            var pos = (family.IsNull ? campEntity : family).GetComponent<TilePosition>();
            Entity child;
            if (family.IsNull)
            {
                Economy.TakeFood(world, player, growth.BirthFoodCost * 100);
                child = Spawn.Citizen(world, player, pos.X, pos.Y, world.Tick);
            }
            else
            {
                TakeFamilyFood(world, family, growth.BirthFoodCost * 100);
                child = Spawn.Citizen(world, player, pos.X, pos.Y, world.Tick);
                child.GetComponent<Citizen>().Home = family.Id;
            }
            world.Emit(SimEventKind.Birth, player, child.Id, pos.X, pos.Y);
        }
    }

    // Meals a family has at home and at the marketplace.
    private static int FamilyMeals(World world, Entity home)
    {
        var stock = home.GetComponent<Inventory>().Amounts;
        var atMarket = home.GetComponent<Trader>().AtMarket;
        return Stores.MealsIn(world, stock.Select((n, g) => n + atMarket[g]).ToArray());
    }

    // Food for a birth: from home first, then from what the family has at the marketplace.
    private static void TakeFamilyFood(World world, Entity home, int nutrition)
    {
        int taken = Stores.TakeFoodFrom(world, home, nutrition);
        if (taken >= nutrition || !Economy.TryGetMarket(world, home.GetComponent<Owner>().Player, out var market)) return;
        var atMarket = home.GetComponent<Trader>().AtMarket;
        foreach (int good in world.Content.FoodGoods)
        {
            if (taken >= nutrition) break;
            int value = world.Content.Nutrition(good);
            int units = Math.Min(atMarket[good], (nutrition - taken + value - 1) / value);
            if (units > 0) Markets.Withdraw(market, home, good, units);
            taken += units * value;
        }
    }

    // The family with room and the most food per member beyond what it wants to keep plus the cost of a child.
    private static bool TryFindFamily(World world, int player, out Entity family)
    {
        var growth = world.Content.Citizens.Growth;
        int perMember = world.Content.Economy.Households.FoodTargetPerMember;
        var members = Households.Members(world, player);
        family = default;
        long bestMeals = 0;
        int bestCount = 1;
        foreach (var home in Households.Of(world, player))
        {
            int count = members.GetValueOrDefault(home.Id)?.Count ?? 0;
            if (count == 0 || count >= Buildings.TypeOf(world, home).Def.Shelter) continue;
            int meals = FamilyMeals(world, home);
            if (meals < count * perMember + growth.BirthFoodCost) continue;
            if (!family.IsNull && meals * bestCount <= bestMeals * count) continue;
            family = home;
            bestMeals = meals;
            bestCount = count;
        }
        return !family.IsNull;
    }
}
