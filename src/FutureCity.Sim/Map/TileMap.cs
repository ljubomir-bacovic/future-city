namespace FutureCity.Sim.Map;

/// <summary>
/// The terrain grid. Each tile stores an index into <see cref="Content.ContentDatabase.Terrains"/>, the amount of
/// its terrain resource left (e.g. wood in a forest) and its soil fertility.
/// Tile (0, 0) is the top corner of the isometric view; x grows down-right, y grows down-left.
/// </summary>
public sealed class TileMap
{
    private readonly byte[] _terrain;
    private readonly ushort[] _resource;
    private readonly byte[] _fertility;

    /// <summary>Creates a map filled with one terrain, no resources and zero fertility.</summary>
    public TileMap(int width, int height, byte fillTerrain)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _terrain = new byte[width * height];
        Array.Fill(_terrain, fillTerrain);
        _resource = new ushort[width * height];
        _fertility = new byte[width * height];
    }

    internal TileMap(int width, int height, byte[] terrain, ushort[] resource, byte[] fertility)
    {
        if (terrain.Length != width * height || resource.Length != width * height || fertility.Length != width * height)
            throw new ArgumentException("Map layers do not match the map size.");
        Width = width;
        Height = height;
        _terrain = terrain;
        _resource = resource;
        _fertility = fertility;
    }

    /// <summary>Width in tiles.</summary>
    public int Width { get; }

    /// <summary>Height in tiles.</summary>
    public int Height { get; }

    /// <summary>Whether (x, y) is on the map.</summary>
    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Terrain index at (x, y).</summary>
    public int GetTerrain(int x, int y) => _terrain[Index(x, y)];

    /// <summary>Sets the terrain index at (x, y).</summary>
    public void SetTerrain(int x, int y, int terrain)
    {
        _terrain[Index(x, y)] = checked((byte)terrain);
        Version++;
    }

    /// <summary>Units of the terrain's resource left at (x, y).</summary>
    public int GetResource(int x, int y) => _resource[Index(x, y)];

    /// <summary>Sets the resource left at (x, y).</summary>
    public void SetResource(int x, int y, int amount) => _resource[Index(x, y)] = checked((ushort)amount);

    /// <summary>Soil fertility at (x, y), in percent.</summary>
    public int GetFertility(int x, int y) => _fertility[Index(x, y)];

    /// <summary>Sets the soil fertility at (x, y).</summary>
    public void SetFertility(int x, int y, int fertility) => _fertility[Index(x, y)] = checked((byte)fertility);

    /// <summary>Counts terrain changes, so caches derived from the map know when to rebuild. Not saved.</summary>
    public int Version { get; private set; }

    /// <summary>Raw row-major terrain data, for saving and rendering.</summary>
    public ReadOnlySpan<byte> RawTerrain => _terrain;

    /// <summary>Raw row-major resource amounts, for saving.</summary>
    public ReadOnlySpan<ushort> RawResource => _resource;

    /// <summary>Raw row-major fertility, for saving and overlays.</summary>
    public ReadOnlySpan<byte> RawFertility => _fertility;

    private int Index(int x, int y)
    {
        if (!Contains(x, y)) throw new ArgumentOutOfRangeException($"Tile ({x}, {y}) is outside the {Width}x{Height} map.");
        return y * Width + x;
    }
}
