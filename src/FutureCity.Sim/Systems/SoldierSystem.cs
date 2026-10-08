using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// The upkeep of soldiers, every job check. Paid soldiers draw a wage from the treasury, which goes to their families; a
/// soldier the treasury cannot pay loses heart and, unpaid long enough, goes home for good. Hungry soldiers lose
/// morale too. Out of combat, morale slowly recovers. (Soldiers eat from the public stores: see <see cref="NeedsSystem"/>.)
/// </summary>
public sealed class SoldierSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        int interval = world.Content.Citizens.Jobs.CheckIntervalTicks;
        if (world.Tick % interval != 0) return;
        var combat = world.Content.Military.Combat;
        var citizens = world.Content.Citizens;
        int hungry = (citizens.EatAtHunger + citizens.MaxHunger) / 2;
        foreach (var unit in World.InIdOrder(world.Store.Query<Soldier, Citizen, Owner>()))
        {
            int player = unit.GetComponent<Owner>().Player;
            ref var soldier = ref unit.GetComponent<Soldier>();
            int morale = soldier.Morale;
            if (soldier.Service == Service.Paid)
            {
                if (Pay(world, unit, player))
                {
                    soldier.UnpaidChecks = 0;
                }
                else
                {
                    soldier.UnpaidChecks++;
                    morale -= combat.UnpaidMoraleLoss;
                }
            }
            if (unit.GetComponent<Citizen>().Hunger >= hungry) morale -= combat.HungryMoraleLoss;
            else if (world.Tick - soldier.LastCombatTick > combat.RoutTicks)
                morale = Math.Min(Military.BaseMorale(world, soldier.Kind, soldier.Service), morale + combat.MoraleRecoveryPerCheck);
            if (morale >= soldier.Morale) soldier.Morale = Math.Min(100, morale);
            else Combat.LoseMorale(world, unit, soldier.Morale - morale); // hunger and want of pay can break them too

            if (soldier.UnpaidChecks >= combat.DesertAfterUnpaidChecks)
            {
                var pos = unit.GetComponent<TilePosition>();
                world.Emit(SimEventKind.Deserted, player, unit.Id, pos.X, pos.Y);
                Military.Discharge(world, unit);
            }
        }
    }

    // Pays one check's wage from the treasury to the soldier's family. Without a family there is no one to keep it,
    // so the soldier serves for their keep; without coins in the treasury the soldier goes unpaid.
    private static bool Pay(World world, Friflo.Engine.ECS.Entity unit, int player)
    {
        if (!Civics.TryGet(world, player, out var civEntity)) return false;
        var civ = civEntity.GetComponent<Civilization>();
        int wage = civ.PublicWage * world.Content.Military.Service.Paid.WagePercent / 100
                   * world.Content.Citizens.Jobs.CheckIntervalTicks / 100;
        if (wage <= 0 || !Households.TryGetHome(world, unit, out var home)) return true;
        ref var treasury = ref civEntity.GetComponent<Trader>();
        if (treasury.Coins < wage) return false;
        treasury.Coins -= wage;
        home.GetComponent<Trader>().Coins += wage;
        Economy.Record(world, player, LedgerEntry.Soldiers, wage);
        return true;
    }
}
