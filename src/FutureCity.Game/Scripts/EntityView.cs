using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Draws people, animals, plants and camps as simple placeholder shapes (until the illustrated art arrives in
/// Phase 8). Walkers are interpolated between ticks, and everything is drawn back to front.
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

    /// <summary>World-space position of an entity's feet, interpolated between ticks for walkers.</summary>
    public Vector2 WorldPosition(Entity entity)
    {
        var pos = entity.GetComponent<TilePosition>();
        var tile = new Vector2(pos.X, pos.Y);
        if (entity.TryGetComponent<Mover>(out var mover) && (mover.NextX != pos.X || mover.NextY != pos.Y) && mover.StepCost > 0)
        {
            float t = Mathf.Clamp((mover.Progress + (float)_driver.Alpha * Mover.ProgressPerTick) / mover.StepCost, 0f, 1f);
            tile = tile.Lerp(new Vector2(mover.NextX, mover.NextY), t);
        }
        return _map.TileToLocal(tile);
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
        foreach (var e in world.Store.Query<Plant, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var plant = e.GetComponent<Plant>();
            int max = world.Content.Plants[plant.Kind].MaxFood;
            var color = new Color(world.Content.Plants[plant.Kind].Color);
            _drawList.Add((p.Y, () => DrawBush(p, plant.Food, max, color)));
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
            bool carrying = citizen.CarriedFood > 0;
            _drawList.Add((p.Y, () => DrawPerson(p, adult ? 1f : 0.65f, selected, health, carrying)));
        }

        _drawList.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, draw) in _drawList)
            draw();
    }

    private void Ellipse(Vector2 center, float rx, float ry, Color color)
    {
        DrawSetTransform(center, 0, new Vector2(1, ry / rx));
        DrawCircle(Vector2.Zero, rx, color);
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawPerson(Vector2 feet, float scale, bool selected, float health, bool carrying)
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
        if (carrying) DrawCircle(feet + new Vector2(4.5f, -11) * scale, 2.5f * scale, Meat);
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
}
