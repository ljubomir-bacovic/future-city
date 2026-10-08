using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>
/// The rules of fighting: who is an enemy, how much a hit does, and what happens to the dead and the destroyed. Used by
/// <see cref="Systems.CombatSystem"/>, commands and the UI.
/// </summary>
public static class Combat
{
    /// <summary>
    /// Whether <paramref name="target"/> is an enemy of <paramref name="player"/>: a person or building of a civilization
    /// at war with them, or a merchant caravan bound for such a civilization's market.
    /// </summary>
    public static bool IsEnemy(World world, int player, Entity target)
    {
        if (target.TryGetComponent<Merchant>(out var merchant)) return Relations.AtWar(world, player, merchant.Player);
        if (!target.TryGetComponent<Owner>(out var owner) || !Relations.AtWar(world, player, owner.Player)) return false;
        return target.HasComponent<Citizen>() || target.HasComponent<Building>();
    }

    /// <summary>Damage one hit of an attacker of <paramref name="kind"/> in <paramref name="service"/> does to <paramref name="target"/>.</summary>
    public static int Damage(World world, int kind, Service service, Entity target)
    {
        var unit = world.Content.Units[kind];
        int bonus, armour = 0;
        if (target.HasComponent<Building>())
        {
            bonus = unit.BuildingBonus;
        }
        else if (target.TryGetComponent<Soldier>(out var soldier))
        {
            bonus = unit.BonusVsUnit[soldier.Kind];
            armour = world.Content.Units[soldier.Kind].Def.Armour;
        }
        else
        {
            bonus = 100;
        }
        long attack = (long)unit.Def.Attack * Military.ServiceOf(world, service).AttackPercent / 100 * bonus / 100;
        return (int)Math.Max(world.Content.Military.Combat.MinDamage, attack - armour);
    }

    /// <summary>Damage a tower's shot does to a person.</summary>
    public static int TowerDamage(World world, DefenceDef defence, Entity target)
    {
        int armour = target.TryGetComponent<Soldier>(out var soldier) ? world.Content.Units[soldier.Kind].Def.Armour : 0;
        return Math.Max(world.Content.Military.Combat.MinDamage, defence.Attack - armour);
    }

    /// <summary>Hit points left in a building.</summary>
    public static int HitPointsLeft(World world, Entity building) =>
        Buildings.TypeOf(world, building).Def.HitPoints - building.GetComponent<Building>().Damage;

    /// <summary>
    /// A person killed by <paramref name="byPlayer"/>: counted as a death in battle (it weighs on the survivors'
    /// happiness), shakes the morale of their side's soldiers nearby, and drops what they carried.
    /// </summary>
    internal static void Kill(World world, Entity victim, int byPlayer)
    {
        int player = victim.GetComponent<Owner>().Player;
        var pos = victim.GetComponent<TilePosition>();
        var citizen = victim.GetComponent<Citizen>();
        if (Civics.TryGet(world, player, out var civEntity))
        {
            ref var civ = ref civEntity.GetComponent<Civilization>();
            civ.DeathsThisYear++;
            civ.BattleDeaths++;
        }
        world.Emit(SimEventKind.DiedInBattle, player, victim.Id, pos.X, pos.Y, byPlayer);
        if (citizen.Carried > 0)
        {
            var spilled = new int[world.Content.Goods.Count];
            spilled[citizen.CarriedGood] = citizen.Carried;
            Spill(world, pos.X, pos.Y, spilled);
        }
        var rules = world.Content.Military.Combat;
        foreach (var ally in World.InIdOrder(world.Store.Query<Soldier, Owner, TilePosition>()))
        {
            if (ally.Id == victim.Id || ally.GetComponent<Owner>().Player != player
                || ally.GetComponent<TilePosition>().DistanceTo(pos.X, pos.Y) > rules.AllyDeathRadius)
                continue;
            LoseMorale(world, ally, rules.AllyDeathMoraleLoss);
        }
        victim.DeleteEntity();
    }

    /// <summary>Lowers a soldier's morale; below the rout threshold they break and flee home.</summary>
    internal static void LoseMorale(World world, Entity unit, int amount)
    {
        ref var soldier = ref unit.GetComponent<Soldier>();
        soldier.Morale = Math.Max(0, soldier.Morale - amount);
        if (soldier.Morale >= world.Content.Military.Combat.RoutBelow || Military.IsRouted(world, soldier)) return;
        soldier.RoutUntil = world.Tick + world.Content.Military.Combat.RoutTicks;
        unit.GetComponent<Order>() = new Order { Public = true };
        var pos = unit.GetComponent<TilePosition>();
        world.Emit(SimEventKind.Routed, unit.GetComponent<Owner>().Player, unit.Id, pos.X, pos.Y);
    }

    /// <summary>
    /// A building destroyed by <paramref name="byPlayer"/>. Part of its goods is left in the ruins for looting, a family
    /// home's coins are plundered (the attacker's treasury takes the same share; the rest is lost in the fire), its
    /// family is homeless, and a marketplace takes the goods traders had there with it.
    /// </summary>
    internal static void Destroy(World world, Entity building, int byPlayer)
    {
        var rules = world.Content.Military.Combat;
        int player = building.GetComponent<Owner>().Player;
        var pos = building.GetComponent<TilePosition>();
        int kind = building.GetComponent<Building>().Kind;
        var stock = building.GetComponent<Inventory>().Amounts;
        var left = stock.Select(a => a * rules.LootSharePercent / 100).ToArray();
        int size = Buildings.SizeOf(world, building);
        Spill(world, pos.X + size / 2, pos.Y + size / 2, left);

        if (building.TryGetComponent<Trader>(out var household) && household.Coins > 0 && Civics.TryGet(world, byPlayer, out var raider))
            raider.GetComponent<Trader>().Coins += household.Coins * rules.LootSharePercent / 100;
        foreach (var unit in world.Store.Query<Citizen>().Entities)
        {
            if (unit.GetComponent<Citizen>().Home == building.Id) unit.GetComponent<Citizen>().Home = 0;
        }
        if (building.HasComponent<Market>())
        {
            foreach (var trader in world.Store.Query<Trader>().Entities)
            {
                bool here = trader.TryGetComponent<Merchant>(out var m)
                    ? m.Market == building.Id
                    : trader.TryGetComponent<Owner>(out var o) && o.Player == player;
                if (here) Array.Clear(trader.GetComponent<Trader>().AtMarket);
            }
        }

        if (Civics.TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().BuildingsLost++;
        world.Emit(SimEventKind.BuildingDestroyed, player, building.Id, pos.X, pos.Y, kind);
        bool wall = Buildings.TypeOf(world, building).Def.Wall && Buildings.IsComplete(building);
        building.DeleteEntity();
        if (wall) world.Map.SetWall(pos.X, pos.Y, 0, false);
    }

    /// <summary>
    /// A merchant caravan attacked on its way to (or at) an enemy's market: the merchants flee abroad with their coins, and
    /// their cargo, including what they had at the market, is left on the ground.
    /// </summary>
    internal static void Raid(World world, Entity caravan)
    {
        var merchant = caravan.GetComponent<Merchant>();
        var cargo = (int[])caravan.GetComponent<Inventory>().Amounts.Clone();
        var atMarket = caravan.GetComponent<Trader>().AtMarket;
        bool marketStands = world.TryGetEntity(merchant.Market, out var market) && market.HasComponent<Market>();
        for (int g = 0; g < atMarket.Length; g++)
        {
            if (atMarket[g] == 0) continue;
            cargo[g] += atMarket[g];
            if (marketStands) Markets.Withdraw(market, caravan, g, atMarket[g]);
        }
        var pos = caravan.GetComponent<TilePosition>();
        Spill(world, pos.X, pos.Y, cargo);
        world.Emit(SimEventKind.CaravanRaided, merchant.Player, caravan.Id, pos.X, pos.Y);
        caravan.DeleteEntity();
    }

    /// <summary>Leaves goods on the ground at (x, y) as a loot pile, unless there is nothing.</summary>
    internal static void Spill(World world, int x, int y, int[] amounts)
    {
        if (amounts.All(a => a == 0)) return;
        var pile = world.CreateEntity();
        pile.AddComponent(new TilePosition(x, y));
        pile.AddComponent(new Owner { Player = Players.Nature });
        pile.AddComponent(new LootPile { DecayTick = world.Tick + world.Content.Military.Combat.LootDecayTicks });
        pile.AddComponent(new Inventory { Amounts = amounts });
    }
}
