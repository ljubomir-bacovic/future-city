using System.Collections.Generic;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The game's art: sprites for units, buildings and nature (<c>Art/sprites.svg</c>) and icons for goods and the HUD
/// (<c>Art/icons.svg</c>). Both sheets are drawn at twice the in-game size so they stay sharp when zoomed in.
/// Sprites are found by name: a building's, unit's or good's content id, or a fixed name such as "villager0". Content
/// without art yet (a new good or building) falls back to the placeholder shapes or text.
/// </summary>
public static class Art
{
    /// <summary>In-game size of a sheet pixel.</summary>
    public const float Scale = 0.5f;

    private const int IconCell = 64;
    private const int IconColumns = 8;

    // Region of each sprite in the sheet and its anchor (a unit's feet, a building's footprint centre) in the region.
    private static readonly Dictionary<string, (Rect2 Region, Vector2 Anchor)> Sprites = new()
    {
        ["hut"] = new(new Rect2(0, 0, 140, 144), new Vector2(70, 114)),
        ["camp"] = new(new Rect2(140, 0, 154, 114), new Vector2(74, 86)),
        ["shrine"] = new(new Rect2(294, 0, 140, 174), new Vector2(70, 142)),
        ["toolmaker"] = new(new Rect2(434, 0, 154, 144), new Vector2(70, 106)),
        ["mill"] = new(new Rect2(588, 0, 140, 154), new Vector2(66, 122)),
        ["mill_sails"] = new(new Rect2(728, 0, 116, 116), new Vector2(58, 58)),
        ["bakery"] = new(new Rect2(844, 0, 154, 134), new Vector2(72, 100)),
        ["mint"] = new(new Rect2(0, 174, 154, 134), new Vector2(72, 100)),
        ["storehouse"] = new(new Rect2(154, 174, 304, 234), new Vector2(150, 160)),
        ["marketplace"] = new(new Rect2(458, 174, 264, 204), new Vector2(132, 132)),
        ["villager0"] = new(new Rect2(722, 174, 44, 68), new Vector2(22, 60)),
        ["villager0_walk"] = new(new Rect2(766, 174, 44, 68), new Vector2(22, 60)),
        ["villager1"] = new(new Rect2(810, 174, 44, 68), new Vector2(22, 60)),
        ["villager1_walk"] = new(new Rect2(854, 174, 44, 68), new Vector2(22, 60)),
        ["villager2"] = new(new Rect2(898, 174, 44, 68), new Vector2(22, 60)),
        ["villager2_walk"] = new(new Rect2(942, 174, 44, 68), new Vector2(22, 60)),
        ["porter"] = new(new Rect2(0, 408, 44, 74), new Vector2(22, 66)),
        ["porter_walk"] = new(new Rect2(44, 408, 44, 74), new Vector2(22, 66)),
        ["deer"] = new(new Rect2(88, 408, 84, 80), new Vector2(38, 72)),
        ["deer_run"] = new(new Rect2(172, 408, 84, 80), new Vector2(38, 72)),
        ["carcass"] = new(new Rect2(256, 408, 68, 36), new Vector2(32, 28)),
        ["mule"] = new(new Rect2(324, 408, 84, 74), new Vector2(38, 66)),
        ["mule_loaded"] = new(new Rect2(408, 408, 84, 74), new Vector2(38, 66)),
        ["bush"] = new(new Rect2(492, 408, 54, 46), new Vector2(27, 36)),
        ["bush_bare"] = new(new Rect2(546, 408, 44, 34), new Vector2(22, 26)),
        ["stone_outcrop"] = new(new Rect2(590, 408, 74, 44), new Vector2(36, 34)),
        ["silver_vein"] = new(new Rect2(664, 408, 74, 44), new Vector2(36, 34)),
        ["clay_pit"] = new(new Rect2(738, 408, 78, 50), new Vector2(36, 32)),
        ["pine0"] = new(new Rect2(816, 408, 50, 82), new Vector2(25, 74)),
        ["pine1"] = new(new Rect2(866, 408, 44, 72), new Vector2(22, 64)),
        ["farm"] = new(new Rect2(0, 490, 136, 76), new Vector2(68, 36)), // build menu only: fields are drawn as they grow
        // Soldiers, fortifications, the stable and ruins (Phase 4).
        ["clubman"] = new(new Rect2(136, 490, 56, 100), new Vector2(24, 90)),
        ["clubman_walk"] = new(new Rect2(192, 490, 56, 100), new Vector2(24, 90)),
        ["spearman"] = new(new Rect2(248, 490, 56, 100), new Vector2(28, 90)),
        ["spearman_walk"] = new(new Rect2(304, 490, 56, 100), new Vector2(28, 90)),
        ["archer"] = new(new Rect2(360, 490, 56, 100), new Vector2(30, 90)),
        ["archer_walk"] = new(new Rect2(416, 490, 56, 100), new Vector2(30, 90)),
        ["cavalry"] = new(new Rect2(472, 490, 104, 120), new Vector2(48, 110)),
        ["cavalry_walk"] = new(new Rect2(576, 490, 104, 120), new Vector2(48, 110)),
        ["ram"] = new(new Rect2(680, 490, 194, 104), new Vector2(84, 68)),
        ["palisade"] = new(new Rect2(874, 490, 124, 108), new Vector2(62, 62)),
        ["gate"] = new(new Rect2(0, 610, 124, 116), new Vector2(62, 66)),
        ["stone_wall"] = new(new Rect2(124, 610, 128, 104), new Vector2(64, 60)),
        ["tower"] = new(new Rect2(252, 610, 124, 194), new Vector2(62, 152)),
        ["stable"] = new(new Rect2(376, 610, 264, 204), new Vector2(128, 130)),
        ["loot"] = new(new Rect2(640, 610, 64, 44), new Vector2(30, 32)),
    };

    private static readonly string[] IconNames =
    [
        "berries", "meat", "grain", "flour", "bread", "wood", "stone", "clay",
        "tools", "silver", "food", "people", "house", "coins", "prices", "happiness",
        "spring", "summer", "autumn", "winter", "horses", "soldiers", "morale",
    ];

    private static readonly Color[] PlayerColors = [new("#d8d2c4"), new("#3b6fb6"), new("#c0392b"), new("#d9a83a"), new("#8e5bb5")];

    /// <summary>The colour that marks a player's people, flags and buildings (nature is grey).</summary>
    public static Color PlayerColor(int player) => PlayerColors[Mathf.Clamp(player, 0, PlayerColors.Length - 1)];

    /// <summary>Where the mill's sails turn, relative to the mill's footprint centre (in-game pixels).</summary>
    public static readonly Vector2 MillHub = new Vector2(19.2f, -28.4f) * Scale;

    private static Texture2D? _sprites, _icons;
    private static readonly Dictionary<string, AtlasTexture> IconCache = [];
    private static readonly Dictionary<string, AtlasTexture> SpriteIconCache = [];

    private static Texture2D SpriteSheet => _sprites ??= GD.Load<Texture2D>("res://Art/sprites.svg");
    private static Texture2D IconSheet => _icons ??= GD.Load<Texture2D>("res://Art/icons.svg");

    /// <summary>Whether there is a sprite of this name.</summary>
    public static bool HasSprite(string name) => Sprites.ContainsKey(name);

    /// <summary>
    /// Draws a sprite with its anchor at <paramref name="at"/>, scaled (1 = normal size), optionally mirrored, rotated
    /// or tinted. Returns false if there is no such sprite.
    /// </summary>
    public static bool DrawSprite(CanvasItem canvas, string name, Vector2 at, float scale = 1f, bool flip = false,
        float rotation = 0f, Color? modulate = null)
    {
        if (!Sprites.TryGetValue(name, out var sprite)) return false;
        float s = Scale * scale;
        canvas.DrawSetTransform(at, rotation, new Vector2(flip ? -s : s, s));
        canvas.DrawTextureRectRegion(SpriteSheet, new Rect2(-sprite.Anchor, sprite.Region.Size), sprite.Region, modulate);
        canvas.DrawSetTransform(Vector2.Zero);
        return true;
    }

    /// <summary>An icon by name (a good's content id, or food, people, house, coins, prices, happiness, or a season), or null.</summary>
    public static Texture2D? Icon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached)) return cached;
        int index = System.Array.IndexOf(IconNames, name);
        if (index < 0) return null;
        var icon = new AtlasTexture
        {
            Atlas = IconSheet,
            Region = new Rect2(index % IconColumns * IconCell, index / IconColumns * IconCell, IconCell, IconCell),
        };
        IconCache[name] = icon;
        return icon;
    }

    /// <summary>Draws an icon centred at <paramref name="center"/>, <paramref name="size"/> in-game pixels wide.</summary>
    public static bool DrawIcon(CanvasItem canvas, string name, Vector2 center, float size)
    {
        if (Icon(name) is not AtlasTexture icon) return false;
        canvas.DrawTextureRectRegion(icon.Atlas, new Rect2(center - new Vector2(size, size) / 2, new Vector2(size, size)), icon.Region);
        return true;
    }

    /// <summary>A sprite as a texture for buttons (e.g. a building in the build menu), or null.</summary>
    public static Texture2D? SpriteIcon(string name)
    {
        if (SpriteIconCache.TryGetValue(name, out var cached)) return cached;
        if (!Sprites.TryGetValue(name, out var sprite)) return null;
        var icon = new AtlasTexture { Atlas = SpriteSheet, Region = sprite.Region };
        SpriteIconCache[name] = icon;
        return icon;
    }

    /// <summary>
    /// An icon followed by a value, for bars and lists: returns the row and the label to update. Without art for the
    /// icon, the tooltip text stands in for it.
    /// </summary>
    public static HBoxContainer IconValue(string icon, string tooltip, out Label value, int size = 22)
    {
        var row = new HBoxContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 3);
        row.AddChild(IconRect(icon, size, tooltip));
        value = new Label { MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = tooltip };
        row.AddChild(value);
        return row;
    }

    /// <summary>An icon as a control, or the name as text when there is no art for it.</summary>
    public static Control IconRect(string icon, int size, string tooltip)
    {
        if (Icon(icon) is not { } texture)
            return new Label { Text = tooltip, MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = tooltip };
        return new TextureRect
        {
            Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = tooltip,
        };
    }

    /// <summary>Appends an icon (or the name, without art) to rich text, with the name as its tooltip.</summary>
    public static void AddIcon(RichTextLabel text, string icon, string name, int size = 18)
    {
        if (Icon(icon) is { } texture) text.AddImage(texture, size, size, inlineAlign: InlineAlignment.Center, tooltip: name);
        else text.AddText(name);
    }

    /// <summary>Appends goods as "icon amount" pairs to rich text, e.g. the contents of a store.</summary>
    public static void AddGoods(RichTextLabel text, Sim.World world, IReadOnlyList<int> amounts, int max = int.MaxValue)
    {
        int shown = 0;
        for (int g = 0; g < amounts.Count && shown < max; g++)
        {
            if (amounts[g] <= 0) continue;
            if (shown++ > 0) text.AddText("   ");
            var good = world.Content.Goods[g];
            AddIcon(text, good.Id, good.Name);
            text.AddText($" {amounts[g]}");
        }
    }
}
