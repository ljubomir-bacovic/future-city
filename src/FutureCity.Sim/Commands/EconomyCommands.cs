using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Commands;

/// <summary>
/// Sets the player's tax rates in percent. Rates outside 0 to the content's maximum are refused (nothing changes).
/// </summary>
/// <param name="Tribute">Share of what families bring home, delivered to the public stores in goods.</param>
/// <param name="MarketTax">Share of every market sale, in coins (once there are coins).</param>
/// <param name="Tariff">Share of every trade with foreign merchants, in coins.</param>
public sealed record SetTaxes(int Tribute, int MarketTax, int Tariff) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        var taxes = world.Content.Economy.Taxes;
        if (Tribute < 0 || Tribute > taxes.Tribute.Max || MarketTax < 0 || MarketTax > taxes.MarketTax.Max
            || Tariff < 0 || Tariff > taxes.Tariff.Max || !Civics.TryGet(world, Player, out var entity))
            return;
        ref var civ = ref entity.GetComponent<Civilization>();
        civ.TributePercent = Tribute;
        civ.MarketTaxPercent = MarketTax;
        civ.TariffPercent = Tariff;
    }
}

/// <summary>
/// Sets the silver content of newly struck coins in percent (100 = full weight). Below 100 the mint strikes more
/// coins from each unit of silver: the treasury can spend more, until prices rise to match. Values outside the
/// content's minimum and 100 are refused.
/// </summary>
/// <param name="Quality">Silver content in percent.</param>
public sealed record SetCoinQuality(int Quality) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (Quality < world.Content.Economy.Money.MinQuality || Quality > 100 || !Civics.TryGet(world, Player, out var entity))
            return;
        entity.GetComponent<Civilization>().CoinQuality = Quality;
    }
}
