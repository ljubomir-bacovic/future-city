namespace FutureCity.Sim.Map;

/// <summary>
/// The terrain grid. Each tile stores an index into <see cref="Content.ContentDatabase.Terrains"/>.
/// Tile (0, 0) is the top corner of the isometric view; x grows down-right, y grows down-left.
/// </summary>
public sealed class TileMap
{
    private readonly byte[] _terrain;

    /// <summary>Creates a map filled with one terrain.</summary>
    public TileMap(int width, int height, byte fillTerrain)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _terrain = new byte[width * height];
        Array.Fill(_terrain, fillTerrain);
    }

    internal TileMap(int width, int height, byte[] terrain)
    {
        if (terrain.Length != width * height)
            throw new ArgumentException("Terrain data does not match map size.", nameof(terrain));
        Width = width;
        Height = height;
        _terrain = terrain;
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
    public void SetTerrain(int x, int y, int terrain) => _terrain[Index(x, y)] = checked((byte)terrain);

    /// <summary>Raw row-major terrain data, for saving and rendering.</summary>
    public ReadOnlySpan<byte> RawTerrain => _terrain;

    private int Index(int x, int y)
    {
        if (!Contains(x, y)) throw new ArgumentOutOfRangeException($"Tile ({x}, {y}) is outside the {Width}x{Height} map.");
        return y * Width + x;
    }
}
