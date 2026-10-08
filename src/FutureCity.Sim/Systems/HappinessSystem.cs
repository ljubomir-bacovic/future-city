using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Happiness, once families own their goods. Each check every citizen's happiness moves a step toward a target set
/// by needs met (food, a home, firewood), taxes, safety (recent starvation deaths), health and the family's wealth
/// compared with others. Happy people work faster (see <see cref="Labor.Productivity"/>); when the average falls below
/// the unrest threshold the people are in unrest.
/// </summary>
public sealed class HappinessSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Economy.Happiness;
        if (world.Tick % rules.CheckIntervalTicks != 0) return;
        foreach (var civEntity in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            int player = civEntity.GetComponent<Owner>().Player;
            if (!Economy.HasHouseholds(world, player)) continue;
            Update(world, player, ref civEntity.GetComponent<Civilization>(), civEntity.Id);
        }
    }

    private static void Update(World world, int player, ref Civilization civ, int civId)
    {
        var rules = world.Content.Economy.Happiness;
        var wealth = Society.Wealth(world, player);
        var nobles = Society.NobleHomes(world, player, wealth);
        long average = wealth.Count == 0 ? 0 : wealth.Values.Sum() / wealth.Count;
        int shared = rules.Base
                     - (civ.TributePercent + (Economy.HasMoney(world, player) ? civ.MarketTaxPercent : 0)) * rules.TaxPercent / 100
                     + Math.Max(rules.MaxDeathPenalty, (civ.DeathsLastYear + civ.DeathsThisYear) * rules.DeathPenalty);
        var citizens = world.Content.Citizens;
        long sum = 0;
        int people = 0;
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            if (unit.GetComponent<Owner>().Player != player) continue;
            ref var citizen = ref unit.GetComponent<Citizen>();
            int target = shared;
            target += citizen.Hunger >= citizens.MaxHunger * 8 / 10 ? rules.Hungry : citizen.Hunger < citizens.EatAtHunger ? rules.Fed : 0;
            target += citizen.Health * 2 < citizens.MaxHealth ? rules.Sick : 0;
            if (Households.TryGetHome(world, unit, out var home))
            {
                target += rules.Home;
                if (home.GetComponent<Household>().Cold) target += rules.Cold;
                if (nobles.Contains(home.Id)) target += rules.Rich;
                else if (average > 0 && wealth.GetValueOrDefault(home.Id) * 3 < average) target += rules.Poor;
            }
            else
            {
                target += rules.Homeless;
            }
            target = Math.Clamp(target, 0, 100);
            int gap = target - citizen.Happiness;
            citizen.Happiness += Math.Clamp(gap, -rules.Step, rules.Step);
            sum += citizen.Happiness;
            people++;
        }

        int mood = people == 0 ? rules.Base : (int)(sum / people);
        if (!civ.Unrest && mood < rules.UnrestBelow)
        {
            civ.Unrest = true;
            Bands.TryGetCamp(world, player, out var camp);
            var at = camp.IsNull ? default : camp.GetComponent<TilePosition>();
            world.Emit(SimEventKind.Unrest, player, civId, at.X, at.Y, mood);
        }
        else if (civ.Unrest && mood >= rules.UnrestBelow + rules.Step * 2)
        {
            civ.Unrest = false;
        }
    }
}
