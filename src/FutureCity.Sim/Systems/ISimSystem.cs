namespace FutureCity.Sim.Systems;

/// <summary>
/// One step of the simulation rules, run once per tick in a fixed order.
/// Systems must be stateless: all state lives in the <see cref="World"/> so it is saved and hashed.
/// </summary>
public interface ISimSystem
{
    /// <summary>Advances this system's part of the world by one tick.</summary>
    void Update(World world);
}
