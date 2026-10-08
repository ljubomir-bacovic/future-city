using FutureCity.Sim.Random;

namespace FutureCity.Sim.Map;

/// <summary>Smooth 2D value noise in integer math (two octaves), for terrain features.</summary>
internal static class ValueNoise
{
    private const int One = 1024; // fixed-point 1.0

    /// <summary>Returns a row-major field of values in [0, 3 × 1024) with features about <paramref name="cellSize"/> tiles across.</summary>
    public static int[] Generate(int width, int height, int cellSize, Pcg32 rng)
    {
        var field = new int[width * height];
        AddOctave(field, width, height, cellSize, 2, rng);
        AddOctave(field, width, height, Math.Max(2, cellSize / 2), 1, rng);
        return field;
    }

    /// <summary>Returns the value below which <paramref name="percent"/>% of <paramref name="values"/> lie.</summary>
    public static int Percentile(IEnumerable<int> values, int percent)
    {
        var sorted = values.ToArray();
        if (sorted.Length == 0) return 0;
        Array.Sort(sorted);
        int index = (int)Math.Clamp((long)sorted.Length * percent / 100, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static void AddOctave(int[] field, int width, int height, int cell, int weight, Pcg32 rng)
    {
        int gridWidth = width / cell + 2, gridHeight = height / cell + 2;
        var lattice = new int[gridWidth * gridHeight];
        for (int i = 0; i < lattice.Length; i++)
            lattice[i] = rng.NextInt(One);

        for (int y = 0; y < height; y++)
        {
            int gy = y / cell, ty = Smooth((y % cell) * One / cell);
            for (int x = 0; x < width; x++)
            {
                int gx = x / cell, tx = Smooth((x % cell) * One / cell);
                long a = lattice[gy * gridWidth + gx], b = lattice[gy * gridWidth + gx + 1];
                long c = lattice[(gy + 1) * gridWidth + gx], d = lattice[(gy + 1) * gridWidth + gx + 1];
                long top = a * (One - tx) + b * tx;
                long bottom = c * (One - tx) + d * tx;
                field[y * width + x] += (int)((top * (One - ty) + bottom * ty) / ((long)One * One)) * weight;
            }
        }
    }

    // Smoothstep 3t² − 2t³ in fixed point, so features have soft edges instead of grid lines.
    private static int Smooth(int t) => (int)((long)t * t * (3 * One - 2 * t) / ((long)One * One));
}
