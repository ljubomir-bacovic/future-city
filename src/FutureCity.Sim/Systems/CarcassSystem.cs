using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>Meat spoils: a carcass loses food over time and disappears when nothing is left.</summary>
public sealed class CarcassSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        foreach (var entity in World.InIdOrder(world.Store.Query<Carcass>()))
        {
            ref var carcass = ref entity.GetComponent<Carcass>();
            if ((world.Tick + entity.Id) % world.Content.Animals[carcass.Kind].CarcassDecayIntervalTicks != 0) continue;
            if (--carcass.Food <= 0)
                entity.DeleteEntity();
        }
    }
}
