using FutureCity.Sim.Components;

namespace FutureCity.Sim.Commands;

/// <summary>
/// Declares war on another civilization: soldiers of both sides fight, raid and loot. Any alliance or trade agreement
/// between the two ends, and the target's allies join the war against the declarer.
/// </summary>
/// <param name="Target">The player to declare war on.</param>
public sealed record DeclareWar(int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world) => Relations.DeclareWar(world, Player, Target);
}

/// <summary>
/// Proposes an agreement to another civilization, which answers with <see cref="Respond"/>: peace (during a war), an
/// alliance, a trade agreement, or tribute paid every year (offered by the proposer, or demanded from the other side).
/// </summary>
/// <param name="Target">The player to propose it to.</param>
/// <param name="Kind">What is proposed.</param>
/// <param name="Good">For tribute: the good paid, or null for coins.</param>
/// <param name="Amount">For tribute: units (or coins) per year.</param>
public sealed record Propose(int Target, ProposalKind Kind, string? Good = null, int Amount = 0) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        int good = -1;
        if (Good != null)
        {
            good = world.Content.Goods.Select(g => g.Id).ToList().IndexOf(Good);
            if (good < 0) return;
        }
        Relations.Propose(world, Player, Target, Kind, good, Amount);
    }
}

/// <summary>Accepts or declines the proposal another civilization made.</summary>
/// <param name="From">The player who made the proposal.</param>
/// <param name="Accept">Whether to accept it.</param>
public sealed record Respond(int From, bool Accept) : Command
{
    /// <inheritdoc />
    public override void Execute(World world) => Relations.Respond(world, Player, From, Accept);
}

/// <summary>
/// Ends an agreement with another civilization: an alliance (back to peace), a trade agreement, or the tribute the
/// player pays them (<see cref="ProposalKind.OfferTribute"/>).
/// </summary>
/// <param name="Other">The other player.</param>
/// <param name="Kind">Which agreement.</param>
public sealed record BreakAgreement(int Other, ProposalKind Kind) : Command
{
    /// <inheritdoc />
    public override void Execute(World world) => Relations.Break(world, Player, Other, Kind);
}
