using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Holds a market day at each player's marketplace every market interval (see <see cref="Markets"/>). A player has one
/// marketplace: traders' goods at the market (<see cref="Trader.AtMarket"/>) belong to that one. There is no market day
/// while enemy soldiers are near the marketplace.
/// </summary>
public sealed class MarketSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        if (world.Tick % world.Content.Economy.Market.IntervalTicks != 0) return;
        foreach (var civ in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            if (!Economy.TryGetMarket(world, civ.GetComponent<Owner>().Player, out var market)) continue;
            if (Combat.MarketUnderThreat(world, market))
            {
                // Nobody brings goods to a market with enemy soldiers at the gates.
                var pos = market.GetComponent<TilePosition>();
                world.Emit(SimEventKind.MarketClosed, civ.GetComponent<Owner>().Player, market.Id, pos.X, pos.Y);
                continue;
            }
            Markets.HoldDay(world, market);
        }
    }
}
