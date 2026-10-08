using System;
using System.Globalization;
using System.IO;
using FutureCity.Content;
using FutureCity.Sim;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Persistence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Root of the game scene: loads content, starts a game and wires the view, camera, HUD and saving together.
/// Command-line options (after "--"): --seed=N, --map=ID, --zoom=F, --screenshot=PATH, --frames=N,
/// --select-all (select the band at start), --autoplay[=forage] (a stand-in computer player runs the band: by default
/// the settler that builds and farms), --skip=N (simulate N ticks before showing the game; useful with --autoplay for
/// screenshots), --research (open the discoveries panel), --economy (open the economy panel), --place=ID (start
/// placing a building), --select-building=ID.
/// </summary>
public partial class Main : Node2D
{
    private static readonly long AutosaveIntervalTicks = SimClock.FromSeconds(120);

    private ContentDatabase _content = null!;
    private SimulationDriver _driver = null!;
    private MapView _mapView = null!;
    private EntityView _entityView = null!;
    private SelectionController _selection = null!;
    private BuildMenu _buildMenu = null!;
    private ResearchPanel _research = null!;
    private EconomyPanel _economy = null!;
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
        _entityView = new EntityView { Name = "EntityView" };
        _selection = new SelectionController { Name = "Selection" };
        _buildMenu = new BuildMenu { Name = "BuildMenu" };
        _camera = new RtsCamera { Name = "Camera", EdgeScrollEnabled = _options.ScreenshotPath == null };
        _hud = new Hud { Name = "Hud" };
        _research = new ResearchPanel { Name = "Research" };
        _economy = new EconomyPanel { Name = "Economy" };
        AddChild(_driver);
        AddChild(_mapView);
        AddChild(_entityView);
        AddChild(_selection);
        AddChild(_buildMenu); // after the selection, so placement gets mouse clicks first
        AddChild(_camera);
        AddChild(_hud);
        AddChild(_research);
        AddChild(_economy);
        _camera.MakeCurrent();

        _driver.SimulationChanged += OnSimulationChanged;
        _driver.Ticked += OnTicked;
        _entityView.Initialize(_driver, _mapView, _selection);
        _selection.Initialize(_driver, _entityView, _mapView);
        _research.Initialize(_driver);
        _economy.Initialize(_driver);
        _hud.Initialize(_driver, _selection, _research, _economy);
        _buildMenu.Initialize(_driver, _mapView, _entityView);
        _buildMenu.Message += _hud.ShowMessage;

        System.Action<Simulation>? bot = _options.Autoplay switch
        {
            "forage" => new ForagingBot(Players.Human).Act,
            null => null,
            _ => new SettlerBot(Players.Human).Act,
        };
        if (bot != null) _driver.BeforeStep = bot;
        var sim = Simulation.NewGame(_content, new GameSetup
        {
            Seed = _options.Seed ?? (ulong)System.Random.Shared.NextInt64(),
            MapSize = _options.MapSize ?? _content.Rules.DefaultMapSize,
        });
        for (int i = 0; i < _options.SkipTicks; i++)
        {
            bot?.Invoke(sim);
            sim.Step();
        }
        _driver.Start(sim);
        if (_options.SelectAll)
            _selection.SelectAllOwn();
        if (_options.Research)
            _research.Toggle();
        if (_options.Economy)
            _economy.Toggle();
        if (_options.Place != null)
            _buildMenu.BeginPlacing(_options.Place);
        if (_options.SelectBuilding != null)
            _selection.SelectFirstBuilding(_options.SelectBuilding);
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
        else if (@event.IsActionPressed(InputActions.ToggleResearch)) _research.Toggle();
        else if (@event.IsActionPressed(InputActions.ToggleEconomy)) _economy.Toggle();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void OnSimulationChanged()
    {
        var world = _driver.Simulation!.World;
        _mapView.Build(world);
        _camera.SetBounds(_mapView.Bounds);
        if (Bands.TryGetCamp(world, Players.Human, out var camp))
            _camera.Position = _entityView.WorldPosition(camp);
        if (_options.Zoom is { } zoom)
            _camera.Zoom = new Vector2(zoom, zoom);
    }

    private void OnTicked()
    {
        _mapView.SyncTerrain(_driver.Simulation!.World);
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

    private sealed record LaunchOptions(ulong? Seed, string? MapSize, float? Zoom, string? ScreenshotPath, int ScreenshotFrames,
        bool SelectAll, string? Autoplay, int SkipTicks, bool Research = false, string? Place = null, string? SelectBuilding = null, bool Economy = false)
    {
        public static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions(null, null, null, null, 60, false, null, 0);
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
                    "--select-all" => options with { SelectAll = true },
                    "--autoplay" => options with { Autoplay = value.Length > 0 ? value : "settle" },
                    "--research" => options with { Research = true },
                    "--economy" => options with { Economy = true },
                    "--place" => options with { Place = value },
                    "--select-building" => options with { SelectBuilding = value },
                    "--skip" => options with { SkipTicks = int.Parse(value, CultureInfo.InvariantCulture) },
                    _ => options,
                };
            }
            return options;
        }
    }
}
