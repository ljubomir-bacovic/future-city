using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Selecting villagers (click, shift-click, drag a box) and giving orders with a right click:
/// on an animal to hunt, on a bush or carcass to gather, on the camp to return, anywhere else to move.
/// Orders are sent to the simulation as commands; this node holds only UI state.
/// </summary>
public partial class SelectionController : Node2D
{
    private const float DragThreshold = 6f;   // screen pixels before a click becomes a box
    private const float PickRadius = 16f;     // world pixels around a sprite's body that count as a hit
    private const double PingSeconds = 0.6;

    private static readonly Color BoxFill = new(1, 1, 1, 0.08f);
    private static readonly Color BoxEdge = new(1, 1, 1, 0.7f);

    private SimulationDriver _driver = null!;
    private EntityView _view = null!;
    private MapView _map = null!;
    private readonly List<int> _selected = new();
    private Vector2? _dragStart;
    private Vector2 _dragNow;
    private Vector2 _pingAt;
    private Color _pingColor;
    private double _pingAge = PingSeconds;

    /// <summary>Raised when the selection changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Ids of the selected villagers.</summary>
    public IReadOnlyList<int> Selected => _selected;

    /// <summary>Whether a villager is selected.</summary>
    public bool IsSelected(int id) => _selected.Contains(id);

    /// <summary>Connects the controller to the game.</summary>
    public void Initialize(SimulationDriver driver, EntityView view, MapView map)
    {
        _driver = driver;
        _view = view;
        _map = map;
        _driver.SimulationChanged += () => SetSelection(Array.Empty<int>());
    }

    public override void _Process(double delta)
    {
        // Drop villagers who died since the last frame.
        var world = _driver.Simulation?.World;
        if (world != null && _selected.RemoveAll(id => !world.TryGetEntity(id, out _)) > 0)
            SelectionChanged?.Invoke();
        _pingAge += delta;
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_driver.Simulation == null) return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _dragStart = _dragNow = press.Position;
                break;
            case InputEventMouseMotion motion when _dragStart != null:
                _dragNow = motion.Position;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release when _dragStart is { } start:
                _dragStart = null;
                if (start.DistanceTo(release.Position) < DragThreshold) ClickSelect(release.ShiftPressed);
                else BoxSelect(new Rect2(start, Vector2.Zero).Expand(release.Position), release.ShiftPressed);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                IssueOrder();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                SetSelection(Array.Empty<int>());
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        if (_dragStart is { } start && start.DistanceTo(_dragNow) >= DragThreshold)
        {
            var toWorld = GetViewport().GetCanvasTransform().AffineInverse();
            var rect = new Rect2(toWorld * start, Vector2.Zero).Expand(toWorld * _dragNow);
            DrawRect(rect, BoxFill);
            DrawRect(rect, BoxEdge, false, 1f / GetViewport().GetCanvasTransform().X.X);
        }
        if (_pingAge < PingSeconds)
        {
            float t = (float)(_pingAge / PingSeconds);
            var color = _pingColor with { A = 1 - t };
            DrawSetTransform(_pingAt, 0, new Vector2(1, 0.5f));
            DrawArc(Vector2.Zero, 6 + 14 * t, 0, Mathf.Tau, 24, color, 2);
            DrawSetTransform(Vector2.Zero);
        }
    }

    /// <summary>Selects all of the player's villagers.</summary>
    public void SelectAllOwn()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        var mine = new List<int>();
        foreach (var e in world.Store.Query<Citizen, Owner>().Entities)
        {
            if (IsMine(e)) mine.Add(e.Id);
        }
        SetSelection(mine);
    }

    private void ClickSelect(bool add)
    {
        var world = _driver.Simulation!.World;
        var hit = Pick(world, e => e.HasComponent<Citizen>() && IsMine(e));
        if (hit is not { } entity)
        {
            if (!add) SetSelection(Array.Empty<int>());
            return;
        }
        if (!add) SetSelection(new[] { entity.Id });
        else if (_selected.Contains(entity.Id)) SetSelection(_selected.Where(id => id != entity.Id));
        else SetSelection(_selected.Append(entity.Id));
    }

    private void BoxSelect(Rect2 screenRect, bool add)
    {
        var world = _driver.Simulation!.World;
        var toScreen = GetViewport().GetCanvasTransform();
        var inside = new List<int>();
        foreach (var e in world.Store.Query<Citizen, TilePosition, Owner>().Entities)
        {
            if (IsMine(e) && screenRect.HasPoint(toScreen * BodyCenter(e))) inside.Add(e.Id);
        }
        SetSelection(add ? _selected.Union(inside) : inside);
    }

    private void IssueOrder()
    {
        var sim = _driver.Simulation!;
        var world = sim.World;
        var units = _selected.Where(id => world.TryGetEntity(id, out var e) && Bands.IsAdult(world, e.GetComponent<Citizen>())).ToArray();
        if (units.Length == 0) return;

        var target = Pick(world, e => e.HasComponent<Animal>() || e.HasComponent<Carcass>() || e.HasComponent<Plant>()
            || (e.HasComponent<Camp>() && IsMine(e)));
        Command command;
        if (target is { } t && t.HasComponent<Animal>())
            (command, _pingColor) = (new Hunt(units, t.Id), new Color("#ff6a5a"));
        else if (target is { } c && c.HasComponent<Camp>())
            (command, _pingColor) = (new ReturnToCamp(units), new Color("#ffffff"));
        else if (target is { } f && FoodSources.HasGatherableFood(f))
            (command, _pingColor) = (new Gather(units, f.Id), new Color("#ffd24a"));
        else
        {
            var tile = _map.LocalToTile(GetGlobalMousePosition());
            (command, _pingColor) = (new MoveUnits(units, tile.X, tile.Y), new Color("#7dff8a"));
        }
        sim.Enqueue(command with { Player = Players.Human });
        _pingAt = target is { } pinged ? _view.WorldPosition(pinged) : GetGlobalMousePosition();
        _pingAge = 0;
    }

    // The entity whose sprite is nearest the mouse, within the pick radius.
    private Entity? Pick(World world, Func<Entity, bool> filter)
    {
        var mouse = GetGlobalMousePosition();
        Entity? best = null;
        float bestDistance = PickRadius;
        foreach (var e in world.Store.Query<TilePosition>().Entities)
        {
            if (!filter(e)) continue;
            float distance = BodyCenter(e).DistanceTo(mouse);
            if (distance >= bestDistance) continue;
            best = e;
            bestDistance = distance;
        }
        return best;
    }

    private Vector2 BodyCenter(Entity e) => _view.WorldPosition(e) + new Vector2(0, e.HasComponent<Camp>() ? -8 : -10);

    private static bool IsMine(Entity e) => e.TryGetComponent<Owner>(out var owner) && owner.Player == Players.Human;

    private void SetSelection(IEnumerable<int> ids)
    {
        var next = ids.Distinct().OrderBy(id => id).ToList();
        if (next.SequenceEqual(_selected)) return;
        _selected.Clear();
        _selected.AddRange(next);
        SelectionChanged?.Invoke();
    }
}
