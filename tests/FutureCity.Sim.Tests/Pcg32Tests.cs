using FutureCity.Sim.Random;

namespace FutureCity.Sim.Tests;

public class Pcg32Tests
{
    [Fact]
    public void Matches_reference_pcg32_output()
    {
        // Reference values from the PCG32 demo (pcg32-demo.c), seed 42, stream 54.
        var rng = new Pcg32(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
        foreach (uint value in expected)
            Assert.Equal(value, rng.NextUInt());
    }

    [Fact]
    public void Restored_state_continues_the_same_sequence()
    {
        var a = new Pcg32(7);
        for (int i = 0; i < 100; i++) a.NextUInt();
        var b = new Pcg32(a.State);
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        var a = new Pcg32(1);
        var b = new Pcg32(2);
        Assert.NotEqual(Enumerable.Range(0, 8).Select(_ => a.NextUInt()), Enumerable.Range(0, 8).Select(_ => b.NextUInt()));
    }

    [Fact]
    public void NextInt_stays_in_range_and_covers_it()
    {
        var rng = new Pcg32(3);
        var seen = new int[7];
        for (int i = 0; i < 7000; i++)
        {
            int v = rng.NextInt(-3, 4);
            Assert.InRange(v, -3, 3);
            seen[v + 3]++;
        }
        Assert.All(seen, count => Assert.InRange(count, 800, 1200));
    }

    [Fact]
    public void Chance_handles_edges_and_is_roughly_fair()
    {
        var rng = new Pcg32(5);
        Assert.False(rng.Chance(0, 10));
        Assert.True(rng.Chance(10, 10));
        int hits = Enumerable.Range(0, 10000).Count(_ => rng.Chance(1, 4));
        Assert.InRange(hits, 2300, 2700);
    }

    [Fact]
    public void Rejects_invalid_arguments()
    {
        var rng = new Pcg32(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        Assert.Throws<ArgumentException>(() => new Pcg32(new Pcg32State(1, 2)));
    }
}
