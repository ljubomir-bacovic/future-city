using System.Collections.Generic;
using FutureCity.Sim;
using FutureCity.Sim.Content;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Draws the terrain as an isometric tile map. Tiles are placeholder diamonds generated from
/// the terrain colors in content, and forests get simple drawn trees, until the illustrated art arrives (Phase 8).
/// </summary>
public partial class MapView : Node2D
{
    /// <summary>Size of one isometric tile in pixels.</summary>
    public static readonly Vector2I TileSize = new(64, 32);

    private static readonly Color TreeColor = new("#2f5a2a");
    private static readonly Color TreeShade = new("#24461f");
    private static readonly Color TrunkColor = new("#5a3e26");

    private TileMapLayer? _layer;
    private Vector2 _origin, _axisX, _axisY;
    private readonly List<Vector2> _trees = new();

    /// <summary>World-space rectangle enclosing the whole map.</summary>
    public Rect2 Bounds { get; private set; }

    /// <summary>Rebuilds the view from the world's map.</summary>
    public void Build(World world)
    {
        _layer?.QueueFree();
        // ShowBehindParent: the tiles go under the trees this node draws itself.
        _layer = new TileMapLayer { Name = "Terrain", TileSet = CreateTileSet(world.Content.Terrains), ShowBehindParent = true };
        AddChild(_layer);
        _origin = _layer.MapToLocal(Vector2I.Zero);
        _axisX = _layer.MapToLocal(new Vector2I(1, 0)) - _origin;
        _axisY = _layer.MapToLocal(new Vector2I(0, 1)) - _origin;

        var map = world.Map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
                _layer.SetCell(new Vector2I(x, y), 0, new Vector2I(map.GetTerrain(x, y), (x + y) & 1));
        }

        var half = (Vector2)TileSize / 2;
        var top = _layer.MapToLocal(new Vector2I(0, 0)) - new Vector2(0, half.Y);
        var right = _layer.MapToLocal(new Vector2I(map.Width - 1, 0)) + new Vector2(half.X, 0);
        var bottom = _layer.MapToLocal(new Vector2I(map.Width - 1, map.Height - 1)) + new Vector2(0, half.Y);
        var left = _layer.MapToLocal(new Vector2I(0, map.Height - 1)) - new Vector2(half.X, 0);
        Bounds = new Rect2(left.X, top.Y, right.X - left.X, bottom.Y - top.Y);

        PlaceTrees(world);
        QueueRedraw();
    }

    /// <summary>Converts a (fractional) tile coordinate to the world-space position of that point on the ground.</summary>
    public Vector2 TileToLocal(Vector2 tile) => _origin + _axisX * tile.X + _axisY * tile.Y;

    public override void _Draw()
    {
        foreach (var tree in _trees)
        {
            DrawRect(new Rect2(tree + new Vector2(-1.5f, -6), new Vector2(3, 6)), TrunkColor);
            DrawColoredPolygon([tree + new Vector2(-9, -5), tree + new Vector2(9, -5), tree + new Vector2(0, -26)], TreeShade);
            DrawColoredPolygon([tree + new Vector2(-7, -12), tree + new Vector2(7, -12), tree + new Vector2(0, -30)], TreeColor);
        }
    }

    // Two or three trees per forest tile, jittered by a hash of the tile so the wood looks natural but never changes.
    private void PlaceTrees(World world)
    {
        _trees.Clear();
        int forest = -1;
        for (int i = 0; i < world.Content.Terrains.Count; i++)
        {
            if (world.Content.Terrains[i].Id == world.Content.Rules.MapGeneration.ForestTerrain) forest = i;
        }
        var map = world.Map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.GetTerrain(x, y) != forest) continue;
                uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                int count = 2 + (int)(hash % 2);
                for (int t = 0; t < count; t++)
                {
                    hash = hash * 1664525 + 1013904223;
                    float fx = (hash >> 8) % 1000 / 1000f, fy = (hash >> 18) % 1000 / 1000f;
                    _trees.Add(TileToLocal(new Vector2(x - 0.35f + fx * 0.7f, y - 0.35f + fy * 0.7f)));
                }
            }
        }
        // Back to front, so nearer trees overlap farther ones.
        _trees.Sort((a, b) => a.Y.CompareTo(b.Y));
    }

    /// <summary>Converts a world-space position to the tile under it.</summary>
    public Vector2I LocalToTile(Vector2 position) => _layer?.LocalToMap(position) ?? Vector2I.Zero;

    private static TileSet CreateTileSet(IReadOnlyList<TerrainDef> terrains)
    {
        // Atlas: one column per terrain, two rows (plain and slightly darker) for a subtle checker that shows the grid.
        var image = Image.CreateEmpty(TileSize.X * terrains.Count, TileSize.Y * 2, false, Image.Format.Rgba8);
        for (int i = 0; i < terrains.Count; i++)
        {
            var baseColor = Color.FromHtml(terrains[i].Color);
            DrawDiamond(image, new Vector2I(i * TileSize.X, 0), baseColor);
            DrawDiamond(image, new Vector2I(i * TileSize.X, TileSize.Y), baseColor.Darkened(0.06f));
        }

        var source = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureRegionSize = TileSize,
        };
        var tileSet = new TileSet
        {
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileSize = TileSize,
        };
        tileSet.AddSource(source, 0);
        for (int i = 0; i < terrains.Count; i++)
        {
            source.CreateTile(new Vector2I(i, 0));
            source.CreateTile(new Vector2I(i, 1));
        }
        return tileSet;
    }

    private static void DrawDiamond(Image image, Vector2I origin, Color fill)
    {
        var edge = fill.Darkened(0.18f);
        float hw = TileSize.X / 2f, hh = TileSize.Y / 2f;
        for (int py = 0; py < TileSize.Y; py++)
        {
            for (int px = 0; px < TileSize.X; px++)
            {
                float d = Mathf.Abs(px + 0.5f - hw) / hw + Mathf.Abs(py + 0.5f - hh) / hh;
                if (d <= 1f)
                    image.SetPixel(origin.X + px, origin.Y + py, d > 0.94f ? edge : fill);
            }
        }
    }
}
