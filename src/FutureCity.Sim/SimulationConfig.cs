using FutureCity.Sim.Commands;
using FutureCity.Sim.Systems;

namespace FutureCity.Sim;

/// <summary>
/// The rules a simulation runs with: known commands and the ordered system list.
/// A save must be loaded with the same configuration it was created with.
/// </summary>
public sealed class SimulationConfig
{
    /// <summary>Creates a configuration.</summary>
    public SimulationConfig(CommandRegistry commands, IReadOnlyList<ISimSystem> systems)
    {
        Commands = commands;
        Systems = systems;
    }

    /// <summary>Registered command types.</summary>
    public CommandRegistry Commands { get; }

    /// <summary>Systems in the order they run each tick.</summary>
    public IReadOnlyList<ISimSystem> Systems { get; }

    /// <summary>The standard game configuration.</summary>
    public static SimulationConfig CreateDefault() =>
        new(CommandRegistry.CreateDefault(), DefaultSystems());

    private static ISimSystem[] DefaultSystems() =>
    [
        // Execution order matters: idle people find jobs, orders decide where people go, movement moves them,
        // then needs, soldiers' pay and morale, and life events, families, nature, the market and merchants, happiness, and finally the
        // emergence engine looks at the resulting society.
        new JobAssignmentSystem(),
        new OrderSystem(),
        new MovementSystem(),
        new NeedsSystem(),
        new SoldierSystem(),
        new AgingSystem(),
        new PopulationSystem(),
        new HouseholdSystem(),
        new FieldSystem(),
        new PlantGrowthSystem(),
        new AnimalSystem(),
        new CarcassSystem(),
        new MarketSystem(),
        new MerchantSystem(),
        new HappinessSystem(),
        new EmergenceSystem(),
    ];
}
