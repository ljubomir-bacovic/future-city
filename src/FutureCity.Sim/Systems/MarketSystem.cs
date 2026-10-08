using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Holds a market day at each player's marketplace every market interval (see <see cref="Markets"/>). A player has one
/// marketplace: traders' goods at the market (<see cref="Trader.AtMarket"/>) belong to that one.
/// </summary>
public sealed class MarketSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        if (world.Tick % world.Content.Economy.Market.IntervalTicks != 0) return;
        foreach (var civ in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            if (Economy.TryGetMarket(world, civ.GetComponent<Owner>().Player, out var market))
                Markets.HoldDay(world, market);
        }
    }
}
