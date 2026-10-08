using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim;

/// <summary>Diplomatic relations between civilizations: war, peace, alliances, trade agreements and tribute.</summary>
public static class Relations
{
    /// <summary>A fresh diplomacy component: at peace with everyone, no agreements.</summary>
    internal static Diplomacy NewDiplomacy()
    {
        int n = GameSetup.MaxCivilizations + 1;
        var good = new int[n];
        Array.Fill(good, -1);
        return new Diplomacy
        {
            Relation = new int[n], TradeAgreement = new int[n], TributeGood = good, TributeAmount = new int[n],
            Proposal = new int[n], ProposalGood = (int[])good.Clone(), ProposalAmount = new int[n],
        };
    }

    private static bool IsCivPlayer(int player) => player > Players.Nature && player <= GameSetup.MaxCivilizations;

    /// <summary>How <paramref name="a"/> stands toward <paramref name="b"/> (peace for nature and unknown players).</summary>
    public static Relation Between(World world, int a, int b)
    {
        if (a == b || !IsCivPlayer(a) || !IsCivPlayer(b) || !Civics.TryGet(world, a, out var civ)
            || !civ.TryGetComponent<Diplomacy>(out var diplomacy))
            return Relation.Peace;
        return (Relation)diplomacy.Relation[b];
    }

    /// <summary>Whether the two players are at war.</summary>
    public static bool AtWar(World world, int a, int b) => Between(world, a, b) == Relation.War;

    /// <summary>Whether the player is at war with anyone.</summary>
    public static bool AtWarWithAnyone(World world, int player)
    {
        for (int other = 1; other <= GameSetup.MaxCivilizations; other++)
        {
            if (AtWar(world, player, other)) return true;
        }
        return false;
    }

    /// <summary>Whether any two civilizations are at war (combat can only happen then).</summary>
    public static bool AnyWar(World world)
    {
        foreach (var civ in world.Store.Query<Diplomacy>().Entities)
        {
            if (civ.GetComponent<Diplomacy>().Relation.Contains((int)Relation.War)) return true;
        }
        return false;
    }

    /// <summary>
    /// Declares war: <paramref name="declarer"/> and <paramref name="target"/> are at war, any alliance or trade agreement
    /// between them ends, and the target's allies join the war against the declarer. Returns false if nothing changed.
    /// </summary>
    internal static bool DeclareWar(World world, int declarer, int target)
    {
        if (declarer == target || !IsCivPlayer(declarer) || !IsCivPlayer(target)
            || !Civics.TryGet(world, declarer, out _) || !Civics.TryGet(world, target, out _) || AtWar(world, declarer, target))
            return false;
        Set(world, declarer, target, Relation.War);
        world.Emit(SimEventKind.WarDeclared, declarer, 0, 0, 0, target);
        for (int ally = 1; ally <= GameSetup.MaxCivilizations; ally++)
        {
            if (ally == declarer || ally == target || Between(world, target, ally) != Relation.Alliance) continue;
            if (AtWar(world, ally, declarer)) continue;
            Set(world, ally, declarer, Relation.War); // called to the defence of their ally
            world.Emit(SimEventKind.WarDeclared, ally, 0, 0, 0, declarer);
        }
        return true;
    }

    /// <summary>Sets the relation on both sides. War and peace end trade agreements and tribute between them; war also ends pending proposals.</summary>
    internal static void Set(World world, int a, int b, Relation relation)
    {
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            if (!Civics.TryGet(world, self, out var civ)) continue;
            if (!civ.HasComponent<Diplomacy>()) civ.AddComponent(NewDiplomacy());
            ref var d = ref civ.GetComponent<Diplomacy>();
            d.Relation[other] = (int)relation;
            if (relation == Relation.War)
            {
                d.TradeAgreement[other] = 0;
                d.TributeAmount[other] = 0;
                d.Proposal[other] = (int)ProposalKind.None;
            }
        }
    }
}
