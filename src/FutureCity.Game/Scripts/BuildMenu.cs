using System.Collections.Generic;
using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The build menu and building placement. A button per building type (disabled, with what is missing, until the
/// building is available); choosing one shows a ghost footprint under the mouse, green where it can be placed and
/// red where not. Left click places it (shift keeps placing), right click or Esc cancels.
/// </summary>
public partial class BuildMenu : Node2D
{
    private static readonly Color Good = new(0.4f, 1f, 0.45f, 0.45f);
    private static readonly Color Bad = new(1f, 0.3f, 0.25f, 0.45f);

    private SimulationDriver _driver = null!;
    private MapView _map = null!;
    private EntityView _view = null!;
    private readonly List<Button> _buttons = new();
    private int _placing = -1;
    private Vector2I _at;
    private Placement _placement;

    /// <summary>Raised to show a short message (e.g. why a building cannot go somewhere).</summary>
    public event System.Action<string>? Message;

    /// <summary>Whether a building is being placed (the mouse belongs to the menu meanwhile).</summary>
    public bool IsPlacing => _placing >= 0;

    /// <summary>Connects the menu to the game.</summary>
    public void Initialize(SimulationDriver driver, MapView map, EntityView view)
    {
        _driver = driver;
        _map = map;
        _view = view;
        BuildLayout();
        _driver.Ticked += () => { if (_driver.Simulation!.World.Tick % 10 == 0) Refresh(); };
        _driver.SimulationChanged += () => { Cancel(); Refresh(); };
    }

    /// <summary>Starts placing a building type (for the launch options and tests).</summary>
    public void BeginPlacing(string id)
    {
        int kind = _driver.Simulation?.World.Content.BuildingIndex(id) ?? -1;
        if (kind >= 0) _placing = kind;
    }

    private void BuildLayout()
    {
        var layer = new CanvasLayer { Name = "BuildLayer" };
        AddChild(layer);
        var panel = new PanelContainer { Name = "BuildPanel" };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.Position += new Vector2(-8, -32);
        layer.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        column.AddChild(new Label { Text = "Build", HorizontalAlignment = HorizontalAlignment.Center });
        var grid = new GridContainer { Columns = 2 };
        column.AddChild(grid);
        _driver.SimulationChanged += () =>
        {
            if (_buttons.Count > 0) return;
            var content = _driver.Simulation!.World.Content;
            foreach (var type in content.Buildings)
            {
                int kind = type.Index;
                var button = new Button { Text = type.Def.Name, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0) };
                button.Pressed += () => _placing = kind;
                _buttons.Add(button);
                grid.AddChild(button);
            }
            Refresh();
        };
    }

    // Enables the buttons whose requirements hold and explains the rest in their tooltips.
    private void Refresh()
    {
        var world = _driver.Simulation?.World;
        if (world == null || _buttons.Count == 0) return;
        var facts = Civics.FactsOf(world, Players.Human);
        for (int i = 0; i < _buttons.Count; i++)
        {
            var type = world.Content.Buildings[i];
            bool available = type.Requires.IsMet(facts);
            _buttons[i].Disabled = !available;
            _buttons[i].TooltipText = Describe(world, type) + (available ? ""
                : "\n\nNot yet available:\n" + string.Join("\n", Civics.Explain(world, type.Requires, facts)
                    .Where(p => !p.Met).Select(p => $"  {p.Label}: {p.Current} / {p.Target}")));
        }
        if (_placing >= 0 && !world.Content.Buildings[_placing].Requires.IsMet(facts)) Cancel();
    }

    /// <summary>A short description of a building type: cost and what it does.</summary>
    public static string Describe(World world, BuildingType type)
    {
        var content = world.Content;
        var lines = new List<string> { type.Def.Name };
        string Goods(IReadOnlyList<int> amounts) =>
            string.Join(", ", amounts.Select((n, g) => (n, g)).Where(x => x.n > 0).Select(x => $"{x.n} {content.Goods[x.g].Name.ToLowerInvariant()}"));
        lines.Add("Cost: " + (type.Cost.Sum() == 0 ? "nothing" : Goods(type.Cost)));
        if (type.Def.Shelter > 0) lines.Add($"Houses {type.Def.Shelter} people");
        if (type.Def.Storage) lines.Add("Store: people drop goods off here, so put it near their work");
        if (type.Def.Research > 0) lines.Add($"Up to {type.Def.Workers} people research here, toward the chosen discovery");
        if (type.Def.Recipe != null) lines.Add($"Makes {Goods(type.Outputs)} from {Goods(type.Inputs)} ({type.Def.Workers} worker)");
        if (type.Def.Field != null)
            lines.Add($"Grows {content.Goods[type.FieldGood].Name.ToLowerInvariant()}: sown in spring or summer, harvested when ripe " +
                      $"({type.Def.Workers} workers). Better soil, bigger harvests; every harvest tires the soil.");
        return string.Join("\n", lines);
    }

    public override void _Process(double delta)
    {
        if (_placing < 0 || _driver.Simulation == null) return;
        var world = _driver.Simulation.World;
        int size = world.Content.Buildings[_placing].Def.Size;
        var tile = _map.LocalToTile(GetGlobalMousePosition());
        _at = new Vector2I(tile.X - (size - 1) / 2, tile.Y - (size - 1) / 2);
        _placement = Buildings.CanPlace(world, Players.Human, _placing, _at.X, _at.Y);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_placing < 0 || _driver.Simulation == null) return;
        var world = _driver.Simulation.World;
        int size = world.Content.Buildings[_placing].Def.Size;
        var corners = _view.Footprint(_at.X, _at.Y, size);
        var color = _placement == Placement.Ok ? Good : Bad;
        DrawColoredPolygon(corners, color);
        DrawPolyline([.. corners, corners[0]], color with { A = 0.9f }, 1.5f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_placing < 0 || _driver.Simulation == null) return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                Place(click.ShiftPressed);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                Cancel();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left }:
                break; // swallow the release so the selection does not see a click
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Place(bool keepPlacing)
    {
        var world = _driver.Simulation!.World;
        if (_placement != Placement.Ok)
        {
            Message?.Invoke(_placement switch
            {
                Placement.BadTerrain => "Cannot build there: the ground is not suitable",
                Placement.Occupied => "Cannot build there: something is in the way",
                Placement.Unreachable => "Cannot build there: your people cannot get there",
                Placement.OffMap => "Cannot build there",
                _ => "Not available yet",
            });
            return;
        }
        _driver.Simulation.Enqueue(new PlaceBuilding(world.Content.Buildings[_placing].Def.Id, _at.X, _at.Y) { Player = Players.Human });
        if (!keepPlacing) Cancel();
    }

    private void Cancel()
    {
        _placing = -1;
        QueueRedraw();
    }
}
