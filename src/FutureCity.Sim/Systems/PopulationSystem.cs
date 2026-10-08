using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Births. A band grows when it has a food surplus (stores large enough for everyone plus the cost of a child),
/// shelter to spare (camp and huts) and at least two adults. The child is born at camp and works once grown up.
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
            if (Stores.Meals(world, player) < census.Total * growth.FoodPerCapita + growth.BirthFoodCost) continue;
            if (!world.Rng.Chance(growth.BirthChancePercent, 100)) continue;

            Stores.TakeFood(world, player, growth.BirthFoodCost * 100);
            var pos = campEntity.GetComponent<TilePosition>();
            var child = Spawn.Citizen(world, player, pos.X, pos.Y, world.Tick);
            world.Emit(SimEventKind.Birth, player, child.Id, pos.X, pos.Y);
        }
    }
}
