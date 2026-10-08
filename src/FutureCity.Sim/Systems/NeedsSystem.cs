using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Hunger, eating and starvation. Citizens eat from their band's shared stores (the tribe pools its food), best food
/// first, or from food they carry when the stores are empty. A starving citizen loses health and dies at zero.
/// </summary>
public sealed class NeedsSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Citizens;
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            ref var citizen = ref unit.GetComponent<Citizen>();
            int player = unit.GetComponent<Owner>().Player;
            citizen.Hunger = Math.Min(rules.MaxHunger, citizen.Hunger + rules.HungerPerTick);

            if (citizen.Hunger >= rules.EatAtHunger)
                Eat(world, player, ref citizen);

            if (citizen.Hunger >= rules.MaxHunger)
                citizen.Health -= rules.StarvationDamagePerTick;
            else
                citizen.Health = Math.Min(rules.MaxHealth, citizen.Health + rules.HealthRegenPerTick);

            if (citizen.Health <= 0)
            {
                var pos = unit.GetComponent<TilePosition>();
                world.Emit(SimEventKind.DiedOfStarvation, player, unit.Id, pos.X, pos.Y);
                unit.DeleteEntity();
            }
        }
    }

    private static void Eat(World world, int player, ref Citizen citizen)
    {
        var rules = world.Content.Citizens;
        int need = rules.FoodPerMeal * 100;
        int eaten = Stores.TakeFood(world, player, need);
        int value = citizen.Carried > 0 ? world.Content.Nutrition(citizen.CarriedGood) : 0;
        if (eaten < need && value > 0)
        {
            int units = Math.Min(citizen.Carried, (need - eaten + value - 1) / value);
            citizen.Carried -= units;
            eaten += units * value;
        }
        // A partial meal relieves hunger in proportion.
        citizen.Hunger = Math.Max(0, citizen.Hunger - (int)((long)rules.HungerPerMeal * Math.Min(eaten, need) / need));
    }
}
