using FutureCity.Sim;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Top bar (time, speed controls) and transient messages. Starts minimal; resources, population, era
/// and treasury join the bar as those systems are built (progressive disclosure).
/// </summary>
public partial class Hud : CanvasLayer
{
    private SimulationDriver _driver = null!;
    private Label _timeLabel = null!;
    private Label _gameLabel = null!;
    private Label _toast = null!;
    private Button _pauseButton = null!;
    private readonly Button[] _speedButtons = new Button[SimulationDriver.MaxSpeed];
    private Tween? _toastTween;

    /// <summary>Connects the HUD to the driver it displays and controls.</summary>
    public void Initialize(SimulationDriver driver)
    {
        _driver = driver;
        BuildLayout();
        _driver.Ticked += RefreshTime;
        _driver.SimulationChanged += RefreshAll;
        _driver.TimeControlsChanged += RefreshTimeControls;
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

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _gameLabel = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
        row.AddChild(_gameLabel);

        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = Colors.Transparent };
        _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(0, 48);
        _toast.GrowHorizontal = Control.GrowDirection.Both;
        AddChild(_toast);

        var help = new Label
        {
            Text = "Space pause · 1–4 speed · WASD / arrows / screen edge / middle-drag to pan · wheel zoom · F5 save · F9 load",
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

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTimeControls();
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
