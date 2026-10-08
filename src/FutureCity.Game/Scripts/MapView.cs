using System.Collections.Generic;
using FutureCity.Sim;
using FutureCity.Sim.Content;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Draws the terrain as an isometric tile map. Tiles are placeholder diamonds generated from
/// the terrain colors in content until the illustrated art arrives (Phase 8).
/// </summary>
public partial class MapView : Node2D
{
    /// <summary>Size of one isometric tile in pixels.</summary>
    public static readonly Vector2I TileSize = new(64, 32);

    private TileMapLayer? _layer;

    /// <summary>World-space rectangle enclosing the whole map.</summary>
    public Rect2 Bounds { get; private set; }

    /// <summary>Rebuilds the view from the world's map.</summary>
    public void Build(World world)
    {
        _layer?.QueueFree();
        _layer = new TileMapLayer { Name = "Terrain", TileSet = CreateTileSet(world.Content.Terrains) };
        AddChild(_layer);

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
