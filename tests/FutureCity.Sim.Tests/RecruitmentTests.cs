using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Recruiting takes workers away, soldiers are armed from the public stores, fed and (when paid) paid.</summary>
public class RecruitmentTests
{
    private static (Simulation Sim, Entity Camp) Village(int wood = 20)
    {
        var sim = GameSupport.Plain();
        var camp = GameSupport.Camp(sim, 10, 10, food: 2000);
        GameSupport.SetAmount(camp, "wood", wood);
        GameSupport.Civ(sim);
        return (sim, camp);
    }

    private static void Recruit(Simulation sim, string unit, int count, Service service = Service.Levy, int player = Players.Human) =>
        sim.Enqueue(new Recruit(unit, count, service) { Player = player });

    private static List<Entity> Soldiers(Simulation sim) => World.InIdOrder(sim.World.Store.Query<Soldier>());

    [Fact]
    public void Recruits_leave_their_work_and_collect_their_equipment_from_the_public_stores()
    {
        var (sim, camp) = Village();
        GameSupport.SetAmount(camp, "berries", 0);
        GameSupport.Establish(sim); // the chief sends anyone idle to gather the food the stores lack
        for (int x = 4; x < 8; x++) GameSupport.Plant(sim, x, 20, 100);
        var people = Enumerable.Range(0, 3).Select(i => GameSupport.Adult(sim, 14 + i, 14)).ToList();
        GameSupport.Run(sim, 25); // everyone gets a job
        Assert.All(people, p => Assert.NotEqual(OrderKind.Idle, p.GetComponent<Order>().Kind));

        Recruit(sim, "clubman", 2);
        GameSupport.Run(sim, 1);
        var soldiers = Soldiers(sim);
        Assert.Equal(2, soldiers.Count);
        GameSupport.RunUntil(sim, () => soldiers.All(s => s.GetComponent<Soldier>().Equipped), 300);
        Assert.Equal(20 - 2 * 2, GameSupport.Amount(camp, "wood"));
        Assert.All(soldiers, s => Assert.True(s.GetComponent<TilePosition>().DistanceTo(10, 10) <= 2, "armed at the camp"));

        GameSupport.Run(sim, 100); // the chief does not put soldiers back to work
        Assert.All(soldiers, s => Assert.True(s.GetComponent<Order>().Kind is OrderKind.Idle or OrderKind.Move));
        Assert.Equal(1, sim.World.Store.Query<Citizen>().Count - Soldiers(sim).Count);
    }

    [Fact]
    public void Soldiers_wait_for_equipment_the_treasury_lacks_and_it_counts_as_demand()
    {
        var (sim, camp) = Village(wood: 0);
        GameSupport.Learn(sim, "toolmaking");
        GameSupport.Adult(sim, 12, 12);
        Recruit(sim, "spearman", 1);
        GameSupport.Run(sim, 100);
        var soldier = Assert.Single(Soldiers(sim));
        Assert.False(soldier.GetComponent<Soldier>().Equipped);
        var wanted = Military.EquipmentWanted(sim.World, Players.Human);
        Assert.Equal(2, wanted[GameSupport.Good("wood")]);
        Assert.Equal(1, wanted[GameSupport.Good("tools")]);
        Assert.Equal(2, Demand.Of(sim.World, Players.Human).SitesNeed(GameSupport.Good("wood")));

        GameSupport.SetAmount(camp, "wood", 5);
        GameSupport.SetAmount(camp, "tools", 1);
        GameSupport.RunUntil(sim, () => soldier.GetComponent<Soldier>().Equipped, 200);
        Assert.Equal(3, GameSupport.Amount(camp, "wood"));
        Assert.Equal(0, GameSupport.Amount(camp, "tools"));
    }

    [Fact]
    public void A_unit_needs_its_requirements_and_paid_service_needs_coins()
    {
        var (sim, _) = Village();
        GameSupport.Adult(sim, 12, 12);
        Recruit(sim, "archer", 1);           // archery is unknown
        Recruit(sim, "clubman", 1, Service.Paid); // no coins yet
        Recruit(sim, "catapult", 1);         // no such unit
        GameSupport.Run(sim, 2);
        Assert.Empty(Soldiers(sim));
        Assert.Equal(Recruitment.NotAvailable, Military.CanRecruit(sim.World, Players.Human, sim.World.Content.UnitIndex("archer"), Service.Levy));
        Assert.Equal(Recruitment.NeedsCoins, Military.CanRecruit(sim.World, Players.Human, 0, Service.Paid));

        GameSupport.Economy(sim, money: true);
        Recruit(sim, "clubman", 1, Service.Paid);
        GameSupport.Run(sim, 2);
        Assert.Equal(Service.Paid, Assert.Single(Soldiers(sim)).GetComponent<Soldier>().Service);
        Assert.Equal(Recruitment.NoOne, Military.CanRecruit(sim.World, Players.Human, 0, Service.Levy));
    }

    [Fact]
    public void Paid_soldiers_send_their_wage_home_and_desert_when_the_treasury_runs_dry()
    {
        var (sim, _) = Village();
        var civ = GameSupport.Civ(sim);
        GameSupport.Establish(sim, "property");
        GameSupport.Establish(sim, "coinage"); // no chiefdom: the wage stays where the test sets it
        civ.GetComponent<Civilization>().PublicWage = 100;
        int interval = sim.World.Content.Citizens.Jobs.CheckIntervalTicks;
        int wage = 100 * interval / 100;
        GameSupport.TraderOf(civ).Coins = 3 * wage;
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.Adult(sim, 12, 12).LivesIn(home);
        Recruit(sim, "clubman", 1, Service.Paid);
        GameSupport.Run(sim, 3 * interval + 1);
        Assert.Equal(0, GameSupport.TraderOf(civ).Coins);
        Assert.Equal(3 * wage, GameSupport.TraderOf(home).Coins);
        Assert.Equal(3 * wage, civ.GetComponent<Civilization>().Ledger[(int)LedgerEntry.Soldiers]);

        int desertAfter = sim.World.Content.Military.Combat.DesertAfterUnpaidChecks;
        var events = GameSupport.Run(sim, desertAfter * interval);
        Assert.Contains(events, e => e.Kind == SimEventKind.Deserted);
        Assert.Empty(Soldiers(sim));
    }

    [Fact]
    public void Soldiers_eat_from_the_public_stores_before_their_familys()
    {
        var (sim, camp) = Village();
        GameSupport.Economy(sim);
        var home = GameSupport.Home(sim, 14, 10);
        GameSupport.SetAmount(home, "berries", 100);
        var unit = GameSupport.Adult(sim, 12, 12).LivesIn(home);
        Recruit(sim, "clubman", 1);
        GameSupport.Run(sim, 1);
        int before = GameSupport.Berries(camp);
        unit.GetComponent<Citizen>().Hunger = sim.World.Content.Citizens.EatAtHunger;
        GameSupport.Run(sim, 1);
        Assert.True(GameSupport.Berries(camp) < before);
        Assert.Equal(100, GameSupport.Berries(home));
    }

    [Fact]
    public void Disbanded_soldiers_go_back_to_civilian_life_with_their_health_in_proportion()
    {
        var (sim, camp) = Village();
        GameSupport.Learn(sim, "horsemanship");
        GameSupport.SetAmount(camp, "horses", 1);
        GameSupport.SetAmount(camp, "tools", 1);
        var unit = GameSupport.Adult(sim, 12, 12);
        Recruit(sim, "cavalry", 1);
        GameSupport.Run(sim, 2);
        var cavalry = sim.World.Content.Units[sim.World.Content.UnitIndex("cavalry")].Def;
        Assert.Equal(cavalry.Health, unit.GetComponent<Citizen>().Health);
        Assert.Equal(cavalry.TicksPerTile, unit.GetComponent<Mover>().TicksPerTile);
        unit.GetComponent<Citizen>().Health = cavalry.Health / 2;

        sim.Enqueue(new Disband([unit.Id]) { Player = Players.Human });
        GameSupport.Run(sim, 1);
        Assert.False(unit.HasComponent<Soldier>());
        Assert.InRange(unit.GetComponent<Citizen>().Health, sim.World.Content.Citizens.MaxHealth / 2, sim.World.Content.Citizens.MaxHealth / 2 + 5);
        Assert.Equal(sim.World.Content.Citizens.MoveTicksPerTile, unit.GetComponent<Mover>().TicksPerTile);
    }

    [Fact]
    public void Armed_soldiers_gather_at_the_rally_point()
    {
        var (sim, _) = Village();
        for (int i = 0; i < 3; i++) GameSupport.Adult(sim, 12, 12 + i);
        sim.Enqueue(new SetRallyPoint(25, 20) { Player = Players.Human });
        Recruit(sim, "clubman", 3);
        GameSupport.RunUntil(sim, () => Soldiers(sim).All(s => s.GetComponent<TilePosition>().DistanceTo(25, 20) <= 1
                                                               && !s.GetComponent<Mover>().Moving), 400);
        var spots = Soldiers(sim).Select(s => s.GetComponent<TilePosition>()).Distinct().Count();
        Assert.Equal(3, spots);
    }
}
