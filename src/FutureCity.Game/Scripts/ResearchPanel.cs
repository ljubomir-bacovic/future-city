using System.Linq;
using FutureCity.Sim;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// The progress panel (R): the next era and what it still needs, technologies (known, discoverable with progress,
/// or locked with the missing preconditions) with a research focus button, and institutions with an Establish
/// button. Codex texts explain why each one emerged. Everything shown comes from the emergence engine's
/// "what is missing" data; choices are sent as commands.
/// </summary>
public partial class ResearchPanel : CanvasLayer
{
    private static readonly Color Met = new("#8fd88f");
    private static readonly Color Missing = new("#e8a07a");
    private static readonly Color Dim = new(1, 1, 1, 0.65f);

    private SimulationDriver _driver = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _rows = null!;

    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen => _panel.Visible;

    /// <summary>Connects the panel to the game.</summary>
    public void Initialize(SimulationDriver driver)
    {
        _driver = driver;
        Layer = 2;
        _panel = new PanelContainer { Name = "ResearchPanel", Visible = false, CustomMinimumSize = new Vector2(460, 0) };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.Position += new Vector2(-8, 44);
        AddChild(_panel);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(460, 600), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _panel.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rows);
        _driver.Ticked += () => { if (IsOpen && _driver.Simulation!.World.Tick % 10 == 0) Rebuild(); };
        _driver.SimulationChanged += () => { if (IsOpen) Rebuild(); };
    }

    /// <summary>Opens or closes the panel.</summary>
    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) Rebuild();
    }

    private void Rebuild()
    {
        var sim = _driver.Simulation;
        if (sim == null) return;
        foreach (var child in _rows.GetChildren()) child.QueueFree();
        var world = sim.World;
        var content = world.Content;
        var facts = Civics.FactsOf(world, _driver.Player);
        Civics.TryGet(world, _driver.Player, out var civEntity);
        var civ = civEntity.IsNull ? default : civEntity.GetComponent<Civilization>();

        var era = Civics.EraOf(world, _driver.Player);
        Heading($"{era.Def.Name}");
        Text(era.Def.Codex, Dim);
        if (era.Index + 1 < content.Eras.Count)
        {
            var next = content.Eras[era.Index + 1];
            Heading($"Next era: {next.Def.Name}");
            Checklist(world, next.Preconditions, facts);
        }

        Heading("Discoveries");
        Text("Related work brings discoveries closer once their conditions are met. Elders at a shrine research the focus.", Dim);
        foreach (var tech in content.Techs)
        {
            var row = new HBoxContainer();
            _rows.AddChild(row);
            var name = new Label { Text = tech.Def.Name, CustomMinimumSize = new Vector2(150, 0), TooltipText = tech.Def.Codex, MouseFilter = Control.MouseFilterEnum.Pass };
            row.AddChild(name);
            bool known = civ.Techs != null && civ.Techs[tech.Index] != 0;
            if (known)
            {
                row.AddChild(new Label { Text = "Known", Modulate = Met });
                Text(tech.Def.Codex, Dim);
                continue;
            }
            if (!tech.Preconditions.IsMet(facts))
            {
                row.AddChild(new Label { Text = "Not yet possible", Modulate = Missing });
                Checklist(world, tech.Preconditions, facts);
                continue;
            }
            int progress = civ.TechProgress?[tech.Index] ?? 0;
            row.AddChild(new ProgressBar
            {
                MinValue = 0, MaxValue = tech.Def.Cost, Value = progress, CustomMinimumSize = new Vector2(160, 18),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            });
            bool focused = civ.ResearchFocus == tech.Index;
            var focus = new Button { Text = focused ? "Focused" : "Focus", Disabled = focused, FocusMode = Control.FocusModeEnum.None };
            string id = tech.Def.Id;
            focus.Pressed += () => sim.Enqueue(new SetResearchFocus(id) { Player = _driver.Player });
            row.AddChild(focus);
        }

        Heading("Institutions");
        foreach (var institution in content.Institutions)
        {
            var row = new HBoxContainer();
            _rows.AddChild(row);
            row.AddChild(new Label { Text = institution.Def.Name, CustomMinimumSize = new Vector2(150, 0), TooltipText = institution.Def.Codex, MouseFilter = Control.MouseFilterEnum.Pass });
            if (Civics.Has(world, _driver.Player, institution.Index))
            {
                row.AddChild(new Label { Text = "Established", Modulate = Met });
                Text(institution.Def.Codex, Dim);
                continue;
            }
            bool possible = institution.Preconditions.IsMet(facts);
            var establish = new Button
            {
                Text = $"Establish ({institution.Def.FoodCost} meals)",
                Disabled = !Civics.CanEstablish(world, _driver.Player, institution.Index, facts),
                FocusMode = Control.FocusModeEnum.None,
            };
            string id = institution.Def.Id;
            establish.Pressed += () => sim.Enqueue(new EstablishInstitution(id) { Player = _driver.Player });
            row.AddChild(establish);
            Text(institution.Def.Codex, Dim);
            if (!possible) Checklist(world, institution.Preconditions, facts);
        }
    }

    private void Checklist(World world, Condition condition, Facts facts)
    {
        foreach (var part in Civics.Explain(world, condition, facts))
        {
            string value = part.Target == 1 && part.Current is 0 or 1 ? (part.Met ? "yes" : "no") : $"{part.Current} / {part.Target}";
            _rows.AddChild(new Label { Text = $"    {(part.Met ? "+" : "-")} {part.Label}: {value}", Modulate = part.Met ? Met : Missing });
        }
    }

    private void Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        _rows.AddChild(label);
    }

    private void Text(string text, Color color) =>
        _rows.AddChild(new Label
        {
            Text = text, Modulate = color, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(430, 0),
        });
}
