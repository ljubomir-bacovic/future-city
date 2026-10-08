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
