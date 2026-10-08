using System;
using System.Collections.Generic;
using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The army panel (M): recruit soldiers of each kind as levies or paid soldiers (with what they cost the stores and
/// what they still need), see the army, select it, set its rally point or send it home. The controls are built once and
/// refreshed a few times a second; choices are sent as commands.
/// </summary>
public partial class MilitaryPanel : CanvasLayer
{
    private static readonly Color Dim = new(1, 1, 1, 0.65f);
    private static readonly Color Ok = new("#8fd88f");
    private static readonly Color Missing = new("#e8a07a");

    private SimulationDriver _driver = null!;
    private SelectionController _selection = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _units = null!;
    private Button _levy = null!, _paid = null!;
    private Label _serviceNote = null!;
    private Label _army = null!;
    private readonly List<(int Kind, Label Status, SpinBox Count, Button Recruit, RichTextLabel Cost)> _rows = [];
    private Service _service = Service.Levy;

    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen => _panel.Visible;

    /// <summary>Connects the panel to the game and to the selection (for selecting the army and placing its rally point).</summary>
    public void Initialize(SimulationDriver driver, SelectionController selection)
    {
        _driver = driver;
        _selection = selection;
        Layer = 2;
        _panel = new PanelContainer { Name = "MilitaryPanel", Visible = false, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.Position += new Vector2(-8, 44);
        AddChild(_panel);
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        rows.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(rows);

        var heading = new Label { Text = "Army" };
        heading.AddThemeFontSizeOverride("font_size", 18);
        rows.AddChild(heading);
        rows.AddChild(Wrapped("Soldiers are your own people: everyone called up stops working for their family, eats from the " +
                              "public stores and collects their weapons there. Levies serve unpaid; paid soldiers need coins, " +
                              "fight better and hold together longer, but desert when the treasury cannot pay them.", Dim));

        var service = new HBoxContainer();
        service.AddThemeConstantOverride("separation", 8);
        service.AddChild(new Label { Text = "Call up as" });
        var group = new ButtonGroup();
        _levy = new Button { Text = "Levies", ToggleMode = true, ButtonGroup = group, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
        _paid = new Button { Text = "Paid soldiers", ToggleMode = true, ButtonGroup = group, FocusMode = Control.FocusModeEnum.None };
        _levy.Pressed += () => { _service = Service.Levy; Refresh(); };
        _paid.Pressed += () => { _service = Service.Paid; Refresh(); };
        service.AddChild(_levy);
        service.AddChild(_paid);
        _serviceNote = new Label { Modulate = Dim };
        service.AddChild(_serviceNote);
        rows.AddChild(service);

        _units = new VBoxContainer();
        _units.AddThemeConstantOverride("separation", 4);
        rows.AddChild(_units);

        rows.AddChild(new HSeparator());
        _army = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) };
        rows.AddChild(_army);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(MakeButton("Select army", () => _selection.SelectSoldiers()));
        actions.AddChild(MakeButton("Set rally point", () => _selection.BeginRallyPoint(), "Then click on the map: new soldiers gather there"));
        actions.AddChild(MakeButton("Send all home", DisbandAll, "Disband every soldier: they go back to their families and work"));
        rows.AddChild(actions);

        _driver.Ticked += () => { if (IsOpen && _driver.Simulation!.World.Tick % 5 == 0) Refresh(); };
        _driver.SimulationChanged += () => { _rows.Clear(); foreach (var c in _units.GetChildren()) { _units.RemoveChild(c); c.QueueFree(); } if (IsOpen) Refresh(); };
        _driver.PlayerChanged += () => { if (IsOpen) Refresh(); };
    }

    /// <summary>Opens or closes the panel.</summary>
    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) Refresh();
    }

    /// <summary>Closes the panel.</summary>
    public void Close() => _panel.Visible = false;

    private void Refresh()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;
        int player = _driver.Player;
        if (_rows.Count == 0) BuildRows(world);
        var facts = Civics.FactsOf(world, player);
        bool money = Economy.HasMoney(world, player);
        _paid.Disabled = !money;
        if (!money && _service == Service.Paid) { _service = Service.Levy; _levy.ButtonPressed = true; }
        if (Civics.TryGet(world, player, out var civ) && money)
        {
            int wage = civ.GetComponent<Civilization>().PublicWage * world.Content.Military.Service.Paid.WagePercent / 100;
            _serviceNote.Text = $"wage {wage} coins per soldier every 10 s · treasury {civ.GetComponent<Trader>().Coins}";
        }
        else
        {
            _serviceNote.Text = money ? "" : "paid soldiers need Coinage";
        }

        var store = Stores.Totals(world, player);
        foreach (var (kind, status, count, recruit, _) in _rows)
        {
            var unit = world.Content.Units[kind];
            var can = Military.CanRecruit(world, player, kind, _service, facts);
            bool armed = Enumerable.Range(0, store.Length).All(g => store[g] >= unit.Equipment[g] * (int)count.Value);
            (status.Text, status.Modulate) = can switch
            {
                Recruitment.Ok => (armed ? "Ready" : "Short of weapons: they will wait", armed ? Ok : Missing),
                Recruitment.NotAvailable => ("Needs " + string.Join(", ", Civics.Explain(world, unit.Requires, facts).Where(p => !p.Met).Select(p => p.Label)), Missing),
                Recruitment.NeedsCoins => ("Needs coins", Missing),
                _ => ("No one free to call up", Missing),
            };
            recruit.Disabled = can != Recruitment.Ok;
        }

        var soldiers = World.InIdOrder(world.Store.Query<Soldier, Owner>()).Where(s => s.GetComponent<Owner>().Player == player).ToList();
        if (soldiers.Count == 0)
        {
            _army.Text = "No soldiers.";
            return;
        }
        var byKind = soldiers.GroupBy(s => s.GetComponent<Soldier>().Kind).OrderBy(g => g.Key)
            .Select(g => $"{g.Count()} {world.Content.Units[g.Key].Def.Name.ToLowerInvariant()}");
        int waiting = soldiers.Count(s => !s.GetComponent<Soldier>().Equipped);
        int fleeing = soldiers.Count(s => Military.IsRouted(world, s.GetComponent<Soldier>()));
        int morale = (int)soldiers.Average(s => s.GetComponent<Soldier>().Morale);
        _army.Text = $"{soldiers.Count} soldiers: {string.Join(", ", byKind)}  ·  morale {morale}" +
                     (waiting > 0 ? $"  ·  {waiting} waiting for weapons" : "") + (fleeing > 0 ? $"  ·  {fleeing} fleeing" : "");
    }

    private void BuildRows(World world)
    {
        foreach (var unit in world.Content.Units)
        {
            var box = new VBoxContainer();
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new TextureRect
            {
                Texture = Art.SpriteIcon(unit.Def.Id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(30, 30),
            });
            row.AddChild(new Label
            {
                Text = unit.Def.Name, CustomMinimumSize = new Vector2(110, 0), TooltipText = unit.Def.Codex,
                MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            });
            var cost = new RichTextLabel
            {
                FitContent = true, AutowrapMode = TextServer.AutowrapMode.Off, ScrollActive = false,
                CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            Art.AddGoods(cost, world, unit.Equipment.ToArray());
            row.AddChild(cost);
            var count = new SpinBox { MinValue = 1, MaxValue = 30, Value = 1, CustomMinimumSize = new Vector2(80, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            row.AddChild(count);
            int kind = unit.Index;
            var recruit = MakeButton("Recruit", () => Recruit(kind, (int)count.Value));
            row.AddChild(recruit);
            box.AddChild(row);
            var d = unit.Def;
            string role = unit.Role switch { UnitRole.Back => "shoots from behind", UnitRole.Siege => "attacks buildings only", _ => "melee" };
            var info = new HBoxContainer();
            info.AddChild(new Label
            {
                Text = $"      attack {d.Attack} · armour {d.Armour} · range {d.Range} · health {d.Health} · {role}",
                Modulate = Dim, CustomMinimumSize = new Vector2(300, 0),
            });
            var status = new Label();
            info.AddChild(status);
            box.AddChild(info);
            _units.AddChild(box);
            _rows.Add((kind, status, count, recruit, cost));
        }
    }

    private void Recruit(int kind, int count)
    {
        var sim = _driver.Simulation;
        if (sim == null) return;
        sim.Enqueue(new Recruit(sim.World.Content.Units[kind].Def.Id, count, _service) { Player = _driver.Player });
    }

    private void DisbandAll()
    {
        var sim = _driver.Simulation;
        if (sim == null) return;
        var ids = World.InIdOrder(sim.World.Store.Query<Soldier, Owner>())
            .Where(s => s.GetComponent<Owner>().Player == _driver.Player).Select(s => s.Id).ToArray();
        if (ids.Length > 0) sim.Enqueue(new Disband(ids) { Player = _driver.Player });
    }

    private static Button MakeButton(string text, Action onPressed, string tooltip = "")
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
        button.Pressed += onPressed;
        return button;
    }

    private static Label Wrapped(string text, Color color) =>
        new() { Text = text, Modulate = color, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0) };
}
