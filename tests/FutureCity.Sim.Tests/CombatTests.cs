using Friflo.Engine.ECS;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;

namespace FutureCity.Sim.Tests;

/// <summary>Attack, damage, range, armour, morale, destruction and towers.</summary>
public class CombatTests
{
    private static Simulation Field()
    {
        var sim = GameSupport.Plain();
        GameSupport.Camp(sim, 5, 5, food: 5000, player: 1);
        GameSupport.Camp(sim, 50, 50, food: 5000, player: 2);
        GameSupport.CivOf(sim, 1);
        GameSupport.CivOf(sim, 2);
        return sim;
    }

    private static int Unit(Simulation sim, string id) => sim.World.Content.UnitIndex(id);

    [Fact]
    public void Damage_is_attack_with_bonuses_minus_armour_and_never_below_the_minimum()
    {
        var sim = Field();
        var content = sim.World.Content;
        var spearman = GameSupport.Soldier(sim, "spearman", 20, 20, player: 2);
        var rider = GameSupport.Soldier(sim, "cavalry", 21, 20, player: 2);
        var civilian = GameSupport.Adult(sim, 22, 20, player: 2);
        var hut = GameSupport.Building(sim, "hut", 24, 20, player: 2);
        var club = content.Units[Unit(sim, "clubman")].Def;
        var spear = content.Units[Unit(sim, "spearman")].Def;

        Assert.Equal(club.Attack - spear.Armour, Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Levy, spearman));
        Assert.Equal(club.Attack, Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Levy, civilian));
        // Spears are made for horses; paid troops hit harder.
        Assert.Equal(spear.Attack * 2 - content.Units[Unit(sim, "cavalry")].Def.Armour,
            Combat.Damage(sim.World, Unit(sim, "spearman"), Service.Levy, rider));
        Assert.Equal(club.Attack * 120 / 100, Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Paid, civilian));
        // Clubs barely scratch buildings; rams smash them; a ram's armour shrugs off a club.
        Assert.Equal(club.Attack * content.Military.Combat.BuildingDamagePercent / 100,
            Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Levy, hut));
        Assert.True(Combat.Damage(sim.World, Unit(sim, "ram"), Service.Levy, hut)
                    >= 20 * Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Levy, hut));
        var ram = GameSupport.Soldier(sim, "ram", 26, 20, player: 2);
        Assert.Equal(content.Military.Combat.MinDamage, Combat.Damage(sim.World, Unit(sim, "clubman"), Service.Levy, ram));
    }

    [Fact]
    public void Soldiers_leave_each_other_alone_at_peace_and_fight_at_war()
    {
        var sim = Field();
        var a = GameSupport.Soldier(sim, "clubman", 20, 20, player: 1);
        var b = GameSupport.Soldier(sim, "clubman", 22, 20, player: 2);
        GameSupport.Run(sim, 200);
        Assert.Equal(1000, a.GetComponent<Citizen>().Health);
        Assert.Equal(1000, b.GetComponent<Citizen>().Health);

        sim.Enqueue(new DeclareWar(2) { Player = 1 });
        var events = GameSupport.Run(sim, 600);
        Assert.Contains(events, e => e.Kind == SimEventKind.WarDeclared && e.Player == 1 && e.Detail == 2);
        Assert.Contains(events, e => e.Kind == SimEventKind.Attacked);
        // One of them falls or breaks and runs.
        Assert.Contains(events, e => e.Kind is SimEventKind.DiedInBattle or SimEventKind.Routed);
        int deaths = GameSupport.CivOf(sim, 1).GetComponent<Civilization>().BattleDeaths
                     + GameSupport.CivOf(sim, 2).GetComponent<Civilization>().BattleDeaths;
        Assert.Equal(events.Count(e => e.Kind == SimEventKind.DiedInBattle), deaths);
    }

    [Fact]
    public void A_fight_to_the_death_counts_the_loss_and_weighs_on_the_survivors()
    {
        var sim = Field();
        var strong = GameSupport.Soldier(sim, "cavalry", 20, 20, player: 1);
        var weak = GameSupport.Adult(sim, 21, 20, player: 2); // a civilian caught in the open
        GameSupport.War(sim);
        sim.Enqueue(new Attack([strong.Id], weak.Id) { Player = 1 });
        var events = GameSupport.Run(sim, 400);
        var death = Assert.Single(events, e => e.Kind == SimEventKind.DiedInBattle);
        Assert.Equal(2, death.Player);
        Assert.Equal(1, death.Detail);
        var civ = GameSupport.CivOf(sim, 2).GetComponent<Civilization>();
        Assert.Equal(1, civ.BattleDeaths);
        Assert.Equal(1, civ.DeathsThisYear); // counts toward the safety penalty in happiness
    }

    [Fact]
    public void Archers_shoot_from_their_range_and_close_in_only_that_far()
    {
        var sim = Field();
        GameSupport.Learn(sim, "archery");
        var archer = GameSupport.Soldier(sim, "archer", 20, 20, player: 1);
        var hut = GameSupport.Building(sim, "hut", 30, 20, player: 2);
        GameSupport.War(sim);
        sim.Enqueue(new Attack([archer.Id], hut.Id) { Player = 1 });
        GameSupport.RunUntil(sim, () => sim.World.Events.Any(e => e.Kind == SimEventKind.Attacked), 200);
        int range = sim.World.Content.Units[Unit(sim, "archer")].Def.Range;
        Assert.Equal(range, Buildings.DistanceTo(sim.World, hut, archer.GetComponent<TilePosition>().X, archer.GetComponent<TilePosition>().Y));
        Assert.True(hut.GetComponent<Building>().Damage > 0);
    }

    [Fact]
    public void A_soldier_whose_morale_breaks_flees_home_then_comes_back_to_their_senses()
    {
        var sim = Field();
        var rules = sim.World.Content.Military.Combat;
        var coward = GameSupport.Soldier(sim, "clubman", 30, 30, player: 1);
        var foe = GameSupport.Soldier(sim, "spearman", 31, 30, player: 2);
        coward.GetComponent<Soldier>().Morale = rules.RoutBelow + 1;
        GameSupport.War(sim);
        sim.Enqueue(new Attack([foe.Id], coward.Id) { Player = 2 });
        var events = GameSupport.Run(sim, 40);
        Assert.Contains(events, e => e.Kind == SimEventKind.Routed && e.Entity == coward.Id);
        Assert.True(Military.IsRouted(sim.World, coward.GetComponent<Soldier>()));
        GameSupport.RunUntil(sim, () => coward.IsNull || !Military.IsRouted(sim.World, coward.GetComponent<Soldier>()), rules.RoutTicks + 1);
        if (!coward.IsNull)
            Assert.True(coward.GetComponent<TilePosition>().DistanceTo(30, 30) > 5, "they ran toward their camp");
    }

    [Fact]
    public void A_destroyed_home_leaves_ruins_to_loot_and_a_homeless_family()
    {
        var sim = Field();
        var home = GameSupport.Home(sim, 30, 30);
        home.GetComponent<Owner>().Player = 2;
        GameSupport.SetAmount(home, "wood", 40);
        GameSupport.TraderOf(home).Coins = 100;
        var family = GameSupport.Adult(sim, 45, 45, player: 2).LivesIn(home);
        var ram = GameSupport.Soldier(sim, "ram", 28, 30, player: 1);
        GameSupport.War(sim);
        sim.Enqueue(new Attack([ram.Id], home.Id) { Player = 1 });
        var events = GameSupport.Run(sim, 300);
        Assert.Contains(events, e => e.Kind == SimEventKind.BuildingDestroyed && e.Player == 2);
        Assert.Equal(0, family.GetComponent<Citizen>().Home);
        var pile = Assert.Single(World.InIdOrder(sim.World.Store.Query<LootPile>()));
        int share = sim.World.Content.Military.Combat.LootSharePercent;
        Assert.Equal(40 * share / 100, GameSupport.Amount(pile, "wood"));
        Assert.Equal(100 * share / 100, GameSupport.TraderOf(GameSupport.CivOf(sim, 1)).Coins);
        Assert.Equal(1, GameSupport.CivOf(sim, 2).GetComponent<Civilization>().BuildingsLost);
    }

    [Fact]
    public void Rams_attack_only_buildings()
    {
        var sim = Field();
        var ram = GameSupport.Soldier(sim, "ram", 30, 30, player: 1);
        var foe = GameSupport.Soldier(sim, "clubman", 31, 30, player: 2);
        foe.GetComponent<Order>() = new Order { Kind = OrderKind.Move, Public = true }; // stands still, does not fight back
        GameSupport.War(sim);
        sim.Enqueue(new Attack([ram.Id], foe.Id) { Player = 1 });
        var events = GameSupport.Run(sim, 100);
        Assert.DoesNotContain(events, e => e.Kind == SimEventKind.Attacked && e.Entity == ram.Id);
    }

    [Fact]
    public void Towers_shoot_enemies_in_range()
    {
        var sim = Field();
        var tower = GameSupport.Building(sim, "tower", 30, 30, player: 1);
        var foe = GameSupport.Adult(sim, 34, 30, player: 2);
        foe.GetComponent<Order>() = new Order { Kind = OrderKind.Move };
        GameSupport.War(sim);
        var events = GameSupport.Run(sim, 30);
        Assert.Contains(events, e => e.Kind == SimEventKind.Attacked && e.Entity == tower.Id && e.Detail == foe.Id);
        Assert.True(foe.IsNull || foe.GetComponent<Citizen>().Health < 1000);
    }

    [Fact]
    public void Builders_repair_damaged_buildings()
    {
        var sim = Field();
        var hut = GameSupport.Building(sim, "hut", 12, 12, player: 1);
        hut.GetComponent<Building>().Damage = 300;
        var builder = GameSupport.Adult(sim, 10, 12, player: 1);
        sim.Enqueue(new Build([builder.Id], hut.Id) { Player = 1 });
        GameSupport.RunUntil(sim, () => hut.GetComponent<Building>().Damage == 0, 400);
        GameSupport.Run(sim, 2);
        Assert.Equal(OrderKind.Idle, builder.GetComponent<Order>().Kind);
    }
}
