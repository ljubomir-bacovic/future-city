using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Top bar (time, speed controls, food and people), the selected villagers panel and transient messages.
/// Era and treasury join the bar as those systems are built (progressive disclosure).
/// </summary>
public partial class Hud : CanvasLayer
{
    private SimulationDriver _driver = null!;
    private SelectionController _selection = null!;
    private Label _timeLabel = null!;
    private Label _foodLabel = null!;
    private Label _peopleLabel = null!;
    private PanelContainer _selectionPanel = null!;
    private Label _selectionLabel = null!;
    private Label _gameLabel = null!;
    private Label _toast = null!;
    private Button _pauseButton = null!;
    private readonly Button[] _speedButtons = new Button[SimulationDriver.MaxSpeed];
    private Tween? _toastTween;

    /// <summary>Connects the HUD to the driver it displays and controls and to the selection it describes.</summary>
    public void Initialize(SimulationDriver driver, SelectionController selection)
    {
        _driver = driver;
        _selection = selection;
        BuildLayout();
        _driver.Ticked += OnTicked;
        _driver.SimulationChanged += RefreshAll;
        _driver.TimeControlsChanged += RefreshTimeControls;
        _selection.SelectionChanged += RefreshSelection;
        RefreshAll();
    }

    /// <summary>Shows a short message under the top bar that fades out.</summary>
    public void ShowMessage(string text)
    {
        _toast.Text = text;
        _toast.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(2.0);
        _toastTween.TweenProperty(_toast, "modulate:a", 0.0, 0.6);
    }

    private void BuildLayout()
    {
        var bar = new PanelContainer { Name = "TopBar" };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        bar.AddChild(row);

        row.AddChild(new Label { Text = "Future City - Origins" });
        row.AddChild(new VSeparator());
        _timeLabel = new Label { CustomMinimumSize = new Vector2(150, 0) };
        row.AddChild(_timeLabel);

        _pauseButton = MakeButton("Pause", () => _driver.SetPaused(!_driver.Paused));
        row.AddChild(_pauseButton);
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            int speed = i + 1;
            _speedButtons[i] = MakeButton($"{speed}×", () => _driver.SetSpeed(speed));
            _speedButtons[i].ToggleMode = true;
            row.AddChild(_speedButtons[i]);
        }

        row.AddChild(new VSeparator());
        _foodLabel = new Label { CustomMinimumSize = new Vector2(90, 0), TooltipText = "Food in the shared store at camp" };
        _foodLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        row.AddChild(_foodLabel);
        _peopleLabel = new Label { TooltipText = "People in the band (children in brackets) / people the camp can shelter" };
        _peopleLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        row.AddChild(_peopleLabel);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _gameLabel = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
        row.AddChild(_gameLabel);

        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = Colors.Transparent };
        _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(0, 48);
        _toast.GrowHorizontal = Control.GrowDirection.Both;
        AddChild(_toast);

        _selectionPanel = new PanelContainer { Visible = false };
        _selectionPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _selectionPanel.Position += new Vector2(8, -40);
        _selectionPanel.GrowVertical = Control.GrowDirection.Begin;
        _selectionLabel = new Label();
        _selectionPanel.AddChild(_selectionLabel);
        AddChild(_selectionPanel);

        var help = new Label
        {
            Text = "Click / drag to select villagers · right-click: animal = hunt, bush = gather, camp = return, ground = move · " +
                   "Space pause · 1–4 speed · WASD / edge / middle-drag pan · wheel zoom · F5 save · F9 load",
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        help.Position += new Vector2(8, -8);
        help.GrowVertical = Control.GrowDirection.Begin;
        AddChild(help);
    }

    private static Button MakeButton(string text, System.Action onPressed)
    {
        // FocusMode None so Space/keys never "click" a focused button.
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(44, 0) };
        button.Pressed += onPressed;
        return button;
    }

    private void OnTicked()
    {
        RefreshTime();
        RefreshBand();
        if (_selection.Selected.Count > 0) RefreshSelection();
        foreach (var e in _driver.Simulation!.World.Events)
        {
            if (e.Player != Players.Human) continue;
            string? message = e.Kind switch
            {
                SimEventKind.Birth => "A child was born",
                SimEventKind.DiedOfStarvation => "A villager starved to death",
                SimEventKind.DiedOfOldAge => "A villager died of old age",
                _ => null,
            };
            if (message != null) ShowMessage(message);
        }
    }

    private void RefreshBand()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        var census = Bands.CensusOf(world, Players.Human);
        bool hasCamp = Bands.TryGetCamp(world, Players.Human, out var camp);
        _foodLabel.Text = $"Food {(hasCamp ? camp.GetComponent<Camp>().Food : 0)}";
        string children = census.Children > 0 ? $" ({census.Children})" : "";
        _peopleLabel.Text = $"People {census.Total}{children} / {(hasCamp ? camp.GetComponent<Camp>().Shelter : 0)}";
    }

    private void RefreshSelection()
    {
        var world = _driver.Simulation?.World;
        var selected = _selection.Selected;
        _selectionPanel.Visible = world != null && selected.Count > 0;
        if (!_selectionPanel.Visible) return;

        var rules = world!.Content.Citizens;
        if (selected.Count == 1 && world.TryGetEntity(selected[0], out var one))
        {
            var c = one.GetComponent<Citizen>();
            bool adult = Bands.IsAdult(world, c);
            string doing = adult ? Describe(one.GetComponent<Order>()) : "Too young to work";
            _selectionLabel.Text = $"{(adult ? "Villager" : "Child")}, age {Bands.AgeInYears(world, c)}  ·  {doing}\n" +
                                   $"Health {c.Health * 100 / rules.MaxHealth}%  ·  Hunger {c.Hunger * 100 / rules.MaxHunger}%" +
                                   (c.CarriedFood > 0 ? $"  ·  Carrying {c.CarriedFood} food" : "");
            return;
        }
        var orders = selected
            .Select(id => !world.TryGetEntity(id, out var e) ? null
                : Bands.IsAdult(world, e.GetComponent<Citizen>()) ? Describe(e.GetComponent<Order>()) : "Children")
            .Where(d => d != null)
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} {g.Key!.ToLowerInvariant()}");
        _selectionLabel.Text = $"{selected.Count} people selected\n{string.Join("  ·  ", orders)}";
    }

    private static string Describe(Order order) => order.Kind switch
    {
        OrderKind.Move => "Walking",
        OrderKind.Gather when order.Stage == OrderStage.Deliver => "Carrying food to camp",
        OrderKind.Gather => order.TargetIsPlant ? "Gathering berries" : "Butchering",
        OrderKind.Hunt => "Hunting",
        OrderKind.ReturnToCamp => "Returning to camp",
        _ => "Idle",
    };

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTimeControls();
        RefreshBand();
        RefreshSelection();
        var setup = _driver.Simulation?.World.Setup;
        _gameLabel.Text = setup == null ? "" : $"Map {setup.MapSize} · Seed {setup.Seed}";
    }

    private void RefreshTime()
    {
        long tick = _driver.Simulation?.World.Tick ?? 0;
        long seconds = SimClock.ToSeconds(tick);
        _timeLabel.Text = $"Time {seconds / 60:00}:{seconds % 60:00}  ·  Tick {tick}";
    }

    private void RefreshTimeControls()
    {
        _pauseButton.Text = _driver.Paused ? "Resume" : "Pause";
        for (int i = 0; i < _speedButtons.Length; i++)
            _speedButtons[i].SetPressedNoSignal(!_driver.Paused && _driver.Speed == i + 1);
    }
}
