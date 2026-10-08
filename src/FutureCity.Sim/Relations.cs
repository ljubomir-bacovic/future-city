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

    /// <summary>Whether the two players have a trade agreement.</summary>
    public static bool HaveTradeAgreement(World world, int a, int b) =>
        a != b && IsCivPlayer(b) && Civics.TryGet(world, a, out var civ) && civ.TryGetComponent<Diplomacy>(out var d)
        && d.TradeAgreement[b] != 0;

    /// <summary>The tribute <paramref name="payer"/> pays <paramref name="receiver"/> every year: good (-1 = coins) and amount (0 = none).</summary>
    public static (int Good, int Amount) Tribute(World world, int payer, int receiver)
    {
        if (!IsCivPlayer(receiver) || !Civics.TryGet(world, payer, out var civ) || !civ.TryGetComponent<Diplomacy>(out var d))
            return (-1, 0);
        return (d.TributeGood[receiver], d.TributeAmount[receiver]);
    }

    /// <summary>The proposal <paramref name="from"/> has made to <paramref name="player"/> and that awaits an answer.</summary>
    public static (ProposalKind Kind, int Good, int Amount) Pending(World world, int player, int from)
    {
        if (!IsCivPlayer(from) || !Civics.TryGet(world, player, out var civ) || !civ.TryGetComponent<Diplomacy>(out var d))
            return (ProposalKind.None, -1, 0);
        return ((ProposalKind)d.Proposal[from], d.ProposalGood[from], d.ProposalAmount[from]);
    }

    /// <summary>Whether <paramref name="from"/> may propose <paramref name="kind"/> to <paramref name="to"/> as things stand.</summary>
    public static bool CanPropose(World world, int from, int to, ProposalKind kind, int good, int amount)
    {
        if (from == to || !IsCivPlayer(from) || !IsCivPlayer(to) || !Civics.TryGet(world, from, out _) || !Civics.TryGet(world, to, out _))
            return false;
        var relation = Between(world, from, to);
        return kind switch
        {
            ProposalKind.Peace => relation == Relation.War,
            ProposalKind.Alliance => relation == Relation.Peace,
            ProposalKind.TradeAgreement => relation != Relation.War && !HaveTradeAgreement(world, from, to),
            ProposalKind.OfferTribute or ProposalKind.DemandTribute =>
                amount > 0 && amount <= 1_000_000 && good >= -1 && good < world.Content.Goods.Count,
            _ => false,
        };
    }

    /// <summary>Records a proposal for <paramref name="to"/> to answer (replacing any earlier one from the same player).</summary>
    internal static bool Propose(World world, int from, int to, ProposalKind kind, int good, int amount)
    {
        if (!CanPropose(world, from, to, kind, good, amount)) return false;
        Civics.TryGet(world, to, out var civ);
        ref var d = ref civ.GetComponent<Diplomacy>();
        bool tribute = kind is ProposalKind.OfferTribute or ProposalKind.DemandTribute;
        d.Proposal[from] = (int)kind;
        d.ProposalGood[from] = tribute ? good : -1;
        d.ProposalAmount[from] = tribute ? amount : 0;
        world.Emit(SimEventKind.ProposalReceived, to, 0, 0, 0, from);
        return true;
    }

    /// <summary>
    /// Answers the pending proposal <paramref name="from"/> made to <paramref name="player"/>. Accepted, it takes effect if
    /// it still makes sense: peace ends the war, an alliance or trade agreement begins, or tribute starts to be paid.
    /// </summary>
    internal static bool Respond(World world, int player, int from, bool accept)
    {
        var (kind, good, amount) = Pending(world, player, from);
        if (kind == ProposalKind.None) return false;
        Civics.TryGet(world, player, out var civ);
        civ.GetComponent<Diplomacy>().Proposal[from] = (int)ProposalKind.None;
        if (!accept || !CanPropose(world, from, player, kind, good, amount))
        {
            world.Emit(SimEventKind.ProposalDeclined, from, 0, 0, 0, player);
            return true;
        }
        switch (kind)
        {
            case ProposalKind.Peace:
                Set(world, from, player, Relation.Peace);
                break;
            case ProposalKind.Alliance:
                Set(world, from, player, Relation.Alliance);
                break;
            case ProposalKind.TradeAgreement:
                SetTradeAgreement(world, from, player, true);
                break;
            case ProposalKind.OfferTribute:
                SetTribute(world, from, player, good, amount);
                break;
            case ProposalKind.DemandTribute:
                SetTribute(world, player, from, good, amount);
                break;
        }
        world.Emit(SimEventKind.ProposalAccepted, from, 0, 0, 0, player);
        return true;
    }

    /// <summary>
    /// Ends an agreement with <paramref name="other"/>: an alliance (back to peace), a trade agreement, or the tribute
    /// <paramref name="player"/> pays them (<see cref="ProposalKind.OfferTribute"/>).
    /// </summary>
    internal static bool Break(World world, int player, int other, ProposalKind kind)
    {
        switch (kind)
        {
            case ProposalKind.Alliance when Between(world, player, other) == Relation.Alliance:
                Set(world, player, other, Relation.Peace);
                return true;
            case ProposalKind.TradeAgreement when HaveTradeAgreement(world, player, other):
                SetTradeAgreement(world, player, other, false);
                return true;
            case ProposalKind.OfferTribute when Tribute(world, player, other).Amount > 0:
                SetTribute(world, player, other, -1, 0);
                return true;
            default:
                return false;
        }
    }

    private static void SetTradeAgreement(World world, int a, int b, bool on)
    {
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            if (Civics.TryGet(world, self, out var civ)) civ.GetComponent<Diplomacy>().TradeAgreement[other] = on ? 1 : 0;
        }
    }

    internal static void SetTribute(World world, int payer, int receiver, int good, int amount)
    {
        if (!Civics.TryGet(world, payer, out var civ)) return;
        ref var d = ref civ.GetComponent<Diplomacy>();
        d.TributeGood[receiver] = amount > 0 ? good : -1;
        d.TributeAmount[receiver] = amount;
    }
}
