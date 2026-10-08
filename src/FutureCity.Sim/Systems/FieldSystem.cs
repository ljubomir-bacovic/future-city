using FutureCity.Sim.Components;

namespace FutureCity.Sim.Systems;

/// <summary>
/// Crops and soil. A sown field grows at the season's pace times its soil fertility and ripens when fully grown;
/// a ripe crop left in the field when the rotting season comes is lost. Fallow fields slowly regain fertility
/// up to their natural level, while every harvest takes some away (soil exhaustion).
/// </summary>
public sealed class FieldSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var season = Calendar.Season(world);
        foreach (var farm in World.InIdOrder(world.Store.Query<Field, Building, Owner, TilePosition>()))
        {
            if (!Buildings.IsComplete(farm)) continue;
            var def = Buildings.TypeOf(world, farm).Def.Field!;
            ref var field = ref farm.GetComponent<Field>();
            switch (field.Stage)
            {
                case FieldStage.Growing:
                    field.Progress += season.CropGrowthPercent * field.Fertility / 100;
                    if (field.Progress < def.GrowTicks * Labor.PerTick) break;
                    field.Stage = FieldStage.Ripe;
                    field.Progress = 0;
                    field.Remaining = Math.Max(1, def.Yield * field.Fertility / 100);
                    break;
                case FieldStage.Ripe when season.CropsRot:
                    field.Stage = FieldStage.Fallow;
                    field.Remaining = 0;
                    var pos = farm.GetComponent<TilePosition>();
                    world.Emit(SimEventKind.CropsRotted, farm.GetComponent<Owner>().Player, farm.Id, pos.X, pos.Y);
                    break;
                case FieldStage.Fallow:
                    if (field.Fertility < field.NaturalFertility && (world.Tick + farm.Id) % def.FertilityRecoveryIntervalTicks == 0)
                        field.Fertility++;
                    break;
            }
        }
    }
}
