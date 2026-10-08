using FutureCity.Sim.Components;
using FutureCity.Sim.Navigation;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Wild animals roam their home range and breed. Births stop once as many animals as the land can carry share
/// the area (homes within the capacity radius), and a lone animal finds no mate, so a herd hunted faster than it
/// breeds dies out.
/// </summary>
public sealed class AnimalSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var animals = World.InIdOrder(world.Store.Query<Animal, TilePosition, Mover>());
        // Home ranges at the start of the tick, for counting how many animals share the land.
        var homes = animals.Select(a => a.GetComponent<Animal>()).ToList();

        foreach (var entity in animals)
        {
            var animal = entity.GetComponent<Animal>();
            var def = world.Content.Animals[animal.Kind];
            ref var mover = ref entity.GetComponent<Mover>();
            var pos = entity.GetComponent<TilePosition>();

            if (!mover.Moving && world.Rng.Chance(def.WanderChancePerTick, 100))
            {
                int x = animal.HomeX + world.Rng.NextInt(-def.WanderRadius, def.WanderRadius + 1);
                int y = animal.HomeY + world.Rng.NextInt(-def.WanderRadius, def.WanderRadius + 1);
                if (world.CanReach(pos.X, pos.Y, x, y)) // never wander off to land across the water
                    Movement.SetGoal(world, ref mover, pos, x, y);
            }

            if ((world.Tick + entity.Id) % def.BreedIntervalTicks != 0) continue;
            int sharing = homes.Count(h => h.Kind == animal.Kind
                && Math.Max(Math.Abs(h.HomeX - animal.HomeX), Math.Abs(h.HomeY - animal.HomeY)) <= def.CapacityRadius);
            if (sharing >= def.LocalCapacity || sharing < 2) continue; // sharing includes this animal
            if (!world.Rng.Chance(def.BreedChancePercent, 100)) continue;
            var young = Spawn.Animal(world, animal.Kind, pos.X, pos.Y, animal.HomeX, animal.HomeY);
            world.Emit(SimEventKind.AnimalBorn, Players.Nature, young.Id, pos.X, pos.Y);
        }
    }
}
