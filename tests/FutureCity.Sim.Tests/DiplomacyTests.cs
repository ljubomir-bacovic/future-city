using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Peace, war, alliances, trade agreements and tribute.</summary>
public class DiplomacyTests
{
    private static Simulation Neighbours(int civilizations = 2)
    {
        var sim = GameSupport.Plain();
        var spots = new[] { (8, 8), (52, 52), (52, 8), (8, 52) };
        for (int p = 1; p <= civilizations; p++)
        {
            GameSupport.Camp(sim, spots[p - 1].Item1, spots[p - 1].Item2, food: 2000, player: p);
            GameSupport.CivOf(sim, p);
        }
        return sim;
    }

    private static int Year(Simulation sim) => sim.World.Content.Calendar.TicksPerYear;

    // Runs through the step in which the year turns (systems see the tick before it advances).
    private static void RunToNewYear(Simulation sim) =>
        GameSupport.RunUntil(sim, () => sim.World.Tick % Year(sim) == 1, Year(sim) + 1);

    [Fact]
    public void A_proposal_waits_for_an_answer_and_takes_effect_when_accepted()
    {
        var sim = Neighbours();
        GameSupport.War(sim);
        sim.Enqueue(new Propose(2, ProposalKind.Peace) { Player = 1 });
        var events = GameSupport.Run(sim, 1);
        Assert.Contains(events, e => e.Kind == SimEventKind.ProposalReceived && e.Player == 2 && e.Detail == 1);
        Assert.Equal(ProposalKind.Peace, Relations.Pending(sim.World, 2, 1).Kind);
        Assert.True(Relations.AtWar(sim.World, 1, 2));

        sim.Enqueue(new Respond(1, Accept: false) { Player = 2 });
        events = GameSupport.Run(sim, 1);
        Assert.Contains(events, e => e.Kind == SimEventKind.ProposalDeclined && e.Player == 1);
        Assert.True(Relations.AtWar(sim.World, 1, 2));

        sim.Enqueue(new Propose(2, ProposalKind.Peace) { Player = 1 });
        sim.Enqueue(new Respond(1, Accept: true) { Player = 2 }, sim.World.Tick + 2);
        events = GameSupport.Run(sim, 2);
        Assert.Contains(events, e => e.Kind == SimEventKind.ProposalAccepted && e.Player == 1 && e.Detail == 2);
        Assert.Equal(Relation.Peace, Relations.Between(sim.World, 2, 1));
        Assert.Equal(ProposalKind.None, Relations.Pending(sim.World, 2, 1).Kind);
    }

    [Fact]
    public void Proposals_that_make_no_sense_now_are_refused()
    {
        var sim = Neighbours();
        Assert.False(Relations.CanPropose(sim.World, 1, 2, ProposalKind.Peace, -1, 0));       // already at peace
        Assert.False(Relations.CanPropose(sim.World, 1, 1, ProposalKind.Alliance, -1, 0));    // with oneself
        Assert.False(Relations.CanPropose(sim.World, 1, 2, ProposalKind.OfferTribute, -1, 0)); // nothing
        GameSupport.War(sim);
        Assert.False(Relations.CanPropose(sim.World, 1, 2, ProposalKind.Alliance, -1, 0));
        Assert.False(Relations.CanPropose(sim.World, 1, 2, ProposalKind.TradeAgreement, -1, 0));
        sim.Enqueue(new Propose(2, ProposalKind.Alliance) { Player = 1 });
        sim.Enqueue(new Propose(2, ProposalKind.OfferTribute, Good: "unicorns", Amount: 5) { Player = 1 });
        GameSupport.Run(sim, 1);
        Assert.Equal(ProposalKind.None, Relations.Pending(sim.World, 2, 1).Kind);
    }

    [Fact]
    public void Allies_come_to_the_defence_of_a_civilization_that_is_attacked()
    {
        var sim = Neighbours(3);
        sim.Enqueue(new Propose(3, ProposalKind.Alliance) { Player = 1 });
        sim.Enqueue(new Respond(1, true) { Player = 3 }, 2);
        GameSupport.Run(sim, 2);
        Assert.Equal(Relation.Alliance, Relations.Between(sim.World, 1, 3));

        sim.Enqueue(new DeclareWar(1) { Player = 2 });
        var events = GameSupport.Run(sim, 1);
        Assert.True(Relations.AtWar(sim.World, 2, 1));
        Assert.True(Relations.AtWar(sim.World, 3, 2), "the ally joins the war");
        Assert.Equal(Relation.Alliance, Relations.Between(sim.World, 1, 3));
        Assert.Contains(events, e => e.Kind == SimEventKind.WarDeclared && e.Player == 3 && e.Detail == 2);
    }

    [Fact]
    public void Peace_stops_the_fighting()
    {
        var sim = Neighbours();
        var a = GameSupport.Soldier(sim, "spearman", 30, 30, player: 1);
        var b = GameSupport.Soldier(sim, "spearman", 31, 30, player: 2);
        GameSupport.War(sim);
        sim.Enqueue(new Attack([a.Id], b.Id) { Player = 1 });
        Assert.Contains(GameSupport.Run(sim, 30), e => e.Kind == SimEventKind.Attacked);

        sim.Enqueue(new Propose(1, ProposalKind.Peace) { Player = 2 });
        sim.Enqueue(new Respond(2, true) { Player = 1 }, sim.World.Tick + 2);
        GameSupport.Run(sim, 3);
        Assert.DoesNotContain(GameSupport.Run(sim, 100), e => e.Kind == SimEventKind.Attacked);
        Assert.NotEqual(OrderKind.Attack, a.GetComponent<Order>().Kind);
    }

    [Fact]
    public void Tribute_in_coins_is_paid_every_year_until_the_payer_cannot_pay()
    {
        var sim = Neighbours();
        GameSupport.TraderOf(GameSupport.CivOf(sim, 2)).Coins = 150;
        sim.Enqueue(new Propose(2, ProposalKind.DemandTribute, Amount: 100) { Player = 1 });
        sim.Enqueue(new Respond(1, true) { Player = 2 }, 2);
        GameSupport.Run(sim, 2);
        Assert.Equal((-1, 100), Relations.Tribute(sim.World, 2, 1));

        RunToNewYear(sim);
        Assert.Equal(100, GameSupport.TraderOf(GameSupport.CivOf(sim, 1)).Coins);
        Assert.Equal(50, GameSupport.TraderOf(GameSupport.CivOf(sim, 2)).Coins);

        GameSupport.Run(sim, 1);
        var events = GameSupport.Run(sim, Year(sim));
        Assert.Contains(events, e => e.Kind == SimEventKind.TributeLapsed && e.Player == 2 && e.Detail == 1);
        Assert.Equal(0, Relations.Tribute(sim.World, 2, 1).Amount);
        Assert.Equal(50, GameSupport.TraderOf(GameSupport.CivOf(sim, 2)).Coins);
    }

    [Fact]
    public void Tribute_in_goods_is_carried_to_the_receivers_camp()
    {
        var sim = Neighbours();
        Assert.True(Bands.TryGetCamp(sim.World, 1, out var receiverCamp));
        Assert.True(Bands.TryGetCamp(sim.World, 2, out var payerCamp));
        GameSupport.SetAmount(payerCamp, "wood", 40);
        sim.Enqueue(new Propose(1, ProposalKind.OfferTribute, Good: "wood", Amount: 30) { Player = 2 });
        sim.Enqueue(new Respond(2, true) { Player = 1 }, 2);
        GameSupport.Run(sim, 2);
        Assert.Equal((GameSupport.Good("wood"), 30), Relations.Tribute(sim.World, 2, 1));
        RunToNewYear(sim);
        Assert.Equal(10, GameSupport.Amount(payerCamp, "wood"));
        var caravan = Assert.Single(World.InIdOrder(sim.World.Store.Query<Merchant>()));
        Assert.True(caravan.GetComponent<Merchant>().Tribute);
        GameSupport.RunUntil(sim, () => GameSupport.Amount(receiverCamp, "wood") == 30, 1000);
        GameSupport.RunUntil(sim, () => GameSupport.Count<Merchant>(sim) == 0, 1000); // and goes home
    }

    [Fact]
    public void Trade_agreement_caravans_take_the_treasurys_surplus_to_the_partners_market_without_tariffs_and_war_ends_them()
    {
        var sim = Neighbours();
        foreach (int p in new[] { 1, 2 })
        {
            var civ = GameSupport.CivOf(sim, p).GetComponent<Civilization>();
            foreach (var id in new[] { "chiefdom", "property", "coinage" }) civ.Institutions[sim.World.Content.InstitutionIndex(id)] = 1;
        }
        GameSupport.Building(sim, "marketplace", 12, 12, player: 1);
        GameSupport.Building(sim, "marketplace", 46, 46, player: 2);
        Assert.True(Bands.TryGetCamp(sim.World, 1, out var camp));
        GameSupport.SetAmount(camp, "berries", 0); // nobody lives here: all food would be surplus
        GameSupport.SetAmount(camp, "stone", 500); // far more than the treasury wants to keep
        GameSupport.TraderOf(GameSupport.CivOf(sim, 1)).Coins = 400;
        sim.Enqueue(new Propose(2, ProposalKind.TradeAgreement) { Player = 1 });
        sim.Enqueue(new Respond(1, true) { Player = 2 }, 2);
        GameSupport.Run(sim, 2);
        Assert.True(Relations.HaveTradeAgreement(sim.World, 2, 1));

        RunToNewYear(sim);
        var caravan = Assert.Single(World.InIdOrder(sim.World.Store.Query<Merchant>()), c => c.GetComponent<Merchant>().From == 1);
        Assert.False(Traders.IsForeign(caravan));
        int carried = GameSupport.Amount(caravan, "stone");
        Assert.InRange(carried, 1, sim.World.Content.Economy.TradeAgreements.CaravanCargo);
        Assert.Equal(500 - carried, GameSupport.Amount(camp, "stone"));
        int took = GameSupport.TraderOf(caravan).Coins, left = GameSupport.TraderOf(GameSupport.CivOf(sim, 1)).Coins;
        Assert.True(took > 0);
        Assert.Equal((took + left) * sim.World.Content.Economy.TradeAgreements.CaravanCoinsPercent / 100, took);
        Assert.Equal(2, caravan.GetComponent<Merchant>().Player);

        sim.Enqueue(new DeclareWar(2) { Player = 1 });
        GameSupport.Run(sim, 1);
        Assert.False(Relations.HaveTradeAgreement(sim.World, 1, 2));
        Assert.True(Combat.IsEnemy(sim.World, 2, caravan), "the enemy's caravan can now be raided");
    }
}
