using System.Collections.Generic;
using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The economy panel (E): what stage the economy is in, market prices with their recent history, the price index
/// and money supply, the treasury's accounts, the tax and coin quality settings (sent as commands), and the social
/// classes and happiness. Sections appear as the economy develops (progressive disclosure). The controls are built
/// once; values are refreshed a few times a second so sliders can be dragged.
/// </summary>
public partial class EconomyPanel : CanvasLayer
{
    private static readonly Color Dim = new(1, 1, 1, 0.65f);
    private static readonly Color Up = new("#e8a07a");
    private static readonly Color Down = new("#8fd88f");
    private static readonly Color Line = new("#e6c86e");
    private static readonly Color MoneyLine = new("#8fb8e8");
    private const int ChartDays = 48;

    private SimulationDriver _driver = null!;
    private PanelContainer _panel = null!;
    private Label _stage = null!;
    private VBoxContainer _market = null!;
    private Label _barter = null!;
    private GridContainer _prices = null!;
    private VBoxContainer _money = null!;
    private Label _moneyText = null!;
    private Control _cpiChart = null!;
    private VBoxContainer _treasury = null!;
    private Label _treasuryText = null!;
    private RichTextLabel _publicStores = null!;
    private HSlider _tribute = null!, _marketTax = null!, _tariff = null!, _quality = null!;
    private Label _tributeValue = null!, _marketTaxValue = null!, _tariffValue = null!, _qualityValue = null!;
    private HBoxContainer _marketTaxRow = null!, _qualityRow = null!;
    private VBoxContainer _society = null!;
    private Label _societyText = null!;
    private bool _updating;
    private readonly Dictionary<int, (Label Price, Label Change, Label Volume, Control Chart)> _rows = [];
    private Label? _priceHeader;

    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen => _panel.Visible;

    /// <summary>Connects the panel to the game.</summary>
    public void Initialize(SimulationDriver driver)
    {
        _driver = driver;
        Layer = 2;
        _panel = new PanelContainer { Name = "EconomyPanel", Visible = false, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _panel.Position += new Vector2(8, 44);
        AddChild(_panel);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(520, 780), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _panel.AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(rows);

        Heading(rows, "Economy");
        _stage = Text(rows, "", Dim);

        _market = Section(rows, "Market");
        _barter = Text(_market, "", Dim);
        _prices = new GridContainer { Columns = 5 };
        _prices.AddThemeConstantOverride("h_separation", 12);
        _market.AddChild(_prices);

        _money = Section(rows, "Money and prices");
        _moneyText = Text(_money, "", Dim);
        _cpiChart = new Control { CustomMinimumSize = new Vector2(480, 70) };
        _cpiChart.Draw += DrawCpiChart;
        _money.AddChild(_cpiChart);

        _treasury = Section(rows, "Treasury and taxes");
        _treasuryText = Text(_treasury, "", Dim);
        _publicStores = new RichTextLabel { FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(490, 0), Modulate = Dim };
        _treasury.AddChild(_publicStores);
        _tribute = Slider(_treasury, "Tribute", out _tributeValue, out _,
            "Share of what families bring home that they deliver to the public stores. Feeds public works, but families keep less.");
        _marketTax = Slider(_treasury, "Market tax", out _marketTaxValue, out _marketTaxRow,
            "Share of every sale at the market, paid in coins.");
        _tariff = Slider(_treasury, "Tariff", out _tariffValue, out _,
            "Share of every trade with foreign merchants. Protects local sellers, but imports cost more.");
        _quality = Slider(_treasury, "Coin silver", out _qualityValue, out _qualityRow,
            "Silver in each new coin. Less silver strikes more coins from the same metal: the treasury can spend more today, but more money chasing the same goods drives prices up.");
        foreach (var slider in new[] { _tribute, _marketTax, _tariff }) slider.ValueChanged += _ => SendTaxes();
        _quality.ValueChanged += _ => SendQuality();

        _society = Section(rows, "People");
        _societyText = Text(_society, "", Dim);

        _driver.Ticked += () => { if (IsOpen && _driver.Simulation!.World.Tick % 5 == 0) Refresh(); };
        _driver.SimulationChanged += () =>
        {
            _rows.Clear();
            foreach (var c in _prices.GetChildren())
            {
                _prices.RemoveChild(c);
                c.QueueFree();
            }
            if (IsOpen) Refresh();
        };
    }

    /// <summary>Opens or closes the panel.</summary>
    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) Refresh();
    }

    private void Refresh()
    {
        var sim = _driver.Simulation;
        if (sim == null) return;
        var world = sim.World;
        var content = world.Content;
        bool families = Economy.HasHouseholds(world, _driver.Player);
        bool money = Economy.HasMoney(world, _driver.Player);
        bool market = Economy.TryGetMarket(world, _driver.Player, out var marketplace);
        if (!Civics.TryGet(world, _driver.Player, out var civEntity)) return;
        var civ = civEntity.GetComponent<Civilization>();

        _stage.Text = !families ? "Shared stores: the band pools everything it gathers. Private property comes later (R)."
            : money ? "Coins: families sell for money and buy what they need. Prices follow supply, demand and the amount of money."
            : market ? "Barter: families swap goods at the market, when each has what the other wants."
            : "Families keep what they make and pay tribute. A marketplace would let them trade.";
        if (Economy.HasGuilds(world, _driver.Player)) _stage.Text += " Guilds regulate the crafts.";

        _market.Visible = market;
        if (market) RefreshMarket(world, marketplace, money);
        _money.Visible = market && money;
        if (_money.Visible)
        {
            int cpi = Markets.Cpi(world, marketplace);
            _moneyText.Text = $"Price index {cpi} (100 = prices when coins came in)  ·  money in circulation {Economy.MoneySupply(world, _driver.Player)}  ·  " +
                              $"coins struck {civ.Minted}  ·  new coins {civ.CoinQuality}% silver";
            _cpiChart.QueueRedraw();
        }
        _treasury.Visible = families;
        if (families) RefreshTreasury(world, civEntity, civ, money);
        _society.Visible = families;
        if (families) RefreshSociety(world, civ);
    }

    private void RefreshMarket(World world, Friflo.Engine.ECS.Entity marketplace, bool money)
    {
        var content = world.Content;
        var state = marketplace.GetComponent<Market>();
        _barter.Text = money
            ? "Prices in coins. Arrows compare with 10 market days ago."
            : $"Barter: {state.BarterMatched} of {state.BarterWants} wants found a swap on the last market day." +
              (state.Medium >= 0 ? $" {content.Goods[state.Medium].Name} is taken as payment by people who don't need it: early money." : "") +
              " Values below are what goods were swapped at.";
        if (_prices.GetChildCount() == 0)
        {
            foreach (var header in new[] { "Good", "Price", "Change", "Sold", "Recent" })
            {
                var label = new Label { Text = header, Modulate = Dim };
                if (header == "Price") _priceHeader = label;
                _prices.AddChild(label);
            }
        }
        // Barter values become prices once coins arrive.
        _priceHeader!.Text = money ? "Price" : "Value";
        for (int g = 0; g < content.Goods.Count; g++)
        {
            if (state.Price[g] == 0) continue;
            if (!_rows.TryGetValue(g, out var row))
            {
                var name = new HBoxContainer { CustomMinimumSize = new Vector2(90, 0), TooltipText = content.Goods[g].Name };
                name.AddChild(Art.IconRect(content.Goods[g].Id, 22, content.Goods[g].Name));
                name.AddChild(new Label { Text = content.Goods[g].Name, MouseFilter = Control.MouseFilterEnum.Pass });
                _prices.AddChild(name);
                row = (new Label { CustomMinimumSize = new Vector2(60, 0) }, new Label { CustomMinimumSize = new Vector2(60, 0) },
                    new Label { CustomMinimumSize = new Vector2(40, 0) }, new Control { CustomMinimumSize = new Vector2(180, 22) });
                int good = g;
                row.Chart.Draw += () => DrawSparkline(row.Chart, good);
                _prices.AddChild(row.Price);
                _prices.AddChild(row.Change);
                _prices.AddChild(row.Volume);
                _prices.AddChild(row.Chart);
                _rows[g] = row;
            }
            int price = state.Price[g];
            var history = Markets.History(world, marketplace, g, 11);
            int before = history.Length > 0 ? history[0] : 0;
            row.Price.Text = price.ToString();
            int percent = before > 0 ? (price - before) * 100 / before : 0;
            if (percent != 0)
            {
                row.Change.Text = $"{(percent > 0 ? "▲" : "▼")} {System.Math.Abs(percent)}%";
                row.Change.Modulate = percent > 0 ? Up : Down;
            }
            else
            {
                row.Change.Text = "–";
                row.Change.Modulate = Dim;
            }
            row.Volume.Text = state.Volume[g].ToString();
            row.Chart.QueueRedraw();
        }
    }

    private void RefreshTreasury(World world, Friflo.Engine.ECS.Entity civEntity, Civilization civ, bool money)
    {
        var content = world.Content;
        var store = Stores.Totals(world, _driver.Player);
        _publicStores.Clear();
        _publicStores.AddText("Public stores:  ");
        if (store.Any(n => n > 0)) Art.AddGoods(_publicStores, world, store);
        else _publicStores.AddText("empty");
        var last = civ.LastLedger;
        string accounts = money
            ? $"Last year: market tax {last[(int)LedgerEntry.MarketTax]}, tariffs {last[(int)LedgerEntry.Tariffs]}, minted {last[(int)LedgerEntry.Minted]}, " +
              $"sales {last[(int)LedgerEntry.Sales]}  ·  wages {last[(int)LedgerEntry.Wages]}, purchases {last[(int)LedgerEntry.Purchases]}"
            : $"Last year: {last[(int)LedgerEntry.Tribute]} goods received as tribute";
        _treasuryText.Text = (money ? $"Coins {civEntity.GetComponent<Trader>().Coins}  ·  public wage {civ.PublicWage} per 100 ticks\n" : "Public workers are paid in food from the public stores.\n") +
                             accounts;
        _marketTaxRow.Visible = money;
        _qualityRow.Visible = money;
        _updating = true;
        var taxes = content.Economy.Taxes;
        _tribute.MaxValue = taxes.Tribute.Max;
        _marketTax.MaxValue = taxes.MarketTax.Max;
        _tariff.MaxValue = taxes.Tariff.Max;
        _quality.MinValue = content.Economy.Money.MinQuality;
        _quality.MaxValue = 100;
        if (!IsDragging(_tribute)) _tribute.Value = civ.TributePercent;
        if (!IsDragging(_marketTax)) _marketTax.Value = civ.MarketTaxPercent;
        if (!IsDragging(_tariff)) _tariff.Value = civ.TariffPercent;
        if (!IsDragging(_quality)) _quality.Value = civ.CoinQuality;
        _updating = false;
        _tributeValue.Text = $"{(int)_tribute.Value}%";
        _marketTaxValue.Text = $"{(int)_marketTax.Value}%";
        _tariffValue.Text = $"{(int)_tariff.Value}%";
        _qualityValue.Text = $"{(int)_quality.Value}%";
    }

    private void RefreshSociety(World world, Civilization civ)
    {
        var classes = Society.Count(world, _driver.Player);
        string counts = string.Join("  ·  ", classes.Select((n, c) => (n, c)).Where(x => x.n > 0).Select(x => $"{Society.ClassNames[x.c]} {x.n}"));
        int mood = Society.AverageHappiness(world, _driver.Player);
        _societyText.Text = $"{counts}\nHappiness {mood}/100 (work speed {Labor.Productivity(world, mood)}%)" +
                            (civ.Unrest ? "  ·  UNREST: the people are restless. Lower taxes, feed and house them." : "") +
                            $"\nHappiness rises with food, a home, firewood and safety, and falls with taxes and poverty.";
    }

    private bool IsDragging(HSlider slider) => slider.HasFocus() && Input.IsMouseButtonPressed(MouseButton.Left);

    private void SendTaxes()
    {
        if (_updating || _driver.Simulation == null) return;
        _driver.Simulation.Enqueue(new SetTaxes((int)_tribute.Value, (int)_marketTax.Value, (int)_tariff.Value) { Player = _driver.Player });
    }

    private void SendQuality()
    {
        if (_updating || _driver.Simulation == null) return;
        _driver.Simulation.Enqueue(new SetCoinQuality((int)_quality.Value) { Player = _driver.Player });
    }

    private void DrawSparkline(Control chart, int good)
    {
        var world = _driver.Simulation?.World;
        if (world == null || !Economy.TryGetMarket(world, _driver.Player, out var marketplace)) return;
        DrawSeries(chart, Markets.History(world, marketplace, good, ChartDays), Line, 1.5f);
    }

    private void DrawCpiChart()
    {
        var world = _driver.Simulation?.World;
        if (world == null || !Economy.TryGetMarket(world, _driver.Player, out var marketplace)) return;
        var size = _cpiChart.Size;
        _cpiChart.DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.25f));
        var cpi = Markets.History(world, marketplace, -1, ChartDays);
        // A line at 100: prices when coins came in.
        int max = System.Math.Max(150, cpi.DefaultIfEmpty(0).Max() + 10);
        float y100 = size.Y - size.Y * 100f / max;
        _cpiChart.DrawLine(new Vector2(0, y100), new Vector2(size.X, y100), new Color(1, 1, 1, 0.25f));
        DrawSeries(_cpiChart, cpi, Line, 2f, max);
        _cpiChart.DrawString(ThemeDB.FallbackFont, new Vector2(4, 14), "price index", modulate: Line, fontSize: 12);
    }

    private static void DrawSeries(Control chart, int[] values, Color color, float width, int? fixedMax = null)
    {
        var points = values.Select((v, i) => (v, i)).Where(p => p.v > 0).ToList();
        if (points.Count < 2) return;
        var size = chart.Size;
        int max = fixedMax ?? points.Max(p => p.v);
        int min = fixedMax.HasValue ? 0 : points.Min(p => p.v);
        float range = System.Math.Max(1, max - min);
        var line = points.Select(p => new Vector2(
            size.X * p.i / System.Math.Max(1, values.Length - 1),
            size.Y - 2 - (size.Y - 4) * (p.v - min) / range)).ToArray();
        chart.DrawPolyline(line, color, width, antialiased: true);
    }

    private static VBoxContainer Section(VBoxContainer parent, string title)
    {
        var section = new VBoxContainer();
        parent.AddChild(section);
        Heading(section, title);
        return section;
    }

    private static HSlider Slider(VBoxContainer parent, string name, out Label value, out HBoxContainer row, string tooltip)
    {
        row = new HBoxContainer { TooltipText = tooltip };
        parent.AddChild(row);
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(110, 0), TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass });
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, CustomMinimumSize = new Vector2(280, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TooltipText = tooltip };
        row.AddChild(slider);
        value = new Label { CustomMinimumSize = new Vector2(50, 0) };
        row.AddChild(value);
        return slider;
    }

    private static void Heading(VBoxContainer parent, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        parent.AddChild(label);
    }

    private static Label Text(VBoxContainer parent, string text, Color color)
    {
        var label = new Label
        {
            Text = text, Modulate = color, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(490, 0),
        };
        parent.AddChild(label);
        return label;
    }
}
