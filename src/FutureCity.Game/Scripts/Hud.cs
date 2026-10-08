using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Top bar (whose civilization this is, era, season, speed controls, goods and people), the selection panel (villagers,
/// soldiers or a building), transient messages and the era banner. The economy button, the treasury, prices and
/// happiness join the bar as the economy develops (progressive disclosure), the army count once there are soldiers, and
/// the diplomacy button when there is another civilization.
/// </summary>
public partial class Hud : CanvasLayer
{
    private SimulationDriver _driver = null!;
    private SelectionController _selection = null!;
    private Label _timeLabel = null!;
    private Label _foodLabel = null!;
    private Label _peopleLabel = null!;
    private HBoxContainer _goodsRow = null!;
    private readonly System.Collections.Generic.Dictionary<int, (HBoxContainer Row, Label Value)> _goods = [];
    private Label _seasonLabel = null!;
    private TextureRect _seasonIcon = null!;
    private Button _eraButton = null!;
    private Label _banner = null!;
    private Tween? _bannerTween;
    private ResearchPanel _research = null!;
    private EconomyPanel _economy = null!;
    private MilitaryPanel _military = null!;
    private DiplomacyPanel _diplomacy = null!;
    private Button _playerBadge = null!, _diplomacyButton = null!;
    private Button _armyButton = null!;
    private HBoxContainer _soldierActions = null!;
    private Button _lineButton = null!, _columnButton = null!;
    private Button _economyButton = null!;
    private HBoxContainer _coinsRow = null!, _pricesRow = null!, _moodRow = null!;
    private Label _coinsLabel = null!, _pricesLabel = null!, _moodLabel = null!;
    private PanelContainer _selectionPanel = null!;
    private Label _selectionLabel = null!;
    private RichTextLabel _selectionGoods = null!;
    private Label _toast = null!;
    private Button _pauseButton = null!;
    private readonly Button[] _speedButtons = new Button[SimulationDriver.MaxSpeed];
    private Tween? _toastTween;

    /// <summary>Connects the HUD to the driver it displays and controls and to the selection it describes.</summary>
    public void Initialize(SimulationDriver driver, SelectionController selection, ResearchPanel research, EconomyPanel economy,
        MilitaryPanel military, DiplomacyPanel diplomacy)
    {
        _driver = driver;
        _selection = selection;
        _research = research;
        _economy = economy;
        _military = military;
        _diplomacy = diplomacy;
        BuildLayout();
        _driver.Ticked += OnTicked;
        _driver.SimulationChanged += RefreshAll;
        _driver.TimeControlsChanged += RefreshTimeControls;
        _selection.SelectionChanged += RefreshSelection;
        _driver.PlayerChanged += RefreshAll;
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
        var bar = new PanelContainer { Name = "TopBar", TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        bar.AddChild(row);

        _playerBadge = MakeButton("", () => _driver.SwitchPlayer());
        _playerBadge.TooltipText = "The civilization you control. F2 hands control to the next one (testing with several players on one screen).";
        row.AddChild(_playerBadge);
        _eraButton = MakeButton("", () => _research.Toggle());
        _eraButton.TooltipText = "Era and discoveries (R)";
        row.AddChild(_eraButton);
        var season = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        season.AddThemeConstantOverride("separation", 4);
        _seasonIcon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(22, 22), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Pass,
        };
        season.AddChild(_seasonIcon);
        _seasonLabel = new Label { CustomMinimumSize = new Vector2(64, 0), MouseFilter = Control.MouseFilterEnum.Pass };
        season.AddChild(_seasonLabel);
        row.AddChild(season);
        row.AddChild(new VSeparator());
        _timeLabel = new Label { CustomMinimumSize = new Vector2(48, 0), MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddChild(_timeLabel);

        _pauseButton = MakeButton("II", () => _driver.SetPaused(!_driver.Paused));
        _pauseButton.CustomMinimumSize = new Vector2(30, 0);
        _pauseButton.TooltipText = "Pause / resume (Space)";
        row.AddChild(_pauseButton);
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            int speed = i + 1;
            _speedButtons[i] = MakeButton($"{speed}×", () => _driver.SetSpeed(speed));
            _speedButtons[i].CustomMinimumSize = new Vector2(30, 0);
            _speedButtons[i].TooltipText = $"Speed {speed}× ({speed})";
            _speedButtons[i].ToggleMode = true;
            row.AddChild(_speedButtons[i]);
        }

        row.AddChild(new VSeparator());
        var food = Art.IconValue("food", "Food: meals in the shared stores (all foods by their food value)", out _foodLabel);
        _foodLabel.CustomMinimumSize = new Vector2(40, 0);
        row.AddChild(food);
        row.AddChild(Art.IconValue("people", "People (children in brackets) / people the camp and huts can shelter", out _peopleLabel));
        _goodsRow = new HBoxContainer();
        _goodsRow.AddThemeConstantOverride("separation", 8);
        row.AddChild(_goodsRow);
        row.AddChild(new VSeparator());
        _economyButton = MakeButton("Economy", () => _economy.Toggle());
        _economyButton.TooltipText = "Markets, prices, treasury and taxes (E)";
        row.AddChild(_economyButton);
        _coinsRow = Art.IconValue("coins", "Coins in the treasury", out _coinsLabel);
        row.AddChild(_coinsRow);
        _pricesRow = Art.IconValue("prices", "Price index: 100 = prices when coins came in", out _pricesLabel);
        row.AddChild(_pricesRow);
        _moodRow = Art.IconValue("happiness", "Average happiness (0-100)", out _moodLabel);
        row.AddChild(_moodRow);
        row.AddChild(new VSeparator());
        _armyButton = MakeButton("Army", () => _military.Toggle());
        _armyButton.TooltipText = "Recruit soldiers, rally point (M)";
        _armyButton.Icon = Art.Icon("soldiers");
        _armyButton.AddThemeConstantOverride("icon_max_width", 20);
        row.AddChild(_armyButton);
        _diplomacyButton = MakeButton("Diplomacy", () => _diplomacy.Toggle());
        _diplomacyButton.TooltipText = "War, peace, alliances, trade agreements and tribute (N)";
        row.AddChild(_diplomacyButton);


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

        _selectionPanel = new PanelContainer { Visible = false, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        _selectionPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _selectionPanel.Position += new Vector2(8, -40);
        _selectionPanel.GrowVertical = Control.GrowDirection.Begin;
        var selectionRows = new VBoxContainer();
        _selectionPanel.AddChild(selectionRows);
        _selectionLabel = new Label();
        selectionRows.AddChild(_selectionLabel);
        // Goods carried, held or still needed, as icons.
        _selectionGoods = new RichTextLabel { FitContent = true, AutowrapMode = TextServer.AutowrapMode.Off, ScrollActive = false, Visible = false };
        selectionRows.AddChild(_selectionGoods);
        // With soldiers selected: how groups march, and sending them home.
        _soldierActions = new HBoxContainer { Visible = false };
        _soldierActions.AddThemeConstantOverride("separation", 6);
        _soldierActions.AddChild(new Label { Text = "Formation" });
        var formation = new ButtonGroup();
        _lineButton = MakeButton("Line", () => _selection.Formation = Sim.Navigation.Formation.Line);
        _columnButton = MakeButton("Column", () => _selection.Formation = Sim.Navigation.Formation.Column);
        foreach (var b in new[] { _lineButton, _columnButton }) { b.ToggleMode = true; b.ButtonGroup = formation; _soldierActions.AddChild(b); }
        _lineButton.ButtonPressed = true;
        _soldierActions.AddChild(MakeButton("Send home", DisbandSelected));
        selectionRows.AddChild(_soldierActions);
        AddChild(_selectionPanel);

        var help = new Label
        {
            Text = "Right-click with villagers: hunt · gather · cut wood · build · work · move   |   soldiers: attack · " +
                   "Ctrl+right-click loot / attack-move   |   R discoveries · E economy · M army · N diplomacy · Space pause · " +
                   "1–4 speed · F5 / F9 save / load",
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
            if (WarMessage(e) is { } war) { ShowMessage(war); continue; }
            if (e.Player != _driver.Player) continue;
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
                SimEventKind.InstitutionEstablished => $"{content.Institutions[e.Detail].Def.Name} established" + InstitutionNote(content.Institutions[e.Detail].Def),
                SimEventKind.CropsRotted => "A harvest rotted in the field",
                SimEventKind.MerchantsArrived => "Foreign merchants have arrived at the market",
                SimEventKind.MerchantsLeft => "The merchants are leaving",
                SimEventKind.Unrest => $"Unrest! The people are unhappy (happiness {e.Detail}). E for details",
                _ => null,
            };
            if (message != null) ShowMessage(message);
        }
    }

    // Messages about war and diplomacy concerning the controlled player, or null.
    private string? WarMessage(SimEvent e)
    {
        int me = _driver.Player;
        string Other(int p) => DiplomacyPanel.NameOf(p);
        return e.Kind switch
        {
            SimEventKind.WarDeclared when e.Detail == me => $"{Other(e.Player)} has declared war on you! (N)",
            SimEventKind.WarDeclared when e.Player == me => $"You are at war with {Other(e.Detail)}",
            SimEventKind.ProposalReceived when e.Player == me => $"{Other(e.Detail)} has made you a proposal (N)",
            SimEventKind.ProposalAccepted when e.Player == me => $"{Other(e.Detail)} accepted your proposal",
            SimEventKind.ProposalDeclined when e.Player == me => $"{Other(e.Detail)} declined your proposal",
            SimEventKind.TributeLapsed when e.Player == me => $"You could not pay your tribute to {Other(e.Detail)}",
            SimEventKind.TributeLapsed when e.Detail == me => $"{Other(e.Player)} stopped paying you tribute",
            SimEventKind.DiedInBattle when e.Player == me => "One of your people was killed",
            SimEventKind.BuildingDestroyed when e.Player == me => $"Your {_driver.Simulation!.World.Content.Buildings[e.Detail].Def.Name.ToLowerInvariant()} was destroyed",
            SimEventKind.Plundered when e.Player == me => $"Raiders carried off {e.Detail} goods",
            SimEventKind.CaravanRaided when e.Player == me => "A caravan bound for your market was raided",
            SimEventKind.MarketClosed when e.Player == me => "No market day: enemy soldiers are at the marketplace",
            SimEventKind.Deserted when e.Player == me => "An unpaid soldier deserted",
            _ => null,
        };
    }

    private void DisbandSelected()
    {
        var sim = _driver.Simulation;
        if (sim == null) return;
        var ids = _selection.Selected.Where(id => sim.World.TryGetEntity(id, out var e) && e.HasComponent<Soldier>()).ToArray();
        if (ids.Length > 0) sim.Enqueue(new Sim.Commands.Disband(ids) { Player = _driver.Player });
    }

    private static string InstitutionNote(Sim.Content.InstitutionDef def) =>
        def.AutoJobs ? ": idle people now find work themselves"
        : def.Effects?.Households == true ? ": families now keep what they make (E)"
        : def.Effects?.Money == true ? ": build a mint to strike coins (E)"
        : "";

    private void RefreshBand()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        var census = Bands.CensusOf(world, _driver.Player);
        bool families = Economy.HasHouseholds(world, _driver.Player);
        var stock = Economy.Holdings(world, _driver.Player); // the shared stores, or everything families and the treasury hold
        _foodLabel.Text = Stores.MealsIn(world, stock).ToString();
        _foodLabel.TooltipText = families ? "Food: meals held by families and the treasury" : "Food: meals in the shared stores (all foods by their food value)";
        _economyButton.Visible = families;
        bool money = families && Economy.HasMoney(world, _driver.Player);
        _coinsRow.Visible = money;
        _pricesRow.Visible = money;
        _moodRow.Visible = families;
        if (money && Civics.TryGet(world, _driver.Player, out var civ))
        {
            Economy.TryGetMarket(world, _driver.Player, out var market);
            _coinsLabel.Text = civ.GetComponent<Trader>().Coins.ToString();
            _pricesLabel.Text = (market.IsNull ? 0 : Markets.Cpi(world, market)).ToString();
        }
        if (families) _moodLabel.Text = Society.AverageHappiness(world, _driver.Player).ToString();
        int soldiers = Military.Count(world, _driver.Player);
        _armyButton.Text = soldiers > 0 ? soldiers.ToString() : "Army";
        _armyButton.TooltipText = soldiers > 0 ? $"{soldiers} soldiers under arms: recruit, rally point (M)" : "Recruit soldiers, rally point (M)";
        _diplomacyButton.Visible = world.Setup.Civilizations > 1;
        _playerBadge.Visible = world.Setup.Civilizations > 1;
        _playerBadge.Text = $"Civ {_driver.Player}";
        _playerBadge.Modulate = Art.PlayerColor(_driver.Player).Lightened(0.35f);
        string children = census.Children > 0 ? $" ({census.Children})" : "";
        _peopleLabel.Text = $"{census.Total}{children} / {Buildings.ShelterOf(world, _driver.Player)}";
        // Non-food goods appear once the band has any (progressive disclosure); food is in the food count.
        string holder = families ? "held by families and the treasury" : "in the shared stores";
        for (int g = 0; g < stock.Length; g++)
        {
            var good = world.Content.Goods[g];
            bool show = good.Nutrition == 0 && stock[g] > 0;
            if (!_goods.TryGetValue(g, out var item))
            {
                if (!show) continue;
                var goodRow = Art.IconValue(good.Id, good.Name, out var value);
                _goodsRow.AddChild(goodRow);
                item = (goodRow, value);
                _goods[g] = item;
            }
            item.Row.Visible = show;
            item.Value.Text = stock[g].ToString();
            item.Row.TooltipText = item.Value.TooltipText = $"{good.Name} {holder}";
        }
        _eraButton.Text = Civics.EraOf(world, _driver.Player).Def.Name;
        var season = Calendar.Season(world);
        _seasonIcon.Texture = Art.Icon(season.Id);
        _seasonLabel.Text = $"Year {Calendar.Year(world)}";
        _seasonIcon.TooltipText = _seasonLabel.TooltipText = $"{season.Name}, year {Calendar.Year(world)}";
    }

    private void RefreshSelection()
    {
        var world = _driver.Simulation?.World;
        var selected = _selection.Selected;
        _selectionPanel.Visible = world != null && (selected.Count > 0 || _selection.SelectedBuilding != 0);
        if (!_selectionPanel.Visible) return;
        _selectionGoods.Clear();
        _selectionGoods.Visible = false;
        _soldierActions.Visible = world != null && selected.Any(id => world.TryGetEntity(id, out var s) && s.HasComponent<Soldier>());
        if (selected.Count == 0 && world!.TryGetEntity(_selection.SelectedBuilding, out var building))
        {
            _selectionLabel.Text = DescribeBuilding(world, building);
            ShowBuildingGoods(world, building);
            return;
        }

        var rules = world!.Content.Citizens;
        if (selected.Count == 1 && world.TryGetEntity(selected[0], out var one) && one.TryGetComponent<Soldier>(out var soldier))
        {
            var unit = world.Content.Units[soldier.Kind].Def;
            var citizen = one.GetComponent<Citizen>();
            string state = !soldier.Equipped ? "Collecting weapons" : Military.IsRouted(world, soldier) ? "Fleeing!" : DescribeSoldier(one.GetComponent<Order>());
            _selectionLabel.Text = $"{unit.Name} ({(soldier.Service == Service.Paid ? "paid" : "levy")}), age {Bands.AgeInYears(world, citizen)}  ·  {state}\n" +
                                   $"Health {citizen.Health * 100 / unit.Health}%  ·  Morale {soldier.Morale}  ·  Hunger {citizen.Hunger * 100 / rules.MaxHunger}%\n" +
                                   $"Attack {unit.Attack} · armour {unit.Armour} · range {unit.Range}";
            if (citizen.Carried > 0)
            {
                var carried = new int[world.Content.Goods.Count];
                carried[citizen.CarriedGood] = citizen.Carried;
                _selectionGoods.AddText("Carrying loot ");
                Art.AddGoods(_selectionGoods, world, carried);
                _selectionGoods.Visible = true;
            }
            return;
        }
        if (selected.Count == 1 && world.TryGetEntity(selected[0], out one))
        {
            var c = one.GetComponent<Citizen>();
            bool adult = Bands.IsAdult(world, c);
            string doing = adult ? Describe(one.GetComponent<Order>()) : "Too young to work";
            _selectionLabel.Text = $"{(adult ? "Villager" : "Child")}, age {Bands.AgeInYears(world, c)}  ·  {doing}\n" +
                                   $"Health {c.Health * 100 / rules.MaxHealth}%  ·  Hunger {c.Hunger * 100 / rules.MaxHunger}%";
            if (c.Carried > 0)
            {
                var carried = new int[world.Content.Goods.Count];
                carried[c.CarriedGood] = c.Carried;
                _selectionGoods.AddText("Carrying ");
                Art.AddGoods(_selectionGoods, world, carried);
                _selectionGoods.Visible = true;
            }
            if (Economy.HasHouseholds(world, _driver.Player))
            {
                string cls = adult ? Society.ClassNames[(int)Society.ClassOf(world, one, Society.NobleHomes(world, _driver.Player))] + "  ·  " : "";
                string home = Households.TryGetHome(world, one, out _) ? "lives with a family" : "lives at the camp";
                string work = adult ? (one.GetComponent<Order>().Public ? "  ·  works for the chief" : "  ·  works for the family") : "";
                _selectionLabel.Text += $"\n{cls}{home}{work}  ·  Happiness {c.Happiness}";
            }
            return;
        }
        var orders = selected
            .Select(id => !world.TryGetEntity(id, out var e) ? null
                : e.TryGetComponent<Soldier>(out var s) ? world.Content.Units[s.Kind].Def.Name + "s"
                : Bands.IsAdult(world, e.GetComponent<Citizen>()) ? Describe(e.GetComponent<Order>()) : "Children")
            .Where(d => d != null)
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} {g.Key!.ToLowerInvariant()}");
        _selectionLabel.Text = $"{selected.Count} people selected\n{string.Join("  ·  ", orders)}";
    }

    private static string DescribeSoldier(Order order) => order.Kind switch
    {
        OrderKind.Attack => order.Auto ? "Fighting" : "Attacking",
        OrderKind.AttackMove => "Marching to battle",
        OrderKind.Loot => "Looting",
        OrderKind.Move => "Marching",
        OrderKind.ReturnToCamp => "Returning to camp",
        _ => "On guard",
    };

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
        (OrderKind.Trade, _) => "Going to market",
        _ => "Idle",
    };

    private static string DescribeBuilding(World world, Friflo.Engine.ECS.Entity building)
    {
        var type = Buildings.TypeOf(world, building);
        var lines = new System.Collections.Generic.List<string>();
        if (!Buildings.IsComplete(building))
        {
            bool needs = Buildings.MissingMaterials(world, building).Any(n => n > 0);
            lines.Add($"{type.Def.Name} (construction site, {Buildings.ConstructionPercent(world, building)}%)");
            if (!needs) lines.Add("All materials delivered");
            lines.Add($"{Buildings.WorkersAt(world, building.Id)} builders · right-click with villagers selected to build");
            return string.Join("\n", lines);
        }
        lines.Add(type.Def.Name);
        if (building.GetComponent<Building>().Damage > 0)
            lines.Add($"Damaged: {Combat.HitPointsLeft(world, building)} / {type.Def.HitPoints} · right-click with villagers to repair");
        if (type.Def.Gate) lines.Add("A gate: your people pass, enemies must break it down");
        else if (type.Def.Wall) lines.Add("Blocks the way for everyone; enemies must break through");
        if (type.Def.Defence is { } defence) lines.Add($"Shoots enemy soldiers within {defence.Range} tiles");
        if (type.IsWorkplace) lines.Add($"Workers {Buildings.WorkersAt(world, building.Id)} / {type.Def.Workers}");
        if (building.TryGetComponent<Household>(out var household))
        {
            int members = Households.Members(world, building.GetComponent<Owner>().Player).GetValueOrDefault(building.Id)?.Count ?? 0;
            var trader = building.GetComponent<Trader>();
            lines.Add($"A family of {members}  ·  {trader.Coins} coins{(household.Cold ? "  ·  cold: no firewood" : "")}");
        }
        if (building.TryGetComponent<Market>(out var market))
            lines.Add($"Market days held {market.Days}  ·  E for prices");
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
        return string.Join("\n", lines);
    }

    // What a site still needs, or what a building holds, as icons.
    private void ShowBuildingGoods(World world, Friflo.Engine.ECS.Entity building)
    {
        if (!Buildings.IsComplete(building))
        {
            var missing = Buildings.MissingMaterials(world, building);
            if (!missing.Any(n => n > 0)) return;
            _selectionGoods.AddText("Still needs ");
            Art.AddGoods(_selectionGoods, world, missing);
        }
        else
        {
            var stock = building.GetComponent<Inventory>().Amounts;
            if (!stock.Any(n => n > 0)) return;
            _selectionGoods.AddText(Stores.IsStore(world, building) ? "Holds " : "Here: ");
            Art.AddGoods(_selectionGoods, world, stock);
        }
        _selectionGoods.Visible = true;
    }

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTimeControls();
        RefreshBand();
        RefreshSelection();

    }

    private void RefreshTime()
    {
        long tick = _driver.Simulation?.World.Tick ?? 0;
        long seconds = SimClock.ToSeconds(tick);
        _timeLabel.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        var setup = _driver.Simulation?.World.Setup;
        _timeLabel.TooltipText = $"Game time (tick {tick})" + (setup == null ? "" : $"\nMap {setup.MapSize} · seed {setup.Seed}");
    }

    private void RefreshTimeControls()
    {
        _pauseButton.Text = _driver.Paused ? ">" : "II";
        for (int i = 0; i < _speedButtons.Length; i++)
            _speedButtons[i].SetPressedNoSignal(!_driver.Paused && _driver.Speed == i + 1);
    }
}
