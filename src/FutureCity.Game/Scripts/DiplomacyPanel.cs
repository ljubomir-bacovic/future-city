using System;
using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The diplomacy panel (N): one card per other civilization with the relation, trade agreement and tribute between you,
/// their proposals awaiting your answer, and buttons to declare war or propose peace, an alliance, a trade agreement
/// or tribute. Cards are rebuilt only when something between the civilizations changes; choices are sent as commands.
/// </summary>
public partial class DiplomacyPanel : CanvasLayer
{
    private static readonly Color Dim = new(1, 1, 1, 0.65f);
    private static readonly Color WarColor = new("#e8705a");
    private static readonly Color AllyColor = new("#8fd88f");

    private SimulationDriver _driver = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _cards = null!;
    private string _shown = "";
    private int _tributeAmount = 50;
    private int _tributeGood = -1;

    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen => _panel.Visible;

    /// <summary>Connects the panel to the game.</summary>
    public void Initialize(SimulationDriver driver)
    {
        _driver = driver;
        Layer = 2;
        _panel = new PanelContainer { Name = "DiplomacyPanel", Visible = false, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _panel.Position += new Vector2(8, 44);
        AddChild(_panel);
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) };
        rows.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(rows);
        var heading = new Label { Text = "Diplomacy" };
        heading.AddThemeFontSizeOverride("font_size", 18);
        rows.AddChild(heading);
        rows.AddChild(new Label
        {
            Text = "War lets your soldiers fight, loot and burn, and closes markets near the fighting. A trade agreement sends " +
                   "caravans between your treasuries without tariffs; an alliance brings each of you to the other's defence.",
            Modulate = Dim, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0),
        });
        _cards = new VBoxContainer();
        _cards.AddThemeConstantOverride("separation", 10);
        rows.AddChild(_cards);
        _driver.Ticked += () => { if (IsOpen && _driver.Simulation!.World.Tick % 5 == 0) Refresh(); };
        _driver.SimulationChanged += () => { _shown = ""; if (IsOpen) Refresh(); };
        _driver.PlayerChanged += () => { _shown = ""; if (IsOpen) Refresh(); };
    }

    /// <summary>Opens or closes the panel.</summary>
    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) { _shown = ""; Refresh(); }
    }

    /// <summary>Closes the panel.</summary>
    public void Close() => _panel.Visible = false;

    /// <summary>Name of a player as shown in the game.</summary>
    public static string NameOf(int player) => $"Civilization {player}";

    private void Refresh()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        int me = _driver.Player;
        var others = Enumerable.Range(1, world.Setup.Civilizations).Where(p => p != me).ToList();
        // Rebuild only when something between the civilizations changed, so buttons stay put under the mouse.
        string state = string.Join(";", others.Select(p =>
            $"{Relations.Between(world, me, p)},{Relations.HaveTradeAgreement(world, me, p)},{Relations.Tribute(world, me, p)}," +
            $"{Relations.Tribute(world, p, me)},{Relations.Pending(world, me, p)},{Relations.Pending(world, p, me)}"));
        if (state == _shown) return;
        _shown = state;
        foreach (var child in _cards.GetChildren()) { _cards.RemoveChild(child); child.QueueFree(); }
        if (others.Count == 0)
        {
            _cards.AddChild(new Label { Text = "No other civilizations are known yet.", Modulate = Dim });
            return;
        }
        foreach (int other in others) _cards.AddChild(Card(world, me, other));
    }

    private Control Card(World world, int me, int other)
    {
        var card = new VBoxContainer();
        var title = new HBoxContainer();
        title.AddThemeConstantOverride("separation", 10);
        title.AddChild(new ColorRect { Color = Art.PlayerColor(other), CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        var name = new Label { Text = NameOf(other) };
        name.AddThemeFontSizeOverride("font_size", 16);
        title.AddChild(name);
        var relation = Relations.Between(world, me, other);
        title.AddChild(new Label
        {
            Text = relation switch { Relation.War => "At war", Relation.Alliance => "Allied", _ => "At peace" },
            Modulate = relation switch { Relation.War => WarColor, Relation.Alliance => AllyColor, _ => Colors.White },
        });
        card.AddChild(title);

        var facts = new System.Collections.Generic.List<string>();
        if (Relations.HaveTradeAgreement(world, me, other)) facts.Add("trade agreement: caravans every year, no tariffs");
        var (paysGood, pays) = Relations.Tribute(world, me, other);
        if (pays > 0) facts.Add($"you pay {pays} {GoodName(world, paysGood)} a year in tribute");
        var (getsGood, gets) = Relations.Tribute(world, other, me);
        if (gets > 0) facts.Add($"they pay you {gets} {GoodName(world, getsGood)} a year in tribute");
        var (askedKind, _, _) = Relations.Pending(world, other, me);
        if (askedKind != ProposalKind.None) facts.Add($"your proposal ({Describe(askedKind)}) awaits their answer");
        if (facts.Count > 0) card.AddChild(new Label { Text = string.Join("\n", facts), Modulate = Dim });

        var (kind, good, amount) = Relations.Pending(world, me, other);
        if (kind != ProposalKind.None)
        {
            var offer = new HBoxContainer();
            offer.AddThemeConstantOverride("separation", 8);
            string what = kind switch
            {
                ProposalKind.OfferTribute => $"offers to pay you {amount} {GoodName(world, good)} a year",
                ProposalKind.DemandTribute => $"demands {amount} {GoodName(world, good)} a year in tribute",
                _ => "proposes " + Describe(kind),
            };
            offer.AddChild(new Label { Text = $"{NameOf(other)} {what}.", Modulate = new Color("#f2c94c") });
            offer.AddChild(MakeButton("Accept", () => Send(new Respond(other, true))));
            offer.AddChild(MakeButton("Decline", () => Send(new Respond(other, false))));
            card.AddChild(offer);
        }

        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", 6);
        if (relation != Relation.War) actions.AddChild(MakeButton("Declare war", () => Send(new DeclareWar(other)), WarColor));
        void Offer(ProposalKind k, string text)
        {
            if (Relations.CanPropose(world, me, other, k, -1, 1)) actions.AddChild(MakeButton(text, () => Send(new Propose(other, k))));
        }
        Offer(ProposalKind.Peace, "Propose peace");
        Offer(ProposalKind.Alliance, "Propose alliance");
        Offer(ProposalKind.TradeAgreement, "Propose trade agreement");
        if (relation == Relation.Alliance) actions.AddChild(MakeButton("End alliance", () => Send(new BreakAgreement(other, ProposalKind.Alliance))));
        if (Relations.HaveTradeAgreement(world, me, other))
            actions.AddChild(MakeButton("End trade agreement", () => Send(new BreakAgreement(other, ProposalKind.TradeAgreement))));
        if (pays > 0) actions.AddChild(MakeButton("Stop paying tribute", () => Send(new BreakAgreement(other, ProposalKind.OfferTribute))));
        card.AddChild(actions);

        var tribute = new HBoxContainer();
        tribute.AddThemeConstantOverride("separation", 6);
        tribute.AddChild(new Label { Text = "Tribute:", Modulate = Dim });
        var spin = new SpinBox { MinValue = 1, MaxValue = 5000, Value = _tributeAmount, CustomMinimumSize = new Vector2(90, 0) };
        spin.ValueChanged += v => _tributeAmount = (int)v;
        tribute.AddChild(spin);
        var goods = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        goods.AddItem("coins", 0);
        for (int g = 0; g < world.Content.Goods.Count; g++) goods.AddItem(world.Content.Goods[g].Name.ToLowerInvariant(), g + 1);
        goods.Select(_tributeGood + 1);
        goods.ItemSelected += i => _tributeGood = (int)i - 1;
        tribute.AddChild(goods);
        tribute.AddChild(MakeButton("Offer", () => Send(new Propose(other, ProposalKind.OfferTribute, GoodId(world), _tributeAmount))));
        tribute.AddChild(MakeButton("Demand", () => Send(new Propose(other, ProposalKind.DemandTribute, GoodId(world), _tributeAmount))));
        card.AddChild(tribute);
        card.AddChild(new HSeparator());
        return card;
    }

    private string? GoodId(World world) => _tributeGood < 0 ? null : world.Content.Goods[_tributeGood].Id;

    private static string GoodName(World world, int good) => good < 0 ? "coins" : world.Content.Goods[good].Name.ToLowerInvariant();

    private static string Describe(ProposalKind kind) => kind switch
    {
        ProposalKind.Peace => "peace",
        ProposalKind.Alliance => "an alliance",
        ProposalKind.TradeAgreement => "a trade agreement",
        ProposalKind.OfferTribute => "tribute paid by them",
        ProposalKind.DemandTribute => "tribute paid by you",
        _ => "nothing",
    };

    private void Send(Command command)
    {
        _driver.Simulation?.Enqueue(command with { Player = _driver.Player });
        _shown = ""; // show the result as soon as it happens
    }

    private static Button MakeButton(string text, Action onPressed, Color? color = null)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        if (color is { } c) button.Modulate = c;
        button.Pressed += onPressed;
        return button;
    }
}
