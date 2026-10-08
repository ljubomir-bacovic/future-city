using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Natural death. Once a year (on a tick staggered by id) each citizen past old age risks dying,
/// with the yearly chance growing for every year beyond it.
/// </summary>
public sealed class AgingSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Citizens;
        foreach (var unit in World.InIdOrder(world.Store.Query<Citizen, Owner>()))
        {
            if ((world.Tick + unit.Id) % world.Content.Calendar.TicksPerYear != 0) continue;
            int yearsPastOldAge = Bands.AgeInYears(world, unit.GetComponent<Citizen>()) - rules.OldAgeYears;
            if (yearsPastOldAge <= 0) continue;
            int chance = Math.Min(100, yearsPastOldAge * rules.OldAgeDeathPercentPerYear);
            if (!world.Rng.Chance(chance, 100)) continue;

            var pos = unit.GetComponent<TilePosition>();
            world.Emit(SimEventKind.DiedOfOldAge, unit.GetComponent<Owner>().Player, unit.Id, pos.X, pos.Y);
            unit.DeleteEntity();
        }
    }
}
