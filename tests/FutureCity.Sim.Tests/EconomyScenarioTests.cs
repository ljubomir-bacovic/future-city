using Friflo.Engine.ECS;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

/// <summary>
/// Prices emerge and behave: they settle at the same level wherever beliefs start, recover after a shock, and rise
/// with the amount of money (issue #40).
/// </summary>
public class EconomyScenarioTests
{
    // A grain market: producers bring `supply` grain each day; consumers earn `income` coins a day and eat what they buy.
    internal sealed class GrainMarket
    {
        private readonly Simulation _sim;
        private readonly Entity _market;
        private readonly Entity[] _producers;
        private readonly Entity[] _consumers;
        private static readonly int Grain = GameSupport.Good("grain");

        public GrainMarket(int startBelief)
        {
            _sim = GameSupport.Plain();
            GameSupport.Camp(_sim, 10, 10);
            GameSupport.Economy(_sim, money: true);
            _market = GameSupport.Marketplace(_sim, 20, 20);
            _producers = Enumerable.Range(0, 6).Select(i => Family(30, 4 + 2 * i, startBelief)).ToArray();
            _consumers = Enumerable.Range(0, 6).Select(i => Family(34, 4 + 2 * i, startBelief)).ToArray();
            foreach (var p in _producers) GameSupport.SetAmount(p, "berries", 1000); // fed: sells all its grain
        }

        private Entity Family(int x, int y, int belief)
        {
            var home = GameSupport.Home(_sim, x, y);
            GameSupport.Adult(_sim, x, y + 1).LivesIn(home);
            GameSupport.SetAmount(home, "wood", 4);
            GameSupport.SetAmount(home, "tools", 1);
            ref var trader = ref GameSupport.TraderOf(home);
            trader.Beliefs[Grain] = belief;
            foreach (var food in new[] { "berries", "meat", "bread" }) trader.Beliefs[GameSupport.Good(food)] = 1000; // grain is the cheap food
            return home;
        }

        public int Price => _market.GetComponent<Market>().Price[Grain];


        /// <summary>Runs market days and returns the price after each.</summary>
        public List<int> Days(int days, int supply, int income)
        {
            var prices = new List<int>();
            for (int d = 0; d < days; d++)
            {
                foreach (var p in _producers) GameSupport.AtMarket(_market, p, "grain", supply);
                foreach (var c in _consumers)
                {
                    GameSupport.TraderOf(c).Coins = income;
                    int bought = GameSupport.AtMarket(c, "grain");
                    if (bought > 0) Markets.Withdraw(_market, c, Grain, bought); // taken home and eaten
                }
                Markets.HoldDay(_sim.World, _market);
                prices.Add(Price);
            }
            return prices;
        }
    }

    private static double Average(IEnumerable<int> prices) => prices.Average();

    [Fact]
    public void Prices_settle_at_the_same_level_wherever_beliefs_start()
    {
        // People start out believing grain is worth 10 or 60; supply, demand and the money people have decide.
        var low = new GrainMarket(startBelief: 10).Days(120, supply: 10, income: 300);
        var high = new GrainMarket(startBelief: 60).Days(120, supply: 10, income: 300);

        double settledLow = Average(low.TakeLast(10)), settledHigh = Average(high.TakeLast(10));
        Assert.InRange(settledLow / settledHigh, 0.85, 1.15);
        foreach (var run in new[] { low, high })
        {
            var tail = run.TakeLast(10).ToArray();
            Assert.True(tail.Max() <= tail.Min() * 1.2, $"prices still swing: {string.Join(",", tail)}");
        }
    }

    [Fact]
    public void A_supply_shock_raises_the_price_which_recovers_when_supply_returns()
    {
        var market = new GrainMarket(startBelief: 20);
        double before = Average(market.Days(60, supply: 10, income: 300).TakeLast(10));
        double during = Average(market.Days(20, supply: 5, income: 300).TakeLast(5));
        double after = Average(market.Days(60, supply: 10, income: 300).TakeLast(10));

        Assert.True(during >= before * 1.3, $"before {before}, during {during}");
        Assert.InRange(after / before, 0.85, 1.15);
    }

    [Fact]
    public void More_money_chasing_the_same_goods_raises_prices()
    {
        var market = new GrainMarket(startBelief: 20);
        double before = Average(market.Days(60, supply: 10, income: 300).TakeLast(10));
        double after = Average(market.Days(60, supply: 10, income: 600).TakeLast(10));
        Assert.True(after >= before * 1.5, $"before {before}, after {after}");
    }

    [Fact]
    public void Debasing_the_coinage_causes_inflation_in_a_whole_game()
    {
        // Play until coins circulate, then continue twice from the same save: once with honest coins, once debased.
        var sim = GameSupport.NewGame(1);
        var bot = new SettlerBot(Players.Human);
        for (int i = 0; i < SimClock.FromSeconds(16 * 60) && Economy.MoneySupply(sim.World, Players.Human) == 0; i++)
        {
            bot.Act(sim);
            sim.Step();
        }
        Assert.True(Economy.HasMoney(sim.World, Players.Human), "coinage was not reached");
        for (int i = 0; i < SimClock.FromSeconds(60); i++)
        {
            bot.Act(sim);
            sim.Step();
        }
        var save = SaveGame.ToBytes(sim);

        (int Cpi, int Money) Continue(int quality)
        {
            var branch = SaveGame.Read(new MemoryStream(save), TestSupport.Content);
            var branchBot = new SettlerBot(Players.Human);
            branch.Enqueue(new SetCoinQuality(quality) { Player = Players.Human });
            for (int i = 0; i < SimClock.FromSeconds(5 * 60); i++)
            {
                branchBot.Act(branch);
                branch.Step();
            }
            Economy.TryGetMarket(branch.World, Players.Human, out var market);
            return (Markets.Cpi(branch.World, market), Economy.MoneySupply(branch.World, Players.Human));
        }

        var honest = Continue(100);
        var debased = Continue(50);
        Assert.True(debased.Money > honest.Money, $"money {honest.Money} vs {debased.Money}");
        Assert.True(debased.Cpi >= honest.Cpi * 125 / 100, $"price index {honest.Cpi} vs {debased.Cpi}");
    }
}
