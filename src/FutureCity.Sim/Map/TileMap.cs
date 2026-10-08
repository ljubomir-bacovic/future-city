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
    private readonly byte[] _wall;

    private const byte GateBit = 0x80;

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
        _wall = new byte[width * height];
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
        _wall = new byte[width * height];
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

    /// <summary>
    /// Owner of the finished wall or gate on (x, y), or 0. Walls are buildings; this layer only mirrors them for
    /// pathfinding, so it is not saved but rebuilt from the buildings when a game is loaded.
    /// </summary>
    public int GetWallOwner(int x, int y) => _wall[Index(x, y)] & ~GateBit;

    /// <summary>Whether the wall on (x, y) is a gate.</summary>
    public bool IsGate(int x, int y) => (_wall[Index(x, y)] & GateBit) != 0;

    /// <summary>Puts a finished wall (or gate) of <paramref name="owner"/> on (x, y); owner 0 removes it.</summary>
    public void SetWall(int x, int y, int owner, bool gate)
    {
        _wall[Index(x, y)] = owner == 0 ? (byte)0 : (byte)(checked((byte)owner) | (gate ? GateBit : 0));
        if (owner != 0) HasWalls = true;
        Version++;
    }

    /// <summary>Whether a unit of <paramref name="player"/> may enter (x, y) as far as walls go: walls block everyone, gates everyone but their owner.</summary>
    public bool WallLets(int x, int y, int player)
    {
        byte wall = _wall[Index(x, y)];
        return wall == 0 || ((wall & GateBit) != 0 && (wall & ~GateBit) == player);
    }

    /// <summary>Whether any wall stands on the map (lets pathfinding skip wall checks in peaceful games).</summary>
    public bool HasWalls { get; private set; }

    /// <summary>Counts terrain and wall changes, so caches derived from the map know when to rebuild. Not saved.</summary>
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
