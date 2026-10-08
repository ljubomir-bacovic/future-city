using FutureCity.Sim.Components;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>Moves every walking entity along its route.</summary>
public sealed class MovementSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        foreach (var entity in World.InIdOrder(world.Store.Query<TilePosition, Mover>()))
            Movement.Advance(world, ref entity.GetComponent<Mover>(), ref entity.GetComponent<TilePosition>());
    }
}
