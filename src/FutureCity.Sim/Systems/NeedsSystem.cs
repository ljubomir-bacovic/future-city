using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Hunger, eating and starvation. Citizens eat from their band's shared store (the tribe pools its food),
/// or from what they carry when the store is empty. A starving citizen loses health and dies at zero.
/// </summary>
public sealed class NeedsSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Citizens;
        var camps = new Dictionary<int, Entity>();
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            ref var citizen = ref unit.GetComponent<Citizen>();
            int player = unit.GetComponent<Owner>().Player;
            citizen.Hunger = Math.Min(rules.MaxHunger, citizen.Hunger + rules.HungerPerTick);

            if (citizen.Hunger >= rules.EatAtHunger)
            {
                if (!camps.TryGetValue(player, out var camp) && Bands.TryGetCamp(world, player, out camp))
                    camps[player] = camp;
                Eat(rules, ref citizen, camp);
            }

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

    private static void Eat(CitizenRules rules, ref Citizen citizen, Entity camp)
    {
        int eaten = 0;
        if (!camp.IsNull)
        {
            ref var store = ref camp.GetComponent<Camp>();
            eaten = Math.Min(rules.FoodPerMeal, store.Food);
            store.Food -= eaten;
        }
        int fromHand = Math.Min(rules.FoodPerMeal - eaten, citizen.CarriedFood);
        citizen.CarriedFood -= fromHand;
        eaten += fromHand;
        // A partial meal relieves hunger in proportion.
        citizen.Hunger = Math.Max(0, citizen.Hunger - rules.HungerPerMeal * eaten / rules.FoodPerMeal);
    }
}
