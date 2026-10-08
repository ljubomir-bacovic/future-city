namespace FutureCity.Sim.Random;

/// <summary>
/// Saved state of a <see cref="Pcg32"/> generator.
/// </summary>
/// <param name="State">Internal 64-bit state.</param>
/// <param name="Increment">Stream selector (always odd).</param>
public readonly record struct Pcg32State(ulong State, ulong Increment);

/// <summary>
/// Deterministic PCG32 (XSH RR) random number generator. Integer-only, identical on every platform.
/// This is the only source of randomness allowed in the simulation.
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private ulong _increment;

    /// <summary>Creates a generator from a seed and a stream id (different streams give independent sequences).</summary>
    public Pcg32(ulong seed, ulong stream = 0)
    {
        _state = 0;
        _increment = (stream << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    /// <summary>Restores a generator from saved state.</summary>
    public Pcg32(Pcg32State state)
    {
        if ((state.Increment & 1UL) == 0)
            throw new ArgumentException("PCG32 increment must be odd.", nameof(state));
        _state = state.State;
        _increment = state.Increment;
    }

    /// <summary>The current state, for saving.</summary>
    public Pcg32State State => new(_state, _increment);

    /// <summary>Returns a uniformly distributed 32-bit value.</summary>
    public uint NextUInt()
    {
        ulong old = _state;
        _state = unchecked(old * Multiplier + _increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rotation = (int)(old >> 59);
        return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
    }

    /// <summary>Returns a uniformly distributed value in [0, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be positive.");
        uint bound = (uint)maxExclusive;
        // Lemire's multiply-and-reject method.
        ulong product = (ulong)NextUInt() * bound;
        uint low = (uint)product;
        if (low < bound)
        {
            uint threshold = unchecked(0u - bound) % bound;
            while (low < threshold)
            {
                product = (ulong)NextUInt() * bound;
                low = (uint)product;
            }
        }
        return (int)(product >> 32);
    }

    /// <summary>Returns a uniformly distributed value in [<paramref name="min"/>, <paramref name="maxExclusive"/>).</summary>
    public int NextInt(int min, int maxExclusive)
    {
        if (maxExclusive <= min)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be greater than lower bound.");
        return min + NextInt(maxExclusive - min);
    }

    /// <summary>Returns true with probability <paramref name="numerator"/> / <paramref name="denominator"/>.</summary>
    public bool Chance(int numerator, int denominator)
    {
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        if (numerator <= 0) return false;
        if (numerator >= denominator) return true;
        return NextInt(denominator) < numerator;
    }
}
