using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>Holds a market day at every completed marketplace each market interval (see <see cref="Markets"/>).</summary>
public sealed class MarketSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        if (world.Tick % world.Content.Economy.Market.IntervalTicks != 0) return;
        foreach (var market in World.InIdOrder(world.Store.Query<Market, Owner>()))
        {
            if (Buildings.IsComplete(market)) Markets.HoldDay(world, market);
        }
    }
}
