using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Wild plants regrow slowly while they still bear fruit, fastest in summer and not at all in winter. A plant stripped
/// bare stays dormant for a while before it fruits again, so over-gathering an area leaves it empty for a long time.
/// </summary>
public sealed class PlantGrowthSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        int regrowPercent = Calendar.Season(world).PlantRegrowPercent;
        if (regrowPercent == 0) return; // nothing fruits in this season
        // Each plant changes only itself and uses no randomness, so storage order is fine here.
        foreach (var entity in world.Store.Query<Plant>().Entities)
        {
            ref var plant = ref entity.GetComponent<Plant>();
            var def = world.Content.Plants[plant.Kind];
            if (plant.Food == 0)
            {
                if (world.Tick >= plant.DormantUntil)
                    plant.Food = Math.Clamp(def.RegrowAmount, 1, def.MaxFood);
            }
            else if (plant.Food < def.MaxFood
                     && (world.Tick + entity.Id) % Math.Max(1, def.RegrowIntervalTicks * 100 / regrowPercent) == 0)
            {
                plant.Food = Math.Min(def.MaxFood, plant.Food + def.RegrowAmount);
            }
        }
    }
}
