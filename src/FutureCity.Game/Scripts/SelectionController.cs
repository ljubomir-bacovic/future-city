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
/// Selecting villagers and soldiers (click, shift-click, drag a box) or a building (click), and giving orders with a
/// right click: on an animal to hunt; on a bush, carcass, stone or clay deposit to gather; on a forest to cut wood;
/// on a construction site (or a damaged building) to build; on a farm, workshop or shrine to work there; on the camp to
/// return; anywhere else to move. Soldiers attack an enemy right-clicked, loot ruins, and with Ctrl held loot an enemy
/// store or home or attack-move over open ground; groups of soldiers march in the chosen <see cref="Formation"/>.
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

    // Waiting for a click on the map to place the rally point.
    private bool _placingRally;

    /// <summary>How groups of soldiers line up when they move.</summary>
    public Sim.Navigation.Formation Formation { get; set; }

    /// <summary>Raised when the selection changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Ids of the selected villagers.</summary>
    public IReadOnlyList<int> Selected => _selected;

    /// <summary>Id of the selected building, or 0. A building is selected only while no villagers are.</summary>
    public int SelectedBuilding { get; private set; }

    /// <summary>Whether a villager is selected.</summary>
    public bool IsSelected(int id) => _selected.Contains(id);

    /// <summary>Connects the controller to the game.</summary>
    public void Initialize(SimulationDriver driver, EntityView view, MapView map)
    {
        _driver = driver;
        _view = view;
        _map = map;
        _driver.SimulationChanged += () => SetSelection(Array.Empty<int>());
        _driver.PlayerChanged += () => SetSelection(Array.Empty<int>());
    }

    public override void _Process(double delta)
    {
        // Drop villagers who died since the last frame.
        var world = _driver.Simulation?.World;
        if (world != null && _selected.RemoveAll(id => !world.TryGetEntity(id, out _)) > 0)
            SelectionChanged?.Invoke();
        if (world != null && SelectedBuilding != 0 && !world.TryGetEntity(SelectedBuilding, out _))
        {
            SelectedBuilding = 0;
            SelectionChanged?.Invoke();
        }
        _pingAge += delta;
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_driver.Simulation == null) return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } when _placingRally:
                _placingRally = false;
                var spot = _map.LocalToTile(GetGlobalMousePosition());
                _driver.Simulation.Enqueue(new SetRallyPoint(spot.X, spot.Y) { Player = _driver.Player });
                (_pingAt, _pingColor, _pingAge) = (GetGlobalMousePosition(), Art.PlayerColor(_driver.Player), 0);
                break;
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
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right:
                _placingRally = false;
                IssueOrder(right.CtrlPressed);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                _placingRally = false;
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

    /// <summary>Selects the player's first building of a type (for launch options and screenshots).</summary>
    public void SelectFirstBuilding(string id)
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        int kind = world.Content.BuildingIndex(id);
        foreach (var e in World.InIdOrder(world.Store.Query<Building, Owner>()))
        {
            if (e.GetComponent<Building>().Kind != kind || !IsMine(e)) continue;
            SetSelection(Array.Empty<int>());
            SelectedBuilding = e.Id;
            SelectionChanged?.Invoke();
            return;
        }
    }

    /// <summary>Selects all of the player's soldiers.</summary>
    public void SelectSoldiers()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        SetSelection(world.Store.Query<Soldier, Owner>().Entities.Where(IsMine).Select(e => e.Id).ToList());
    }

    /// <summary>The next left click on the map places the rally point, where new soldiers gather.</summary>
    public void BeginRallyPoint() => _placingRally = true;

    /// <summary>Whether a click on the map is awaited to place the rally point.</summary>
    public bool PlacingRally => _placingRally;

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
            if (add) return;
            SetSelection(Array.Empty<int>());
            if (BuildingUnderMouse(world) is { } building && IsMine(building))
            {
                SelectedBuilding = building.Id;
                SelectionChanged?.Invoke();
            }
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

    private void IssueOrder(bool ctrl)
    {
        var sim = _driver.Simulation!;
        var world = sim.World;
        var units = _selected.Where(id => world.TryGetEntity(id, out var e) && Bands.IsAdult(world, e.GetComponent<Citizen>())).ToArray();
        if (units.Length == 0) return;
        var soldiers = units.Where(id => world.TryGetEntity(id, out var e) && e.HasComponent<Soldier>()).ToArray();
        if (soldiers.Length > 0 && IssueMilitaryOrder(sim, soldiers, ctrl))
        {
            units = units.Except(soldiers).ToArray(); // civilians in the selection get the ordinary order below
            if (units.Length == 0) return;
        }

        var target = Pick(world, e => e.HasComponent<Animal>() || e.HasComponent<Carcass>() || e.HasComponent<Plant>()
            || e.HasComponent<Deposit>() || (e.HasComponent<Camp>() && IsMine(e)));
        var building = target == null ? BuildingUnderMouse(world) : null;
        var tile = _map.LocalToTile(GetGlobalMousePosition());
        Command command;
        if (target is { } t && t.HasComponent<Animal>())
            (command, _pingColor) = (new Hunt(units, t.Id), new Color("#ff6a5a"));
        else if (target is { } c && c.HasComponent<Camp>())
            (command, _pingColor) = (new ReturnToCamp(units), new Color("#ffffff"));
        else if (target is { } f && Sources.HasGoods(f))
            (command, _pingColor) = (new Gather(units, f.Id), new Color("#ffd24a"));
        else if (building is { } site && IsMine(site) && (!Buildings.IsComplete(site) || site.GetComponent<Building>().Damage > 0))
            (command, _pingColor) = (new Build(units, site.Id), new Color("#f2c94c"));
        else if (building is { } work && IsMine(work) && Buildings.TypeOf(world, work).IsWorkplace)
            (command, _pingColor) = (new AssignWork(units, work.Id), new Color("#9ad0ff"));
        else if (Sources.TileInfo(world, tile.X, tile.Y) != null)
            (command, _pingColor) = (new GatherTile(units, tile.X, tile.Y), new Color("#b7e07a"));
        else
            (command, _pingColor) = (new MoveUnits(units, tile.X, tile.Y) { Formation = Formation }, new Color("#7dff8a"));
        sim.Enqueue(command with { Player = _driver.Player });
        _pingAt = target is { } pinged ? _view.WorldPosition(pinged) : GetGlobalMousePosition();
        _pingAge = 0;
    }

    // Orders for the selected soldiers: attack an enemy under the mouse, loot ruins (or, with Ctrl, an enemy store or home),
    // attack-move with Ctrl over open ground. Returns false when the click means an ordinary move.
    private bool IssueMilitaryOrder(Simulation sim, int[] soldiers, bool ctrl)
    {
        var world = sim.World;
        int me = _driver.Player;
        var tile = _map.LocalToTile(GetGlobalMousePosition());
        var enemy = Pick(world, e => (e.HasComponent<Citizen>() || e.HasComponent<Merchant>()) && Combat.IsEnemy(world, me, e));
        var pile = enemy == null ? Pick(world, e => e.HasComponent<LootPile>()) : null;
        var building = enemy == null && pile == null ? BuildingUnderMouse(world) : null;
        var camp = enemy == null && pile == null && building == null ? Pick(world, e => e.HasComponent<Camp>() && !IsMine(e)) : null;
        Command command;
        if (pile is { } p)
            (command, _pingColor) = (new Loot(soldiers, p.Id), new Color("#f2c94c"));
        else if (ctrl && (building ?? camp) is { } store && Combat.CanLoot(world, me, store))
            (command, _pingColor) = (new Loot(soldiers, store.Id), new Color("#f2c94c"));
        else if ((enemy ?? building) is { } foe && Combat.IsEnemy(world, me, foe))
            (command, _pingColor) = (new Attack(soldiers, foe.Id), new Color("#ff4a3a"));
        else if (ctrl)
            (command, _pingColor) = (new AttackMove(soldiers, tile.X, tile.Y) { Formation = Formation }, new Color("#ff8a5a"));
        else
            return false;
        sim.Enqueue(command with { Player = me });
        _pingAt = (enemy ?? pile ?? building ?? camp) is { } at ? _view.WorldPosition(at) : GetGlobalMousePosition();
        _pingAge = 0;
        return true;
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

    /// <summary>The building whose footprint is under the mouse, if any.</summary>
    public Entity? BuildingUnderMouse(World world)
    {
        var tile = _map.LocalToTile(GetGlobalMousePosition());
        foreach (var e in world.Store.Query<Building, TilePosition>().Entities)
        {
            var pos = e.GetComponent<TilePosition>();
            int size = Buildings.SizeOf(world, e);
            if (tile.X >= pos.X && tile.X < pos.X + size && tile.Y >= pos.Y && tile.Y < pos.Y + size) return e;
        }
        return null;
    }

    private Vector2 BodyCenter(Entity e) => _view.WorldPosition(e) + new Vector2(0, e.HasComponent<Camp>() ? -8 : -10);

    private bool IsMine(Entity e) => e.TryGetComponent<Owner>(out var owner) && owner.Player == _driver.Player;

    private void SetSelection(IEnumerable<int> ids)
    {
        var next = ids.Distinct().OrderBy(id => id).ToList();
        bool hadBuilding = SelectedBuilding != 0;
        SelectedBuilding = 0;
        if (next.SequenceEqual(_selected))
        {
            if (hadBuilding) SelectionChanged?.Invoke();
            return;
        }
        _selected.Clear();
        _selected.AddRange(next);
        SelectionChanged?.Invoke();
    }
}
