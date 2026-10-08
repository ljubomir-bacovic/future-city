using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Draws people, animals, plants, deposits, camps and buildings as simple placeholder shapes (until the illustrated
/// art arrives in Phase 8). Walkers are interpolated between ticks, and everything is drawn back to front.
/// Construction sites show a scaffold and a progress bar; fields show their crop.
/// </summary>
public partial class EntityView : Node2D
{
    private static readonly Color Shadow = new(0, 0, 0, 0.25f);
    private static readonly Color Skin = new("#e0b48c");
    private static readonly Color Tunic = new("#3b6fb6");
    private static readonly Color Hair = new("#4a3020");
    private static readonly Color Leaf = new("#3f8a3a");
    private static readonly Color LeafDark = new("#2e6b2b");
    private static readonly Color BareTwig = new("#7a6248");
    private static readonly Color Meat = new("#8c3b2f");
    private static readonly Color Tent = new("#b08850");
    private static readonly Color TentDark = new("#8a6a3c");
    private static readonly Color Fire = new("#ff9a2e");
    private static readonly Color SelectRing = new(1, 1, 1, 0.9f);
    private static readonly Color HealthGood = new("#5ecf5e");
    private static readonly Color HealthBad = new("#d94a3a");
    private static readonly Color Soil = new("#7a5a3a");
    private static readonly Color Sprout = new("#6fae45");
    private static readonly Color RipeCrop = new("#e2c04a");
    private static readonly Color Scaffold = new("#c9a46a");
    private static readonly Color Thatch = new("#c8a35a");

    private SimulationDriver _driver = null!;
    private MapView _map = null!;
    private SelectionController _selection = null!;
    private readonly List<(float Depth, Action Draw)> _drawList = new();

    /// <summary>Connects the view to the game it draws.</summary>
    public void Initialize(SimulationDriver driver, MapView map, SelectionController selection)
    {
        _driver = driver;
        _map = map;
        _selection = selection;
    }

    public override void _Process(double delta) => QueueRedraw();

    /// <summary>World-space position of an entity's feet (a building's footprint center), interpolated for walkers.</summary>
    public Vector2 WorldPosition(Entity entity)
    {
        var pos = entity.GetComponent<TilePosition>();
        var tile = new Vector2(pos.X, pos.Y);
        if (entity.TryGetComponent<Building>(out var building))
        {
            float half = (_driver.Simulation!.World.Content.Buildings[building.Kind].Def.Size - 1) / 2f;
            return _map.TileToLocal(tile + new Vector2(half, half));
        }
        if (entity.TryGetComponent<Mover>(out var mover) && (mover.NextX != pos.X || mover.NextY != pos.Y) && mover.StepCost > 0)
        {
            float t = Mathf.Clamp((mover.Progress + (float)_driver.Alpha * Mover.ProgressPerTick) / mover.StepCost, 0f, 1f);
            tile = tile.Lerp(new Vector2(mover.NextX, mover.NextY), t);
        }
        return _map.TileToLocal(tile);
    }

    /// <summary>The four ground corners (top, right, bottom, left) of a square of tiles, shrunk by <paramref name="inset"/> tiles.</summary>
    public Vector2[] Footprint(int x, int y, int size, float inset = 0f)
    {
        float a = -0.5f + inset, b = size - 0.5f - inset;
        return
        [
            _map.TileToLocal(new Vector2(x + a, y + a)),
            _map.TileToLocal(new Vector2(x + b, y + a)),
            _map.TileToLocal(new Vector2(x + b, y + b)),
            _map.TileToLocal(new Vector2(x + a, y + b)),
        ];
    }

    public override void _Draw()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;

        _drawList.Clear();
        foreach (var e in world.Store.Query<Camp, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            _drawList.Add((p.Y, () => DrawCamp(p)));
        }
        foreach (var e in world.Store.Query<Building, TilePosition>().Entities)
            AddBuilding(world, e);
        foreach (var e in world.Store.Query<Plant, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var plant = e.GetComponent<Plant>();
            int max = world.Content.Plants[plant.Kind].MaxFood;
            var color = new Color(world.Content.Plants[plant.Kind].Color);
            _drawList.Add((p.Y, () => DrawBush(p, plant.Food, max, color)));
        }
        foreach (var e in world.Store.Query<Deposit, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var deposit = e.GetComponent<Deposit>();
            var def = world.Content.Deposits[deposit.Kind];
            float fill = Mathf.Clamp((float)deposit.Amount / def.Amount, 0.25f, 1f);
            var color = new Color(def.Color);
            bool pit = def.NearWater;
            _drawList.Add((p.Y, () => { if (pit) DrawClayPit(p, color, fill); else DrawRocks(p, color, fill); }));
        }
        foreach (var e in world.Store.Query<Carcass, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            _drawList.Add((p.Y, () => DrawCarcass(p)));
        }
        foreach (var e in world.Store.Query<Animal, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var color = new Color(world.Content.Animals[e.GetComponent<Animal>().Kind].Color);
            var mover = e.GetComponent<Mover>();
            bool facingLeft = mover.NextX - e.GetComponent<TilePosition>().X < mover.NextY - e.GetComponent<TilePosition>().Y;
            _drawList.Add((p.Y, () => DrawDeer(p, color, facingLeft)));
        }
        var rules = world.Content.Citizens;
        foreach (var e in world.Store.Query<Citizen, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var citizen = e.GetComponent<Citizen>();
            bool adult = Bands.IsAdult(world, citizen);
            bool selected = _selection.IsSelected(e.Id);
            float health = (float)citizen.Health / rules.MaxHealth;
            Color? load = citizen.Carried > 0 ? new Color(world.Content.Goods[citizen.CarriedGood].Color) : null;
            bool tool = citizen.ToolWear > 0;
            _drawList.Add((p.Y, () => DrawPerson(p, adult ? 1f : 0.65f, selected, health, load, tool)));
        }

        _drawList.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, draw) in _drawList)
            draw();
    }

    private void AddBuilding(World world, Entity e)
    {
        var type = world.Content.Buildings[e.GetComponent<Building>().Kind];
        var pos = e.GetComponent<TilePosition>();
        int size = type.Def.Size;
        var color = new Color(type.Def.Color);
        bool selected = _selection.SelectedBuilding == e.Id;
        if (e.TryGetComponent<Field>(out var field))
        {
            // Fields lie flat on the ground: draw them first, under everything standing on them.
            int stacked = type.FieldGood >= 0 ? e.GetComponent<Inventory>().Amounts[type.FieldGood] : 0;
            bool done = Buildings.IsComplete(e);
            float growth = field.Stage == FieldStage.Growing
                ? Mathf.Clamp(field.Progress / (float)(type.Def.Field!.GrowTicks * Labor.PerTick), 0, 1) : 0;
            _drawList.Add((float.MinValue, () => DrawField(pos.X, pos.Y, size, done ? field.Stage : FieldStage.Fallow, growth, stacked, selected)));
            if (!done) AddSiteBar(world, e, pos, size);
            return;
        }
        var corners = Footprint(pos.X, pos.Y, size);
        float depth = corners[2].Y;
        if (Buildings.IsComplete(e))
        {
            string id = type.Def.Id;
            _drawList.Add((depth, () => DrawBuilding(id, pos.X, pos.Y, size, color, selected)));
        }
        else
        {
            int percent = Buildings.ConstructionPercent(world, e);
            _drawList.Add((depth, () => DrawSite(pos.X, pos.Y, size, color, percent, selected)));
        }
    }

    private void AddSiteBar(World world, Entity e, TilePosition pos, int size)
    {
        int percent = Buildings.ConstructionPercent(world, e);
        var top = Footprint(pos.X, pos.Y, size)[0];
        _drawList.Add((float.MaxValue, () => DrawProgress(top + new Vector2(0, -8), percent)));
    }

    private void Ellipse(Vector2 center, float rx, float ry, Color color)
    {
        DrawSetTransform(center, 0, new Vector2(1, ry / rx));
        DrawCircle(Vector2.Zero, rx, color);
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawPerson(Vector2 feet, float scale, bool selected, float health, Color? load, bool tool)
    {
        if (selected)
        {
            DrawSetTransform(feet, 0, new Vector2(1, 0.5f));
            DrawArc(Vector2.Zero, 11, 0, Mathf.Tau, 24, SelectRing, 1.5f);
            DrawSetTransform(Vector2.Zero);
        }
        Ellipse(feet, 6 * scale, 3 * scale, Shadow);
        var body = feet + new Vector2(0, -9 * scale);
        DrawRect(new Rect2(body + new Vector2(-3.5f, -6) * scale, new Vector2(7, 12) * scale), Tunic);
        DrawCircle(feet + new Vector2(0, -19 * scale), 4 * scale, Skin);
        DrawCircle(feet + new Vector2(0, -21 * scale), 3 * scale, Hair);
        if (tool) DrawLine(feet + new Vector2(-4, -8) * scale, feet + new Vector2(-7, -16) * scale, new Color("#5f6f7a"), 1.5f);
        if (load is { } goods) DrawCircle(feet + new Vector2(4.5f, -11) * scale, 2.8f * scale, goods);
        if (selected)
        {
            var bar = new Rect2(feet + new Vector2(-8, -30 * scale), new Vector2(16, 2.5f));
            DrawRect(bar, new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * health, bar.Size.Y)), health > 0.4f ? HealthGood : HealthBad);
        }
    }

    private void DrawDeer(Vector2 feet, Color color, bool facingLeft)
    {
        float dir = facingLeft ? -1 : 1;
        Ellipse(feet, 9, 3.5f, Shadow);
        var dark = color.Darkened(0.3f);
        for (int i = -1; i <= 1; i += 2)
        {
            DrawLine(feet + new Vector2(i * 5, -7), feet + new Vector2(i * 5, 0), dark, 1.5f);
            DrawLine(feet + new Vector2(i * 3, -7), feet + new Vector2(i * 3, 0), dark, 1.5f);
        }
        Ellipse(feet + new Vector2(0, -9), 8, 4, color);
        var head = feet + new Vector2(8 * dir, -15);
        DrawLine(feet + new Vector2(5 * dir, -10), head, color, 3);
        DrawCircle(head, 2.5f, color);
        DrawLine(head + new Vector2(-1, -2), head + new Vector2(-2 * dir, -6), dark, 1);
    }

    private void DrawCarcass(Vector2 feet)
    {
        Ellipse(feet, 9, 3.5f, Shadow);
        Ellipse(feet + new Vector2(0, -3), 8, 3.5f, Meat);
        Ellipse(feet + new Vector2(-2, -4), 3, 1.5f, Meat.Lightened(0.25f));
    }

    private void DrawBush(Vector2 feet, int food, int maxFood, Color berry)
    {
        Ellipse(feet, 10, 4, Shadow);
        if (food == 0)
        {
            for (int i = -2; i <= 2; i++)
                DrawLine(feet, feet + new Vector2(i * 3, -10 + Math.Abs(i) * 2), BareTwig, 1.2f);
            return;
        }
        DrawCircle(feet + new Vector2(-4, -6), 6, LeafDark);
        DrawCircle(feet + new Vector2(4, -6), 6, LeafDark);
        DrawCircle(feet + new Vector2(0, -10), 7, Leaf);
        int berries = Math.Max(1, (int)Math.Ceiling(8.0 * food / maxFood));
        for (int i = 0; i < berries; i++)
        {
            float angle = i * 2.4f;
            DrawCircle(feet + new Vector2(0, -8) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f) * (2 + i % 3 * 2), 1.6f, berry);
        }
    }

    private void DrawRocks(Vector2 feet, Color stone, float fill)
    {
        Ellipse(feet, 13 * fill + 4, 5 * fill + 2, Shadow);
        var dark = stone.Darkened(0.35f);
        DrawColoredPolygon([feet + new Vector2(-11, 0) * fill, feet + new Vector2(-7, -10) * fill, feet + new Vector2(1, -13) * fill,
            feet + new Vector2(6, -6) * fill, feet + new Vector2(3, 1) * fill], stone);
        DrawColoredPolygon([feet + new Vector2(1, -13) * fill, feet + new Vector2(6, -6) * fill, feet + new Vector2(3, 1) * fill,
            feet + new Vector2(-1, -5) * fill], dark);
        DrawColoredPolygon([feet + new Vector2(4, 1), feet + new Vector2(7, -6) * fill, feet + new Vector2(12, -4) * fill,
            feet + new Vector2(11, 2)], stone.Lightened(0.1f));
    }

    private void DrawClayPit(Vector2 feet, Color clay, float fill)
    {
        Ellipse(feet, 12, 5.5f, clay.Darkened(0.2f));
        Ellipse(feet + new Vector2(0, 0.5f), 9 * fill, 4 * fill, clay.Darkened(0.5f));
        Ellipse(feet + new Vector2(-6, -2), 3, 1.5f, clay.Lightened(0.15f));
    }

    private void DrawCamp(Vector2 feet)
    {
        Ellipse(feet, 22, 9, Shadow);
        var left = feet + new Vector2(-14, 2);
        DrawColoredPolygon([left + new Vector2(-12, 0), left + new Vector2(12, 0), left + new Vector2(0, -22)], Tent);
        DrawColoredPolygon([left + new Vector2(0, -22), left + new Vector2(12, 0), left + new Vector2(5, 0)], TentDark);
        var right = feet + new Vector2(14, -4);
        DrawColoredPolygon([right + new Vector2(-10, 0), right + new Vector2(10, 0), right + new Vector2(0, -18)], Tent);
        DrawColoredPolygon([right + new Vector2(0, -18), right + new Vector2(10, 0), right + new Vector2(4, 0)], TentDark);
        DrawCircle(feet + new Vector2(0, 6), 4, Fire.Darkened(0.4f));
        DrawColoredPolygon([feet + new Vector2(-3, 6), feet + new Vector2(3, 6), feet + new Vector2(0, -3)], Fire);
    }

    private void DrawField(int x, int y, int size, FieldStage stage, float growth, int stacked, bool selected)
    {
        var ground = Footprint(x, y, size, 0.05f);
        DrawColoredPolygon(ground, Soil);
        var rows = stage switch
        {
            FieldStage.Ripe => RipeCrop,
            FieldStage.Growing => Soil.Lerp(Sprout, 0.35f + 0.65f * growth),
            _ => Soil.Darkened(0.15f),
        };
        // Furrows: stripes running across the field.
        for (int i = 0; i < size * 3; i++)
        {
            float t = (i + 0.5f) / (size * 3);
            var a = ground[0].Lerp(ground[3], t);
            var b = ground[1].Lerp(ground[2], t);
            DrawLine(a, b, rows, stage == FieldStage.Fallow ? 1f : 2.5f);
        }
        if (stacked > 0)
        {
            var center = _map.TileToLocal(new Vector2(x + size - 1, y + size - 1));
            int sheaves = Math.Min(4, 1 + stacked / 40);
            for (int i = 0; i < sheaves; i++)
            {
                var p = center + new Vector2(-8 + i * 5, -i % 2 * 3);
                DrawColoredPolygon([p + new Vector2(-3, 0), p + new Vector2(3, 0), p + new Vector2(0, -10)], RipeCrop.Darkened(0.1f));
            }
        }
        if (selected) DrawPolyline([.. ground, ground[0]], SelectRing, 1.5f);
    }

    // A simple house: walls on the footprint and a roof. Details depend on the building type.
    private void DrawBuilding(string id, int x, int y, int size, Color color, bool selected)
    {
        float inset = size == 1 ? 0.12f : 0.08f;
        var c = Footprint(x, y, size, inset);
        float wall = id switch { "storehouse" => 16, "shrine" => 10, "hut" => 9, _ => 13 };
        float roof = id switch { "storehouse" => 10, "shrine" => 16, "hut" => 16, _ => 12 };
        var up = new Vector2(0, -wall);
        var shade = color.Darkened(0.25f);
        Ellipse((c[1] + c[3]) / 2 + new Vector2(0, 2), (c[1].X - c[3].X) / 2 + 4, (c[2].Y - c[0].Y) / 2 + 2, Shadow);
        if (selected) DrawPolyline([.. c, c[0]], SelectRing, 1.5f);
        DrawColoredPolygon([c[3], c[2], c[2] + up, c[3] + up], color);
        DrawColoredPolygon([c[2], c[1], c[1] + up, c[2] + up], shade);
        var roofColor = id == "hut" ? Thatch : id == "shrine" ? new Color("#e9e2cf") : color.Darkened(0.45f);
        var peak = (c[0] + c[2]) / 2 + up + new Vector2(0, -roof);
        DrawColoredPolygon([c[3] + up, c[2] + up, peak], roofColor);
        DrawColoredPolygon([c[2] + up, c[1] + up, peak], roofColor.Darkened(0.2f));
        var door = (c[2] + c[3]) / 2;
        DrawRect(new Rect2(door + new Vector2(-2.5f, -7), new Vector2(5, 7)), new Color("#3a2a1a"));

        switch (id)
        {
            case "mill":
                var hub = (c[1] + c[2]) / 2 + new Vector2(4, -wall * 0.7f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2 + 0.4f;
                    DrawLine(hub, hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11, new Color("#efe6cf"), 2);
                }
                DrawCircle(hub, 2, new Color("#5a4632"));
                break;
            case "bakery":
                var chimney = peak + new Vector2(6, 6);
                DrawRect(new Rect2(chimney, new Vector2(4, 9)), new Color("#8a4a32"));
                DrawCircle(chimney + new Vector2(2, -4), 3, new Color(0.85f, 0.85f, 0.85f, 0.6f));
                break;
            case "toolmaker":
                var anvil = (c[2] + c[1]) / 2 + new Vector2(8, 2);
                DrawRect(new Rect2(anvil + new Vector2(-4, -5), new Vector2(8, 3)), new Color("#4a5560"));
                DrawRect(new Rect2(anvil + new Vector2(-1.5f, -2), new Vector2(3, 3)), new Color("#4a5560"));
                break;
            case "storehouse":
                var crates = (c[3] + c[2]) / 2 + new Vector2(-10, 2);
                DrawRect(new Rect2(crates + new Vector2(-4, -6), new Vector2(7, 6)), new Color("#9a7448"));
                DrawRect(new Rect2(crates + new Vector2(3, -5), new Vector2(6, 5)), new Color("#8a6a3c"));
                break;
            case "shrine":
                DrawCircle(peak + new Vector2(0, -3), 2.5f, new Color("#f2c94c"));
                break;
        }
    }

    // A construction site: the footprint marked out, a scaffold growing with progress, and a progress bar.
    private void DrawSite(int x, int y, int size, Color color, int percent, bool selected)
    {
        var c = Footprint(x, y, size, 0.1f);
        DrawColoredPolygon(c, color with { A = 0.18f });
        DrawPolyline([.. c, c[0]], selected ? SelectRing : Scaffold, selected ? 1.5f : 1f);
        float height = 4 + 14 * percent / 100f;
        foreach (var corner in c)
            DrawLine(corner, corner + new Vector2(0, -height), Scaffold, 1.5f);
        if (percent >= 50)
        {
            var up = new Vector2(0, -height);
            DrawColoredPolygon([c[3], c[2], c[2] + up, c[3] + up], color with { A = 0.55f });
            DrawColoredPolygon([c[2], c[1], c[1] + up, c[2] + up], color.Darkened(0.25f) with { A = 0.55f });
        }
        DrawProgress(c[0] + new Vector2(0, -height - 8), percent);
    }

    private void DrawProgress(Vector2 center, int percent)
    {
        var bar = new Rect2(center + new Vector2(-12, 0), new Vector2(24, 3));
        DrawRect(bar, new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * percent / 100f, bar.Size.Y)), new Color("#f2c94c"));
    }
}
