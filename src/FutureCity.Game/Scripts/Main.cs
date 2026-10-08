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
/// placing a building), --select-building=ID, --civs=N (civilizations on the map; F2 switches between them),
/// --war-at=S (with --autoplay and two or more civilizations: the last one raids player 1 at game second S),
/// --military, --diplomacy (open those panels), --player=N (start controlling civilization N), --look=X,Y (centre the
/// camera on a tile).
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
    private MilitaryPanel _military = null!;
    private DiplomacyPanel _diplomacy = null!;
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
        _military = new MilitaryPanel { Name = "Military" };
        _diplomacy = new DiplomacyPanel { Name = "Diplomacy" };
        AddChild(_driver);
        AddChild(_mapView);
        AddChild(_entityView);
        AddChild(_selection);
        AddChild(_buildMenu); // after the selection, so placement gets mouse clicks first
        AddChild(_camera);
        AddChild(_hud);
        AddChild(_research);
        AddChild(_economy);
        AddChild(_military);
        AddChild(_diplomacy);
        _camera.MakeCurrent();

        _driver.SimulationChanged += OnSimulationChanged;
        _driver.Ticked += OnTicked;
        _driver.PlayerChanged += FocusOnPlayer;
        _entityView.Initialize(_driver, _mapView, _selection);
        _selection.Initialize(_driver, _entityView, _mapView);
        _research.Initialize(_driver);
        _economy.Initialize(_driver);
        _military.Initialize(_driver, _selection);
        _diplomacy.Initialize(_driver);
        _hud.Initialize(_driver, _selection, _research, _economy, _military, _diplomacy);
        _buildMenu.Initialize(_driver, _mapView, _entityView);
        _buildMenu.Message += _hud.ShowMessage;

        var sim = Simulation.NewGame(_content, new GameSetup
        {
            Seed = _options.Seed ?? (ulong)System.Random.Shared.NextInt64(),
            MapSize = _options.MapSize ?? _content.Rules.DefaultMapSize,
            Civilizations = Math.Clamp(_options.Civilizations, 1, GameSetup.MaxCivilizations),
        });
        System.Action<Simulation>? bot = null;
        for (int player = Players.Human; _options.Autoplay != null && player <= sim.World.Setup.Civilizations; player++)
        {
            bool raider = _options.WarAt is { } at && player > Players.Human && player == sim.World.Setup.Civilizations;
            System.Action<Simulation> one = raider ? new WarBot(player, Players.Human, SimClock.FromSeconds(_options.WarAt!.Value)).Act
                : _options.Autoplay == "forage" ? new ForagingBot(player).Act : new SettlerBot(player).Act;
            bot += one;
        }
        if (bot != null) _driver.BeforeStep = bot;
        for (int i = 0; i < _options.SkipTicks; i++)
        {
            bot?.Invoke(sim);
            sim.Step();
        }
        _driver.Start(sim);
        for (int i = Players.Human; i < _options.Player; i++) _driver.SwitchPlayer();
        if (_options.Military) _military.Toggle();
        if (_options.Diplomacy) _diplomacy.Toggle();
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
        else if (@event.IsActionPressed(InputActions.ToggleResearch)) { _military.Close(); _research.Toggle(); }
        else if (@event.IsActionPressed(InputActions.ToggleEconomy)) { _diplomacy.Close(); _economy.Toggle(); }
        else if (@event.IsActionPressed(InputActions.ToggleMilitary)) { if (_research.IsOpen) _research.Toggle(); _military.Toggle(); }
        else if (@event.IsActionPressed(InputActions.ToggleDiplomacy)) { if (_economy.IsOpen) _economy.Toggle(); _diplomacy.Toggle(); }
        else if (@event.IsActionPressed(InputActions.SwitchPlayer)) _driver.SwitchPlayer();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void OnSimulationChanged()
    {
        var world = _driver.Simulation!.World;
        _mapView.Build(world);
        _camera.SetBounds(_mapView.Bounds);
        FocusOnPlayer();
        if (_options.Look?.Split(',') is [var lx, var ly])
            _camera.Position = _mapView.TileToLocal(new Vector2(float.Parse(lx, CultureInfo.InvariantCulture), float.Parse(ly, CultureInfo.InvariantCulture)));
        if (_options.Zoom is { } zoom)
            _camera.Zoom = new Vector2(zoom, zoom);
    }

    // Looks at the controlled civilization's camp.
    private void FocusOnPlayer()
    {
        var world = _driver.Simulation!.World;
        if (Bands.TryGetCamp(world, _driver.Player, out var camp))
            _camera.Position = _entityView.WorldPosition(camp);
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
        bool SelectAll, string? Autoplay, int SkipTicks, bool Research = false, string? Place = null, string? SelectBuilding = null, bool Economy = false,
        int Civilizations = 1, int? WarAt = null, bool Military = false, bool Diplomacy = false, int Player = 1, string? Look = null)
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
                    "--civs" => options with { Civilizations = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--war-at" => options with { WarAt = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--military" => options with { Military = true },
                    "--diplomacy" => options with { Diplomacy = true },
                    "--player" => options with { Player = int.Parse(value, CultureInfo.InvariantCulture) },
                    "--look" => options with { Look = value },
                    _ => options,
                };
            }
            return options;
        }
    }
}
