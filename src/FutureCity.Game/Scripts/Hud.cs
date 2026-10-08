using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Top bar (era, season, speed controls, goods and people), the selection panel (villagers or a building),
/// transient messages and the era banner. The treasury joins the bar when money exists (progressive disclosure).
/// </summary>
public partial class Hud : CanvasLayer
{
    private SimulationDriver _driver = null!;
    private SelectionController _selection = null!;
    private Label _timeLabel = null!;
    private Label _foodLabel = null!;
    private Label _peopleLabel = null!;
    private Label _goodsLabel = null!;
    private Label _seasonLabel = null!;
    private Button _eraButton = null!;
    private Label _banner = null!;
    private Tween? _bannerTween;
    private ResearchPanel _research = null!;
    private PanelContainer _selectionPanel = null!;
    private Label _selectionLabel = null!;
    private Label _gameLabel = null!;
    private Label _toast = null!;
    private Button _pauseButton = null!;
    private readonly Button[] _speedButtons = new Button[SimulationDriver.MaxSpeed];
    private Tween? _toastTween;

    /// <summary>Connects the HUD to the driver it displays and controls and to the selection it describes.</summary>
    public void Initialize(SimulationDriver driver, SelectionController selection, ResearchPanel research)
    {
        _driver = driver;
        _selection = selection;
        _research = research;
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

        _eraButton = MakeButton("", () => _research.Toggle());
        _eraButton.TooltipText = "Era and discoveries (R)";
        row.AddChild(_eraButton);
        _seasonLabel = new Label { CustomMinimumSize = new Vector2(110, 0) };
        row.AddChild(_seasonLabel);
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
        _foodLabel = new Label { CustomMinimumSize = new Vector2(90, 0), TooltipText = "Meals in the shared stores (all foods by their food value)" };
        _foodLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        row.AddChild(_foodLabel);
        _peopleLabel = new Label { TooltipText = "People (children in brackets) / people the camp and huts can shelter" };
        _peopleLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        row.AddChild(_peopleLabel);
        _goodsLabel = new Label { TooltipText = "Goods in the shared stores" };
        _goodsLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        row.AddChild(_goodsLabel);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _gameLabel = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
        row.AddChild(_gameLabel);

        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = Colors.Transparent };
        _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _toast.Position += new Vector2(0, 48);
        _toast.GrowHorizontal = Control.GrowDirection.Both;
        AddChild(_toast);

        _banner = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = Colors.Transparent };
        _banner.AddThemeFontSizeOverride("font_size", 40);
        _banner.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _banner.Position += new Vector2(0, 120);
        _banner.GrowHorizontal = Control.GrowDirection.Both;
        AddChild(_banner);

        _selectionPanel = new PanelContainer { Visible = false };
        _selectionPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _selectionPanel.Position += new Vector2(8, -40);
        _selectionPanel.GrowVertical = Control.GrowDirection.Begin;
        _selectionLabel = new Label();
        _selectionPanel.AddChild(_selectionLabel);
        AddChild(_selectionPanel);

        var help = new Label
        {
            Text = "Right-click with villagers: hunt · gather · cut wood · build · work · move   |   " +
                   "R discoveries · Space pause · 1–4 speed · F5 / F9 save / load",
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        help.Position += new Vector2(8, -8);
        help.GrowVertical = Control.GrowDirection.Begin;
        AddChild(help);
    }

    /// <summary>Shows a large banner that fades out, for big moments such as a new era.</summary>
    public void ShowBanner(string text)
    {
        _banner.Text = text;
        _banner.Modulate = Colors.White;
        _bannerTween?.Kill();
        _bannerTween = CreateTween();
        _bannerTween.TweenInterval(3.5);
        _bannerTween.TweenProperty(_banner, "modulate:a", 0.0, 1.2);
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
        if (_selection.Selected.Count > 0 || _selection.SelectedBuilding != 0) RefreshSelection();
        var content = _driver.Simulation!.World.Content;
        foreach (var e in _driver.Simulation!.World.Events)
        {
            if (e.Player != Players.Human) continue;
            if (e.Kind == SimEventKind.EraReached)
            {
                ShowBanner($"The {content.Eras[e.Detail].Def.Name} begin");
                continue;
            }
            string? message = e.Kind switch
            {
                SimEventKind.Birth => "A child was born",
                SimEventKind.DiedOfStarvation => "A villager starved to death",
                SimEventKind.DiedOfOldAge => "A villager died of old age",
                SimEventKind.BuildingCompleted => $"{content.Buildings[e.Detail].Def.Name} finished",
                SimEventKind.TechDiscovered => $"Discovery: {content.Techs[e.Detail].Def.Name}! (R for details)",
                SimEventKind.InstitutionEstablished => $"{content.Institutions[e.Detail].Def.Name} established: idle people now find work themselves",
                SimEventKind.CropsRotted => "A harvest rotted in the field",
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
        var stock = Stores.Totals(world, Players.Human);
        _foodLabel.Text = $"Food {Stores.MealsIn(world, stock)}";
        string children = census.Children > 0 ? $" ({census.Children})" : "";
        _peopleLabel.Text = $"People {census.Total}{children} / {Buildings.ShelterOf(world, Players.Human)}";
        // Non-food goods appear once the band has any (progressive disclosure).
        _goodsLabel.Text = string.Join("  ", Enumerable.Range(0, stock.Length)
            .Where(g => world.Content.Goods[g].Nutrition == 0 && stock[g] > 0)
            .Select(g => $"{world.Content.Goods[g].Name} {stock[g]}"));
        _eraButton.Text = Civics.EraOf(world, Players.Human).Def.Name;
        _seasonLabel.Text = $"{Calendar.Season(world).Name}, year {Calendar.Year(world)}";
    }

    private void RefreshSelection()
    {
        var world = _driver.Simulation?.World;
        var selected = _selection.Selected;
        _selectionPanel.Visible = world != null && (selected.Count > 0 || _selection.SelectedBuilding != 0);
        if (!_selectionPanel.Visible) return;
        if (selected.Count == 0 && world!.TryGetEntity(_selection.SelectedBuilding, out var building))
        {
            _selectionLabel.Text = DescribeBuilding(world, building);
            return;
        }

        var rules = world!.Content.Citizens;
        if (selected.Count == 1 && world.TryGetEntity(selected[0], out var one))
        {
            var c = one.GetComponent<Citizen>();
            bool adult = Bands.IsAdult(world, c);
            string doing = adult ? Describe(one.GetComponent<Order>()) : "Too young to work";
            _selectionLabel.Text = $"{(adult ? "Villager" : "Child")}, age {Bands.AgeInYears(world, c)}  ·  {doing}\n" +
                                   $"Health {c.Health * 100 / rules.MaxHealth}%  ·  Hunger {c.Hunger * 100 / rules.MaxHunger}%" +
                                   (c.Carried > 0 ? $"  ·  Carrying {c.Carried} {world.Content.Goods[c.CarriedGood].Name.ToLowerInvariant()}" : "");
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

    private static string Describe(Order order) => (order.Kind, order.Stage) switch
    {
        (OrderKind.Move, _) => "Walking",
        (_, OrderStage.Deliver) => "Carrying goods to a store",
        (_, OrderStage.Fetch) => "Fetching materials",
        (_, OrderStage.Supply) => "Bringing materials",
        (OrderKind.Gather, _) => order.TargetType switch
        {
            TargetType.Carcass => "Butchering",
            TargetType.Tile => "Cutting wood",
            TargetType.Deposit => "Quarrying",
            _ => "Gathering",
        },
        (OrderKind.Hunt, _) => "Hunting",
        (OrderKind.ReturnToCamp, _) => "Returning to camp",
        (OrderKind.Build, _) => "Building",
        (OrderKind.Work, _) => "Working",
        _ => "Idle",
    };

    private static string DescribeBuilding(World world, Friflo.Engine.ECS.Entity building)
    {
        var type = Buildings.TypeOf(world, building);
        var lines = new System.Collections.Generic.List<string>();
        if (!Buildings.IsComplete(building))
        {
            var missing = Buildings.MissingMaterials(world, building);
            string needs = string.Join(", ", missing.Select((n, g) => (n, g)).Where(x => x.n > 0)
                .Select(x => $"{x.n} {world.Content.Goods[x.g].Name.ToLowerInvariant()}"));
            lines.Add($"{type.Def.Name} (construction site, {Buildings.ConstructionPercent(world, building)}%)");
            lines.Add(needs.Length > 0 ? $"Still needs {needs}" : "All materials delivered");
            lines.Add($"{Buildings.WorkersAt(world, building.Id)} builders · right-click with villagers selected to build");
            return string.Join("\n", lines);
        }
        lines.Add(type.Def.Name);
        if (type.IsWorkplace) lines.Add($"Workers {Buildings.WorkersAt(world, building.Id)} / {type.Def.Workers}");
        if (type.Def.Shelter > 0) lines.Add($"Houses {type.Def.Shelter} people");
        if (building.TryGetComponent<Field>(out var field))
        {
            string stage = field.Stage switch
            {
                FieldStage.Growing => $"Growing ({field.Progress * 100 / (type.Def.Field!.GrowTicks * Labor.PerTick)}%)",
                FieldStage.Ripe => $"Ripe: {field.Remaining} to harvest",
                _ => Calendar.Season(world).Sowing ? "Fallow, ready to sow" : "Fallow until spring",
            };
            lines.Add($"{stage} · soil {field.Fertility}% (natural {field.NaturalFertility}%)");
        }
        var stock = building.GetComponent<Inventory>().Amounts;
        string held = string.Join(", ", stock.Select((n, g) => (n, g)).Where(x => x.n > 0)
            .Select(x => $"{x.n} {world.Content.Goods[x.g].Name.ToLowerInvariant()}"));
        if (held.Length > 0) lines.Add((Stores.IsStore(world, building) ? "Holds " : "Here: ") + held);
        return string.Join("\n", lines);
    }

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
