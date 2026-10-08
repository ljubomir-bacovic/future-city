using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Births. A band grows when it has a food surplus (a store large enough for everyone plus the cost of a child),
/// shelter to spare and at least two adults. The child is born at camp and works once grown up.
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
            ref var camp = ref campEntity.GetComponent<Camp>();
            var census = Bands.CensusOf(world, player);
            if (census.Adults < 2 || census.Total >= Math.Min(camp.Shelter, growth.PopulationCap)) continue;
            if (camp.Food < census.Total * growth.FoodPerCapita + growth.BirthFoodCost) continue;
            if (!world.Rng.Chance(growth.BirthChancePercent, 100)) continue;

            camp.Food -= growth.BirthFoodCost;
            var pos = campEntity.GetComponent<TilePosition>();
            var child = Spawn.Citizen(world, player, pos.X, pos.Y, world.Tick);
            world.Emit(SimEventKind.Birth, player, child.Id, pos.X, pos.Y);
        }
    }
}
