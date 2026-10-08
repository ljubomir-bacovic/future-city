using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

/// <summary>
/// The emergence engine. Every evaluation interval, for each civilization:
/// technologies whose preconditions hold gain progress from related work since the last evaluation (and from
/// research at shrines, if chosen as the focus), and may be discovered by chance, with better odds the more progress
/// there is; and the civilization enters the next era once that era's conditions hold. Institutions are not
/// automatic: the player establishes them with a command once their preconditions hold.
/// </summary>
public sealed class EmergenceSystem : ISimSystem
{
    /// <inheritdoc />
    public void Update(World world)
    {
        var rules = world.Content.Progress;
        if (world.Tick % rules.EvaluationIntervalTicks != 0) return;

        foreach (var entity in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            int player = entity.GetComponent<Owner>().Player;
            ref var civ = ref entity.GetComponent<Civilization>();
            var facts = Civics.FactsOf(world, player);
            var activity = new int[civ.Work.Length];
            for (int k = 0; k < activity.Length; k++)
            {
                activity[k] = civ.Work[k] - civ.LastWork[k];
                civ.LastWork[k] = civ.Work[k];
            }
            int focus = Focus(world, player, civ, facts);
            int research = activity[(int)WorkKind.Research];
            Bands.TryGetCamp(world, player, out var camp);
            var at = camp.IsNull ? default : camp.GetComponent<TilePosition>();

            for (int t = 0; t < world.Content.Techs.Count; t++)
            {
                var tech = world.Content.Techs[t];
                if (civ.Techs[t] != 0 || !tech.Preconditions.IsMet(facts)) continue;
                long gain = 0;
                for (int k = 0; k < activity.Length; k++)
                    gain += (long)activity[k] * tech.Activity[k];
                gain /= 100;
                if (t == focus) gain += research;
                int cost = tech.Def.Cost;
                int progress = (int)Math.Min(cost, civ.TechProgress[t] + gain);
                civ.TechProgress[t] = progress;
                int permille = (int)((long)rules.LuckPercent * progress * 10 / cost);
                bool discovered = progress >= cost || (permille > 0 && world.Rng.Chance(permille, 1000));
                if (!discovered) continue;
                civ.Techs[t] = 1;
                world.Emit(SimEventKind.TechDiscovered, player, entity.Id, at.X, at.Y, t);
            }

            // Facts share the civilization's arrays, so new discoveries already count here.
            int next = civ.Era + 1;
            if (next < world.Content.Eras.Count && world.Content.Eras[next].Preconditions.IsMet(Civics.FactsOf(world, player)))
            {
                civ.Era = next;
                world.Emit(SimEventKind.EraReached, player, entity.Id, at.X, at.Y, next);
            }
        }
    }

    // The chosen focus if it can still be discovered; otherwise the discoverable technology with the most progress.
    private static int Focus(World world, int player, in Civilization civ, Facts facts)
    {
        if (civ.ResearchFocus >= 0 && Civics.IsDiscoverable(world, player, civ.ResearchFocus, facts)) return civ.ResearchFocus;
        int best = -1;
        for (int t = 0; t < world.Content.Techs.Count; t++)
        {
            if (Civics.IsDiscoverable(world, player, t, facts) && (best < 0 || civ.TechProgress[t] > civ.TechProgress[best]))
                best = t;
        }
        return best;
    }
}
