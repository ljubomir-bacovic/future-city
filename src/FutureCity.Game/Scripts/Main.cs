using System;
using System.Globalization;
using System.IO;
using FutureCity.Content;
using FutureCity.Sim;
using FutureCity.Sim.Content;
using FutureCity.Sim.Persistence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Root of the game scene: loads content, starts a game and wires the view, camera, HUD and saving together.
/// Command-line options (after "--"): --seed=N, --map=ID, --zoom=F, --screenshot=PATH, --frames=N.
/// </summary>
public partial class Main : Node2D
{
    private static readonly long AutosaveIntervalTicks = SimClock.FromSeconds(120);

    private ContentDatabase _content = null!;
    private SimulationDriver _driver = null!;
    private MapView _mapView = null!;
    private RtsCamera _camera = null!;
    private Hud _hud = null!;
    private LaunchOptions _options = null!;
    private int _frame;

    private static string SaveDirectory => ProjectSettings.GlobalizePath("user://saves");
    private static string QuickSavePath => Path.Combine(SaveDirectory, "quicksave" + SaveGame.FileExtension);
    private static string AutosavePath => Path.Combine(SaveDirectory, "autosave" + SaveGame.FileExtension);

    public override void _Ready()
    {
        InputActions.Register();
        RenderingServer.SetDefaultClearColor(new Color("#1d2329"));
        _options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        _content = ContentLoader.Load(GameContent.ReadAll());

        _driver = new SimulationDriver { Name = "SimulationDriver" };
        _mapView = new MapView { Name = "MapView" };
        _camera = new RtsCamera { Name = "Camera", EdgeScrollEnabled = _options.ScreenshotPath == null };
        _hud = new Hud { Name = "Hud" };
        AddChild(_driver);
        AddChild(_mapView);
        AddChild(_camera);
        AddChild(_hud);
        _camera.MakeCurrent();

        _driver.SimulationChanged += OnSimulationChanged;
        _driver.Ticked += OnTicked;
        _hud.Initialize(_driver);

        _driver.Start(Simulation.NewGame(_content, new GameSetup
        {
            Seed = _options.Seed ?? (ulong)System.Random.Shared.NextInt64(),
            MapSize = _options.MapSize ?? _content.Rules.DefaultMapSize,
        }));
    }

    public override void _Process(double delta)
    {
        if (_options.ScreenshotPath != null && ++_frame == _options.ScreenshotFrames)
            TakeScreenshotAndQuit(_options.ScreenshotPath);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.TogglePause)) _driver.SetPaused(!_driver.Paused);
        else if (@event.IsActionPressed(InputActions.Speed1)) _driver.SetSpeed(1);
        else if (@event.IsActionPressed(InputActions.Speed2)) _driver.SetSpeed(2);
        else if (@event.IsActionPressed(InputActions.Speed3)) _driver.SetSpeed(3);
        else if (@event.IsActionPressed(InputActions.Speed4)) _driver.SetSpeed(4);
        else if (@event.IsActionPressed(InputActions.SpeedUp)) _driver.SetSpeed(_driver.Speed + 1);
        else if (@event.IsActionPressed(InputActions.SpeedDown)) _driver.SetSpeed(_driver.Speed - 1);
        else if (@event.IsActionPressed(InputActions.QuickSave)) Save(QuickSavePath, "Game saved");
        else if (@event.IsActionPressed(InputActions.QuickLoad)) Load(QuickSavePath);
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void OnSimulationChanged()
    {
        _mapView.Build(_driver.Simulation!.World);
        _camera.SetBounds(_mapView.Bounds);
        if (_options.Zoom is { } zoom)
            _camera.Zoom = new Vector2(zoom, zoom);
    }

    private void OnTicked()
    {
        if (_driver.Simulation!.World.Tick % AutosaveIntervalTicks == 0)
            Save(AutosavePath, "Autosaved");
    }

    private void Save(string path, string message)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            SaveGame.WriteFile(_driver.Simulation!, path);
            _hud.ShowMessage(message);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"Save failed: {e}");
            _hud.ShowMessage("Save failed: " + e.Message);
        }
    }

    private void Load(string path)
    {
        if (!File.Exists(path))
        {
            _hud.ShowMessage("No quicksave yet (press F5 to save)");
            return;
        }
        try
        {
            _driver.Start(SaveGame.ReadFile(path, _content));
            _hud.ShowMessage("Game loaded");
        }
        catch (Exception e) when (e is SaveGameException or IOException)
        {
            GD.PushError($"Load failed: {e}");
            _hud.ShowMessage("Load failed: " + e.Message);
        }
    }

    private async void TakeScreenshotAndQuit(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(error == Error.Ok ? $"Screenshot saved to {path}" : $"Screenshot failed: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private sealed record LaunchOptions(ulong? Seed, string? MapSize, float? Zoom, string? ScreenshotPath, int ScreenshotFrames)
    {
        public static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions(null, null, null, null, 60);
            foreach (var arg in args)
            {
                var parts = arg.Split('=', 2);
                string value = parts.Length == 2 ? parts[1] : "";
                options = parts[0] switch
                {
                    "--seed" => options with { Seed = ulong.Parse(value, CultureInfo.InvariantCulture) },
                    "--map" => options with { MapSize = value },
                    "--zoom" => options with { Zoom = float.Parse(value, CultureInfo.InvariantCulture) },
                    "--screenshot" => options with { ScreenshotPath = value },
                    "--frames" => options with { ScreenshotFrames = int.Parse(value, CultureInfo.InvariantCulture) },
                    _ => options,
                };
            }
            return options;
        }
    }
}
