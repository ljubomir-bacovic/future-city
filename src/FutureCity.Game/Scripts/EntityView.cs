using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using FutureCity.Sim;
using FutureCity.Sim.Components;
using Godot;

namespace FutureCity.Game;

/// <summary>
/// Draws people, soldiers, animals, plants, deposits, camps, buildings and ruins with the sprites in <see cref="Art"/>,
/// falling back to simple shapes for content without art. Walkers are interpolated between ticks and step while they
/// move, and everything is drawn back to front. Construction sites show a scaffold and a progress bar; fields show
/// their crop; market stalls show the goods on sale and a worked mill turns its sails. With several civilizations,
/// people stand on a ring and buildings fly a flag in their owner's colour. Soldiers carry a pennant (a white flag
/// when they flee) and a health bar once hurt; strikes show as a lunge, shots as flying arrows, and badly damaged
/// buildings burn.
/// </summary>
public partial class EntityView : Node2D
{
    private static readonly Color Shadow = new(0, 0, 0, 0.25f);
    private static readonly Color Skin = new("#e0b48c");
    private static readonly Color Tunic = new("#3b6fb6");
    private static readonly Color Hair = new("#4a3020");
    private static readonly Color Leaf = new("#3f8a3a");
    private static readonly Color LeafDark = new("#2e6b2b");
    private static readonly Color BareTwig = new("#7a6248");
    private static readonly Color Meat = new("#8c3b2f");
    private static readonly Color Tent = new("#b08850");
    private static readonly Color TentDark = new("#8a6a3c");
    private static readonly Color Fire = new("#ff9a2e");
    private static readonly Color SelectRing = new(1, 1, 1, 0.9f);
    private static readonly Color HealthGood = new("#5ecf5e");
    private static readonly Color HealthBad = new("#d94a3a");
    private static readonly Color Soil = new("#7a5a3a");
    private static readonly Color Sprout = new("#6fae45");
    private static readonly Color RipeCrop = new("#e2c04a");
    private static readonly Color Scaffold = new("#c9a46a");
    private static readonly Color Thatch = new("#c8a35a");

    private static readonly Color Arrow = new("#3a2a1a");
    private static readonly Color Flame = new("#ff8a1e");
    private static readonly Color FlameCore = new("#ffd84a");
    private static readonly Color Smoke = new(0.35f, 0.35f, 0.35f, 0.5f);

    // How long a strike or a shot stays visible, in seconds.
    private const float StrikeSeconds = 0.35f;

    private SimulationDriver _driver = null!;
    private MapView _map = null!;
    private SelectionController _selection = null!;
    private readonly List<(float Depth, Action Draw)> _drawList = new();
    // Recent hits: who struck whom, when (animation time) and whether it was a shot from a distance.
    private readonly List<(int Attacker, int Target, float Time, bool Ranged)> _strikes = new();

    /// <summary>Connects the view to the game it draws.</summary>
    public void Initialize(SimulationDriver driver, MapView map, SelectionController selection)
    {
        _driver = driver;
        _map = map;
        _selection = selection;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _driver.Ticked += CollectStrikes;
        _driver.SimulationChanged += _strikes.Clear;
    }

    private void CollectStrikes()
    {
        var world = _driver.Simulation!.World;
        _strikes.RemoveAll(s => Now - s.Time > StrikeSeconds);
        foreach (var e in world.Events)
        {
            if (e.Kind != SimEventKind.Attacked || !world.TryGetEntity(e.Entity, out var attacker)) continue;
            bool ranged = attacker.HasComponent<Building>()
                          || (attacker.TryGetComponent<Soldier>(out var s) && world.Content.Units[s.Kind].Def.Range > 1);
            _strikes.Add((e.Entity, e.Detail, Now, ranged));
        }
    }

    // Animation time in seconds (presentation only; the simulation never sees it).
    private static float Now => Time.GetTicksMsec() / 1000f;

    // Whether a walker is between two tiles, and whether it is heading left on screen.
    private bool IsWalking(Entity entity, out bool left)
    {
        left = false;
        if (!entity.TryGetComponent<Mover>(out var mover)) return false;
        var pos = entity.GetComponent<TilePosition>();
        if (mover.NextX == pos.X && mover.NextY == pos.Y) return false;
        left = (mover.NextX - pos.X) - (mover.NextY - pos.Y) < 0;
        return true;
    }

    // Two-frame walk cycle, offset per entity so a group does not step in lockstep.
    private static bool StepFrame(int id) => ((int)(Now * 5) + id) % 2 == 0;

    public override void _Process(double delta) => QueueRedraw();

    /// <summary>World-space position of an entity's feet (a building's footprint center), interpolated for walkers.</summary>
    public Vector2 WorldPosition(Entity entity)
    {
        var pos = entity.GetComponent<TilePosition>();
        var tile = new Vector2(pos.X, pos.Y);
        if (entity.TryGetComponent<Building>(out var building))
        {
            float half = (_driver.Simulation!.World.Content.Buildings[building.Kind].Def.Size - 1) / 2f;
            return _map.TileToLocal(tile + new Vector2(half, half));
        }
        if (entity.TryGetComponent<Mover>(out var mover) && (mover.NextX != pos.X || mover.NextY != pos.Y) && mover.StepCost > 0)
        {
            float t = Mathf.Clamp((mover.Progress + (float)_driver.Alpha * Mover.ProgressPerTick) / mover.StepCost, 0f, 1f);
            tile = tile.Lerp(new Vector2(mover.NextX, mover.NextY), t);
        }
        return _map.TileToLocal(tile);
    }

    /// <summary>The four ground corners (top, right, bottom, left) of a square of tiles, shrunk by <paramref name="inset"/> tiles.</summary>
    public Vector2[] Footprint(int x, int y, int size, float inset = 0f)
    {
        float a = -0.5f + inset, b = size - 0.5f - inset;
        return
        [
            _map.TileToLocal(new Vector2(x + a, y + a)),
            _map.TileToLocal(new Vector2(x + b, y + a)),
            _map.TileToLocal(new Vector2(x + b, y + b)),
            _map.TileToLocal(new Vector2(x + a, y + b)),
        ];
    }

    public override void _Draw()
    {
        var world = _driver.Simulation?.World;
        if (world == null) return;

        _drawList.Clear();
        foreach (var e in world.Store.Query<Camp, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            _drawList.Add((p.Y, () => { if (!Art.DrawSprite(this, "camp", p)) DrawCamp(p); }));
        }
        bool flags = world.Setup.Civilizations > 1;
        foreach (var e in world.Store.Query<Building, TilePosition>().Entities)
            AddBuilding(world, e, flags);
        foreach (var e in world.Store.Query<LootPile, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            _drawList.Add((p.Y, () =>
            {
                if (Art.DrawSprite(this, "loot", p)) return;
                Ellipse(p, 9, 4, Shadow);
                Ellipse(p + new Vector2(0, -3), 7, 4, new Color("#d8c49a"));
            }));
        }
        AddRallyFlag(world);
        foreach (var e in world.Store.Query<Plant, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var plant = e.GetComponent<Plant>();
            int max = world.Content.Plants[plant.Kind].MaxFood;
            var color = new Color(world.Content.Plants[plant.Kind].Color);
            _drawList.Add((p.Y, () => DrawBush(p, plant.Food, max, color)));
        }
        foreach (var e in world.Store.Query<Deposit, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var deposit = e.GetComponent<Deposit>();
            var def = world.Content.Deposits[deposit.Kind];
            // A deposit shrinks as it is worked out.
            float fill = Mathf.Clamp((float)deposit.Amount / def.Amount, 0.25f, 1f);
            var color = new Color(def.Color);
            bool pit = def.NearWater;
            string id = def.Id;
            _drawList.Add((p.Y, () =>
            {
                if (Art.DrawSprite(this, id, p, pit ? 1f : 0.55f + 0.45f * fill)) return;
                if (pit) DrawClayPit(p, color, fill); else DrawRocks(p, color, fill);
            }));
        }
        foreach (var e in world.Store.Query<Carcass, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            _drawList.Add((p.Y, () => { if (!Art.DrawSprite(this, "carcass", p)) DrawCarcass(p); }));
        }
        foreach (var e in world.Store.Query<Animal, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var color = new Color(world.Content.Animals[e.GetComponent<Animal>().Kind].Color);
            string id = world.Content.Animals[e.GetComponent<Animal>().Kind].Id;
            bool walking = IsWalking(e, out bool facingLeft);
            string frame = walking && StepFrame(e.Id) && Art.HasSprite(id + "_run") ? id + "_run" : id;
            _drawList.Add((p.Y, () => { if (!Art.DrawSprite(this, frame, p, flip: facingLeft)) DrawDeer(p, color, facingLeft); }));
        }
        foreach (var e in world.Store.Query<Merchant, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            int porters = world.Content.Economy.Merchants.Porters;
            bool loaded = e.GetComponent<Inventory>().Amounts.Any(n => n > 0);
            bool walking = IsWalking(e, out bool left);
            bool step = walking && StepFrame(e.Id);
            _drawList.Add((p.Y, () => DrawCaravan(p, porters, loaded, step, left)));
        }
        var rules = world.Content.Citizens;
        foreach (var e in world.Store.Query<Citizen, TilePosition>().Entities)
        {
            var p = WorldPosition(e);
            var citizen = e.GetComponent<Citizen>();
            var team = Art.PlayerColor(e.GetComponent<Owner>().Player);
            if (e.TryGetComponent<Soldier>(out var soldier) && soldier.Equipped)
            {
                AddSoldier(world, e, p, citizen, soldier, team);
                continue;
            }
            if (flags) _drawList.Add((p.Y - 0.01f, () => Ring(p, team)));
            bool adult = Bands.IsAdult(world, citizen);
            bool selected = _selection.IsSelected(e.Id);
            float health = (float)citizen.Health / rules.MaxHealth;
            string? load = citizen.Carried > 0 ? world.Content.Goods[citizen.CarriedGood].Id : null;
            Color? loadColor = citizen.Carried > 0 ? new Color(world.Content.Goods[citizen.CarriedGood].Color) : null;
            bool tool = citizen.ToolWear > 0;
            bool walking = IsWalking(e, out bool left);
            string sprite = $"villager{e.Id % 3}" + (walking && StepFrame(e.Id) ? "_walk" : "");
            _drawList.Add((p.Y, () => DrawPerson(p, adult ? 1f : 0.65f, selected, health, load, loadColor, tool, sprite, left)));
        }

        _drawList.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var (_, draw) in _drawList)
            draw();
        DrawShots(world);
    }

    // A soldier: the unit's sprite (stepping while it walks, lunging when it strikes), its side's pennant or a white flag
    // when fleeing, and a health bar once hurt.
    private void AddSoldier(World world, Entity e, Vector2 feet, Citizen citizen, Soldier soldier, Color team)
    {
        var unit = world.Content.Units[soldier.Kind];
        bool walking = IsWalking(e, out bool left);
        string sprite = walking && StepFrame(e.Id) && Art.HasSprite(unit.Def.Id + "_walk") ? unit.Def.Id + "_walk" : unit.Def.Id;
        Vector2 lunge = Vector2.Zero;
        foreach (var (attacker, target, time, ranged) in _strikes)
        {
            if (attacker != e.Id || !world.TryGetEntity(target, out var foe)) continue;
            var toward = WorldPosition(foe) - feet;
            left = toward.X < 0;
            float age = (Now - time) / StrikeSeconds;
            if (!ranged && age < 1) lunge = toward.Normalized() * 4 * (1 - age);
        }
        float health = (float)citizen.Health / unit.Def.Health;
        bool selected = _selection.IsSelected(e.Id);
        bool fleeing = Military.IsRouted(world, soldier);
        string? load = citizen.Carried > 0 ? world.Content.Goods[citizen.CarriedGood].Id : null;
        float top = unit.Role == Sim.Content.UnitRole.Siege ? -30 : unit.Def.Id == "cavalry" ? -56 : -46;
        _drawList.Add((feet.Y, () =>
        {
            var at = feet + lunge;
            Ring(at, team);
            if (selected)
            {
                DrawSetTransform(at, 0, new Vector2(1, 0.5f));
                DrawArc(Vector2.Zero, 13, 0, Mathf.Tau, 24, SelectRing, 1.5f);
                DrawSetTransform(Vector2.Zero);
            }
            if (!Art.DrawSprite(this, sprite, at, flip: left)) DrawPerson(at, 1f, false, health, null, null, false);
            if (load != null) Art.DrawIcon(this, load, at + new Vector2(left ? -8 : 8, -14), 10);
            var pole = at + new Vector2(left ? 5 : -5, top);
            DrawLine(pole, pole + new Vector2(0, 10), new Color("#5a3e22"), 1);
            DrawColoredPolygon([pole, pole + new Vector2(left ? -7 : 7, 2.5f), pole + new Vector2(0, 5)], fleeing ? Colors.White : team);
            if (health < 0.999f || selected) HealthBar(at + new Vector2(0, top - 4), health);
        }));
    }

    // A ring in the owner's colour under a person's feet.
    private void Ring(Vector2 feet, Color team)
    {
        DrawSetTransform(feet, 0, new Vector2(1, 0.5f));
        DrawArc(Vector2.Zero, 8, 0, Mathf.Tau, 20, team with { A = 0.85f }, 2f);
        DrawSetTransform(Vector2.Zero);
    }

    private void HealthBar(Vector2 center, float health)
    {
        var bar = new Rect2(center + new Vector2(-8, 0), new Vector2(16, 2.5f));
        DrawRect(bar, new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(health, 0, 1), bar.Size.Y)), health > 0.4f ? HealthGood : HealthBad);
    }

    // The controlled player's rally point: a flag in their colour.
    private void AddRallyFlag(World world)
    {
        if (!Sim.Emergence.Civics.TryGet(world, _driver.Player, out var civEntity)) return;
        var civ = civEntity.GetComponent<Civilization>();
        if (!civ.HasRally && !_selection.PlacingRally) return;
        var p = _map.TileToLocal(new Vector2(civ.RallyX, civ.RallyY));
        var team = Art.PlayerColor(_driver.Player);
        if (!civ.HasRally) return;
        _drawList.Add((p.Y, () =>
        {
            Ellipse(p, 4, 2, Shadow);
            DrawLine(p, p + new Vector2(0, -22), new Color("#5a3e22"), 1.5f);
            float wave = Mathf.Sin(Now * 4) * 1.5f;
            DrawColoredPolygon([p + new Vector2(0, -22), p + new Vector2(11, -19 + wave), p + new Vector2(0, -15)], team);
        }));
    }

    // Arrows in flight and nothing else: melee strikes show as lunges on the soldier.
    private void DrawShots(World world)
    {
        foreach (var (attacker, target, time, ranged) in _strikes)
        {
            if (!ranged || !world.TryGetEntity(attacker, out var from) || !world.TryGetEntity(target, out var to)) continue;
            float t = Mathf.Clamp((Now - time) / StrikeSeconds, 0, 1);
            var a = WorldPosition(from) + new Vector2(0, from.HasComponent<Building>() ? -40 : -16);
            var b = WorldPosition(to) + new Vector2(0, -10);
            var head = a.Lerp(b, t) + new Vector2(0, -12 * 4 * t * (1 - t)); // a little arc
            var tail = a.Lerp(b, Mathf.Max(0, t - 0.15f)) + new Vector2(0, -12 * 4 * Mathf.Max(0, t - 0.15f) * (1 - Mathf.Max(0, t - 0.15f)));
            DrawLine(tail, head, Arrow, 1.2f);
        }
    }

    private void AddBuilding(World world, Entity e, bool flags)
    {
        var type = world.Content.Buildings[e.GetComponent<Building>().Kind];
        var pos = e.GetComponent<TilePosition>();
        int size = type.Def.Size;
        var color = new Color(type.Def.Color);
        bool selected = _selection.SelectedBuilding == e.Id;
        if (e.TryGetComponent<Field>(out var field))
        {
            // Fields lie flat on the ground: draw them first, under everything standing on them.
            int stacked = type.FieldGood >= 0 ? e.GetComponent<Inventory>().Amounts[type.FieldGood] : 0;
            bool done = Buildings.IsComplete(e);
            float growth = field.Stage == FieldStage.Growing
                ? Mathf.Clamp(field.Progress / (float)(type.Def.Field!.GrowTicks * Labor.PerTick), 0, 1) : 0;
            _drawList.Add((float.MinValue, () => DrawField(pos.X, pos.Y, size, done ? field.Stage : FieldStage.Fallow, growth, stacked, selected)));
            if (!done) AddSiteBar(world, e, pos, size);
            return;
        }
        var corners = Footprint(pos.X, pos.Y, size);
        float depth = corners[2].Y;
        string id = type.Def.Id;
        AddDamage(e, type.Def.HitPoints, corners, depth, flags ? Art.PlayerColor(e.GetComponent<Owner>().Player) : null);
        if (Buildings.IsComplete(e) && type.Def.Market)
        {
            // Stalls show the goods on sale, the most plentiful first.
            var stock = e.GetComponent<Inventory>().Amounts;
            var goods = Enumerable.Range(0, stock.Length).Where(g => stock[g] > 0).OrderByDescending(g => stock[g]).Take(4)
                .Select(g => world.Content.Goods[g]).ToArray();
            _drawList.Add((depth, () => DrawMarket(id, pos.X, pos.Y, size, color, goods, selected)));
        }
        else if (Buildings.IsComplete(e) && Art.HasSprite(id))
        {
            var center = WorldPosition(e);
            // A mill's sails turn while someone works it.
            bool turning = id == "mill" && Buildings.WorkersAt(world, e.Id) > 0;
            _drawList.Add((depth, () =>
            {
                if (selected) DrawPolyline([.. corners, corners[0]], SelectRing, 1.5f);
                Art.DrawSprite(this, id, center);
                if (id == "mill") Art.DrawSprite(this, "mill_sails", center + Art.MillHub, rotation: turning ? Now * 1.2f : 0.4f);
            }));
        }
        else if (Buildings.IsComplete(e))
        {
            _drawList.Add((depth, () => DrawBuilding(id, pos.X, pos.Y, size, color, selected)));
        }
        else
        {
            int percent = Buildings.ConstructionPercent(world, e);
            _drawList.Add((depth, () => DrawSite(pos.X, pos.Y, size, color, percent, selected)));
        }
    }

    // A damaged building shows its hit points; past half damage it burns. With several civilizations it flies its
    // owner's flag.
    private void AddDamage(Entity e, int hitPoints, Vector2[] corners, float depth, Color? owner)
    {
        int damage = e.GetComponent<Building>().Damage;
        var top = corners[0];
        int id = e.Id;
        _drawList.Add((depth + 0.01f, () =>
        {
            if (owner is { } team)
            {
                var pole = top + new Vector2(0, -10);
                DrawLine(top, pole, new Color("#5a3e22"), 1);
                DrawColoredPolygon([pole, pole + new Vector2(8, 2.5f), pole + new Vector2(0, 5)], team);
            }
            if (damage <= 0) return;
            if (damage * 2 >= hitPoints)
            {
                var center = (corners[0] + corners[2]) / 2;
                for (int i = 0; i < 3; i++)
                {
                    float flicker = Mathf.Sin(Now * 9 + id + i * 2.1f);
                    var at = center + new Vector2((i - 1) * 8, -6 - i % 2 * 5);
                    DrawColoredPolygon([at + new Vector2(-4, 0), at + new Vector2(4, 0), at + new Vector2(0, -12 - 3 * flicker)], Flame);
                    DrawColoredPolygon([at + new Vector2(-2, 0), at + new Vector2(2, 0), at + new Vector2(0, -6 - 2 * flicker)], FlameCore);
                    DrawCircle(at + new Vector2(2 * flicker, -20 - 6 * ((Now + i) % 1)), 4 + 2 * ((Now + i) % 1), Smoke);
                }
            }
            HealthBar(top + new Vector2(0, -16), 1f - (float)damage / hitPoints);
        }));
    }

    private void AddSiteBar(World world, Entity e, TilePosition pos, int size)
    {
        int percent = Buildings.ConstructionPercent(world, e);
        var top = Footprint(pos.X, pos.Y, size)[0];
        _drawList.Add((float.MaxValue, () => DrawProgress(top + new Vector2(0, -8), percent)));
    }

    private void Ellipse(Vector2 center, float rx, float ry, Color color)
    {
        DrawSetTransform(center, 0, new Vector2(1, ry / rx));
        DrawCircle(Vector2.Zero, rx, color);
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawPerson(Vector2 feet, float scale, bool selected, float health, string? load, Color? loadColor, bool tool,
        string sprite = "villager0", bool left = false)
    {
        if (selected)
        {
            DrawSetTransform(feet, 0, new Vector2(1, 0.5f));
            DrawArc(Vector2.Zero, 11, 0, Mathf.Tau, 24, SelectRing, 1.5f);
            DrawSetTransform(Vector2.Zero);
        }
        float side = left ? -1 : 1;
        if (tool) DrawLine(feet + new Vector2(-6 * side, -10) * scale, feet + new Vector2(-9 * side, -21) * scale, new Color("#5f6f7a"), 1.5f);
        if (!Art.DrawSprite(this, sprite, feet, scale, left))
        {
            Ellipse(feet, 6 * scale, 3 * scale, Shadow);
            var body = feet + new Vector2(0, -9 * scale);
            DrawRect(new Rect2(body + new Vector2(-3.5f, -6) * scale, new Vector2(7, 12) * scale), Tunic);
            DrawCircle(feet + new Vector2(0, -19 * scale), 4 * scale, Skin);
            DrawCircle(feet + new Vector2(0, -21 * scale), 3 * scale, Hair);
        }
        // What they carry, held in front of them.
        var hand = feet + new Vector2(7 * side, -13) * scale;
        if (load != null && !Art.DrawIcon(this, load, hand, 10 * scale) && loadColor is { } goods)
            DrawCircle(hand, 2.8f * scale, goods);
        if (selected)
        {
            var bar = new Rect2(feet + new Vector2(-8, -30 * scale), new Vector2(16, 2.5f));
            DrawRect(bar, new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * health, bar.Size.Y)), health > 0.4f ? HealthGood : HealthBad);
        }
    }

    private void DrawDeer(Vector2 feet, Color color, bool facingLeft)
    {
        float dir = facingLeft ? -1 : 1;
        Ellipse(feet, 9, 3.5f, Shadow);
        var dark = color.Darkened(0.3f);
        for (int i = -1; i <= 1; i += 2)
        {
            DrawLine(feet + new Vector2(i * 5, -7), feet + new Vector2(i * 5, 0), dark, 1.5f);
            DrawLine(feet + new Vector2(i * 3, -7), feet + new Vector2(i * 3, 0), dark, 1.5f);
        }
        Ellipse(feet + new Vector2(0, -9), 8, 4, color);
        var head = feet + new Vector2(8 * dir, -15);
        DrawLine(feet + new Vector2(5 * dir, -10), head, color, 3);
        DrawCircle(head, 2.5f, color);
        DrawLine(head + new Vector2(-1, -2), head + new Vector2(-2 * dir, -6), dark, 1);
    }

    private void DrawCarcass(Vector2 feet)
    {
        Ellipse(feet, 9, 3.5f, Shadow);
        Ellipse(feet + new Vector2(0, -3), 8, 3.5f, Meat);
        Ellipse(feet + new Vector2(-2, -4), 3, 1.5f, Meat.Lightened(0.25f));
    }

    private void DrawBush(Vector2 feet, int food, int maxFood, Color berry)
    {
        if (food == 0)
        {
            if (Art.DrawSprite(this, "bush_bare", feet)) return;
            Ellipse(feet, 10, 4, Shadow);
            for (int i = -2; i <= 2; i++)
                DrawLine(feet, feet + new Vector2(i * 3, -10 + Math.Abs(i) * 2), BareTwig, 1.2f);
            return;
        }
        if (!Art.DrawSprite(this, "bush", feet))
        {
            Ellipse(feet, 10, 4, Shadow);
            DrawCircle(feet + new Vector2(-4, -6), 6, LeafDark);
            DrawCircle(feet + new Vector2(4, -6), 6, LeafDark);
            DrawCircle(feet + new Vector2(0, -10), 7, Leaf);
        }
        // Berries thin out as the bush is picked.
        int berries = Math.Max(1, (int)Math.Ceiling(9.0 * food / maxFood));
        for (int i = 0; i < berries; i++)
        {
            float angle = i * 2.4f;
            var at = feet + new Vector2(0, -8) + new Vector2(Mathf.Cos(angle) * 1.3f, Mathf.Sin(angle) * 0.8f) * (2 + i % 3 * 2);
            DrawCircle(at, 1.9f, berry.Darkened(0.4f));
            DrawCircle(at, 1.5f, berry);
        }
    }

    private void DrawRocks(Vector2 feet, Color stone, float fill)
    {
        Ellipse(feet, 13 * fill + 4, 5 * fill + 2, Shadow);
        var dark = stone.Darkened(0.35f);
        DrawColoredPolygon([feet + new Vector2(-11, 0) * fill, feet + new Vector2(-7, -10) * fill, feet + new Vector2(1, -13) * fill,
            feet + new Vector2(6, -6) * fill, feet + new Vector2(3, 1) * fill], stone);
        DrawColoredPolygon([feet + new Vector2(1, -13) * fill, feet + new Vector2(6, -6) * fill, feet + new Vector2(3, 1) * fill,
            feet + new Vector2(-1, -5) * fill], dark);
        DrawColoredPolygon([feet + new Vector2(4, 1), feet + new Vector2(7, -6) * fill, feet + new Vector2(12, -4) * fill,
            feet + new Vector2(11, 2)], stone.Lightened(0.1f));
    }

    private void DrawClayPit(Vector2 feet, Color clay, float fill)
    {
        Ellipse(feet, 12, 5.5f, clay.Darkened(0.2f));
        Ellipse(feet + new Vector2(0, 0.5f), 9 * fill, 4 * fill, clay.Darkened(0.5f));
        Ellipse(feet + new Vector2(-6, -2), 3, 1.5f, clay.Lightened(0.15f));
    }

    private void DrawCamp(Vector2 feet)
    {
        Ellipse(feet, 22, 9, Shadow);
        var left = feet + new Vector2(-14, 2);
        DrawColoredPolygon([left + new Vector2(-12, 0), left + new Vector2(12, 0), left + new Vector2(0, -22)], Tent);
        DrawColoredPolygon([left + new Vector2(0, -22), left + new Vector2(12, 0), left + new Vector2(5, 0)], TentDark);
        var right = feet + new Vector2(14, -4);
        DrawColoredPolygon([right + new Vector2(-10, 0), right + new Vector2(10, 0), right + new Vector2(0, -18)], Tent);
        DrawColoredPolygon([right + new Vector2(0, -18), right + new Vector2(10, 0), right + new Vector2(4, 0)], TentDark);
        DrawCircle(feet + new Vector2(0, 6), 4, Fire.Darkened(0.4f));
        DrawColoredPolygon([feet + new Vector2(-3, 6), feet + new Vector2(3, 6), feet + new Vector2(0, -3)], Fire);
    }

    private void DrawField(int x, int y, int size, FieldStage stage, float growth, int stacked, bool selected)
    {
        var ground = Footprint(x, y, size, 0.05f);
        DrawColoredPolygon(ground, Soil);
        var rows = stage switch
        {
            FieldStage.Ripe => RipeCrop,
            FieldStage.Growing => Soil.Lerp(Sprout, 0.35f + 0.65f * growth),
            _ => Soil.Darkened(0.15f),
        };
        // Furrows: stripes running across the field.
        for (int i = 0; i < size * 3; i++)
        {
            float t = (i + 0.5f) / (size * 3);
            var a = ground[0].Lerp(ground[3], t);
            var b = ground[1].Lerp(ground[2], t);
            DrawLine(a, b, rows, stage == FieldStage.Fallow ? 1f : 2.5f);
        }
        if (stacked > 0)
        {
            var center = _map.TileToLocal(new Vector2(x + size - 1, y + size - 1));
            int sheaves = Math.Min(4, 1 + stacked / 40);
            for (int i = 0; i < sheaves; i++)
            {
                var p = center + new Vector2(-8 + i * 5, -i % 2 * 3);
                DrawColoredPolygon([p + new Vector2(-3, 0), p + new Vector2(3, 0), p + new Vector2(0, -10)], RipeCrop.Darkened(0.1f));
            }
        }
        if (selected) DrawPolyline([.. ground, ground[0]], SelectRing, 1.5f);
    }

    // A simple house: walls on the footprint and a roof. Details depend on the building type.
    private void DrawBuilding(string id, int x, int y, int size, Color color, bool selected)
    {
        float inset = size == 1 ? 0.12f : 0.08f;
        var c = Footprint(x, y, size, inset);
        float wall = id switch { "storehouse" => 16, "shrine" => 10, "hut" => 9, _ => 13 };
        float roof = id switch { "storehouse" => 10, "shrine" => 16, "hut" => 16, _ => 12 };
        var up = new Vector2(0, -wall);
        var shade = color.Darkened(0.25f);
        Ellipse((c[1] + c[3]) / 2 + new Vector2(0, 2), (c[1].X - c[3].X) / 2 + 4, (c[2].Y - c[0].Y) / 2 + 2, Shadow);
        if (selected) DrawPolyline([.. c, c[0]], SelectRing, 1.5f);
        DrawColoredPolygon([c[3], c[2], c[2] + up, c[3] + up], color);
        DrawColoredPolygon([c[2], c[1], c[1] + up, c[2] + up], shade);
        var roofColor = id == "hut" ? Thatch : id == "shrine" ? new Color("#e9e2cf") : color.Darkened(0.45f);
        var peak = (c[0] + c[2]) / 2 + up + new Vector2(0, -roof);
        DrawColoredPolygon([c[3] + up, c[2] + up, peak], roofColor);
        DrawColoredPolygon([c[2] + up, c[1] + up, peak], roofColor.Darkened(0.2f));
        var door = (c[2] + c[3]) / 2;
        DrawRect(new Rect2(door + new Vector2(-2.5f, -7), new Vector2(5, 7)), new Color("#3a2a1a"));

        switch (id)
        {
            case "mill":
                var hub = (c[1] + c[2]) / 2 + new Vector2(4, -wall * 0.7f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2 + 0.4f;
                    DrawLine(hub, hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11, new Color("#efe6cf"), 2);
                }
                DrawCircle(hub, 2, new Color("#5a4632"));
                break;
            case "bakery":
                var chimney = peak + new Vector2(6, 6);
                DrawRect(new Rect2(chimney, new Vector2(4, 9)), new Color("#8a4a32"));
                DrawCircle(chimney + new Vector2(2, -4), 3, new Color(0.85f, 0.85f, 0.85f, 0.6f));
                break;
            case "toolmaker":
                var anvil = (c[2] + c[1]) / 2 + new Vector2(8, 2);
                DrawRect(new Rect2(anvil + new Vector2(-4, -5), new Vector2(8, 3)), new Color("#4a5560"));
                DrawRect(new Rect2(anvil + new Vector2(-1.5f, -2), new Vector2(3, 3)), new Color("#4a5560"));
                break;
            case "storehouse":
                var crates = (c[3] + c[2]) / 2 + new Vector2(-10, 2);
                DrawRect(new Rect2(crates + new Vector2(-4, -6), new Vector2(7, 6)), new Color("#9a7448"));
                DrawRect(new Rect2(crates + new Vector2(3, -5), new Vector2(6, 5)), new Color("#8a6a3c"));
                break;
            case "shrine":
                DrawCircle(peak + new Vector2(0, -3), 2.5f, new Color("#f2c94c"));
                break;
            case "mint":
                var coin = (c[2] + c[1]) / 2 + new Vector2(6, -wall * 0.55f);
                DrawCircle(coin, 4, new Color("#d8dde2"));
                DrawArc(coin, 4, 0, Mathf.Tau, 16, new Color("#8d8f96"), 1);
                break;
        }
    }

    // A marketplace: an open square with four stalls under striped awnings, and the goods on sale.
    private void DrawMarket(string id, int x, int y, int size, Color color, Sim.Content.GoodDef[] goods, bool selected)
    {
        var c = Footprint(x, y, size, 0.06f);
        var center = (c[0] + c[2]) / 2;
        // Stalls back to front, as in the sprite: back, left, right, front.
        Vector2[] stalls = [(c[0] + center) / 2, (c[3] + center) / 2, (c[1] + center) / 2, (c[2] + center) / 2];
        if (Art.DrawSprite(this, id, center))
        {
            if (selected) DrawPolyline([.. c, c[0]], SelectRing, 1.5f);
            for (int i = 0; i < goods.Length && i < stalls.Length; i++)
            {
                if (!Art.DrawIcon(this, goods[i].Id, stalls[i] + new Vector2(0, -7), 9))
                    Ellipse(stalls[i] + new Vector2(0, -6), 5, 2.5f, new Color(goods[i].Color));
            }
            return;
        }
        DrawColoredPolygon(c, color.Darkened(0.35f) with { A = 0.55f });
        if (selected) DrawPolyline([.. c, c[0]], SelectRing, 1.5f);
        for (int i = 0; i < stalls.Length; i++)
        {
            var s = stalls[i];
            DrawRect(new Rect2(s + new Vector2(-8, -5), new Vector2(16, 5)), new Color("#7a5a3a"));
            var awning = i % 2 == 0 ? new Color("#c0504d") : new Color("#e9e2cf");
            DrawColoredPolygon([s + new Vector2(-10, -12), s + new Vector2(10, -12), s + new Vector2(8, -6), s + new Vector2(-8, -6)], awning);
            DrawLine(s + new Vector2(-8, -6), s + new Vector2(-8, 0), new Color("#5a4632"), 1);
            DrawLine(s + new Vector2(8, -6), s + new Vector2(8, 0), new Color("#5a4632"), 1);
            if (i < goods.Length) Ellipse(s + new Vector2(0, -6), 5, 2.5f, new Color(goods[i].Color));
        }
    }

    // A merchant caravan: a pack animal and porters in travelling cloaks.
    private void DrawCaravan(Vector2 feet, int porters, bool loaded, bool step = false, bool left = false)
    {
        if (Art.DrawSprite(this, loaded ? "mule_loaded" : "mule", feet + new Vector2(-6, 0), flip: left))
        {
            for (int i = 0; i < porters; i++)
                Art.DrawSprite(this, step == (i % 2 == 0) ? "porter_walk" : "porter", feet + new Vector2(8 + i * 8, 2 + i * 2), flip: left);
            return;
        }
        Ellipse(feet + new Vector2(0, 2), 14, 5, Shadow);
        var mule = feet + new Vector2(-6, -7);
        Ellipse(mule, 8, 4.5f, new Color("#7a6a58"));
        DrawLine(mule + new Vector2(6, 0), mule + new Vector2(11, -5), new Color("#7a6a58"), 3);
        for (int leg = -1; leg <= 1; leg += 2) DrawLine(mule + new Vector2(leg * 5, 3), mule + new Vector2(leg * 5, 8), new Color("#5a4a3a"), 1.5f);
        if (loaded)
        {
            DrawRect(new Rect2(mule + new Vector2(-6, -8), new Vector2(5, 5)), new Color("#c49a5a"));
            DrawRect(new Rect2(mule + new Vector2(0, -8), new Vector2(5, 5)), new Color("#a8324a"));
        }
        for (int i = 0; i < porters; i++)
            DrawPerson(feet + new Vector2(6 + i * 7, i * 2), 0.9f, false, 1f, null, loaded ? new Color("#c49a5a") : null, false);
    }

    // A construction site: the footprint marked out, a scaffold growing with progress, and a progress bar.
    private void DrawSite(int x, int y, int size, Color color, int percent, bool selected)
    {
        var c = Footprint(x, y, size, 0.1f);
        DrawColoredPolygon(c, color with { A = 0.18f });
        DrawPolyline([.. c, c[0]], selected ? SelectRing : Scaffold, selected ? 1.5f : 1f);
        float height = 4 + 14 * percent / 100f;
        foreach (var corner in c)
            DrawLine(corner, corner + new Vector2(0, -height), Scaffold, 1.5f);
        if (percent >= 50)
        {
            var up = new Vector2(0, -height);
            DrawColoredPolygon([c[3], c[2], c[2] + up, c[3] + up], color with { A = 0.55f });
            DrawColoredPolygon([c[2], c[1], c[1] + up, c[2] + up], color.Darkened(0.25f) with { A = 0.55f });
        }
        DrawProgress(c[0] + new Vector2(0, -height - 8), percent);
    }

    private void DrawProgress(Vector2 center, int percent)
    {
        var bar = new Rect2(center + new Vector2(-12, 0), new Vector2(24, 3));
        DrawRect(bar, new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * percent / 100f, bar.Size.Y)), new Color("#f2c94c"));
    }
}
