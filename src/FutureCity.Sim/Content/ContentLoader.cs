using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Content;

/// <summary>
/// Parses and validates content JSON. Unknown properties, missing required properties,
/// duplicate ids and broken references are all errors; every error is reported at once.
/// </summary>
public static partial class ContentLoader
{
    internal const string RulesFile = "rules.json";
    internal const string TerrainFile = "terrain.json";
    internal const string CitizensFile = "citizens.json";
    internal const string NatureFile = "nature.json";
    internal const string GoodsFile = "goods.json";
    internal const string BuildingsFile = "buildings.json";
    internal const string ProgressFile = "progress.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads content from (relative path, JSON text) pairs, e.g. <c>GameContent.ReadAll()</c>.</summary>
    /// <exception cref="ContentException">If anything is missing or invalid.</exception>
    public static ContentDatabase Load(IEnumerable<KeyValuePair<string, string>> files)
    {
        var byPath = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text) in files)
            byPath[path.Replace('\\', '/')] = text;

        var errors = new List<string>();
        var rules = Parse<GameRules>(byPath, RulesFile, errors);
        var terrainFile = Parse<TerrainFile>(byPath, TerrainFile, errors);
        var citizens = Parse<CitizenRules>(byPath, CitizensFile, errors);
        var nature = Parse<NatureFile>(byPath, NatureFile, errors);
        var goods = Parse<GoodsFile>(byPath, GoodsFile, errors);
        var buildings = Parse<BuildingsFile>(byPath, BuildingsFile, errors);
        var progress = Parse<ProgressRules>(byPath, ProgressFile, errors);

        if (goods != null)
            ValidateGoods(goods, errors);
        var goodIds = goods?.Goods.Select(g => g.Id).ToHashSet() ?? [];
        if (rules != null && terrainFile != null)
            Validate(rules, terrainFile.Terrains, goodIds, errors);
        if (citizens != null)
            ValidateCitizens(citizens, goodIds, errors);
        if (nature != null)
            ValidateNature(nature, goodIds, errors);
        if (buildings != null)
            ValidateBuildings(buildings, goodIds, errors);
        if (progress != null)
            ValidateProgress(progress, errors);
        if (goods != null && buildings != null && progress != null)
            ValidateConditions(goods, buildings, progress, errors);

        if (errors.Count > 0)
            throw new ContentException(errors);

        var parts = new ContentParts(rules!, terrainFile!.Terrains, citizens!, nature!, goods!, buildings!.Buildings, progress!);
        return new ContentDatabase(parts, ComputeHash(byPath));
    }

    /// <summary>Loads content from every *.json file under a directory (useful for modding and tools).</summary>
    public static ContentDatabase LoadDirectory(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Select(f => new KeyValuePair<string, string>(Path.GetRelativePath(directory, f), File.ReadAllText(f)));
        return Load(files);
    }

    private static T? Parse<T>(SortedDictionary<string, string> files, string path, List<string> errors) where T : class
    {
        if (!files.TryGetValue(path, out var text))
        {
            errors.Add($"{path}: file is missing.");
            return null;
        }
        try
        {
            var value = JsonSerializer.Deserialize<T>(text, Options);
            if (value == null) errors.Add($"{path}: file is empty.");
            return value;
        }
        catch (JsonException e)
        {
            errors.Add($"{path}: {e.Message}");
            return null;
        }
    }

    private static void Validate(GameRules rules, IReadOnlyList<TerrainDef> terrains, HashSet<string> goods, List<string> errors)
    {
        CheckIds(TerrainFile, "terrain", terrains.Select(t => t.Id), errors);
        CheckIds(RulesFile, "map size", rules.MapSizes.Select(m => m.Id), errors);

        if (terrains.Count == 0)
            errors.Add($"{TerrainFile}: at least one terrain is required.");
        if (terrains.Count > byte.MaxValue + 1)
            errors.Add($"{TerrainFile}: at most {byte.MaxValue + 1} terrains are supported.");
        foreach (var t in terrains)
        {
            CheckColor(TerrainFile, $"terrain '{t.Id}'", t.Color, errors);
            CheckRange(TerrainFile, $"terrain '{t.Id}' moveCost", t.MoveCost, 10, 1000, errors);
            if (t.Resource is not { } r) continue;
            string n = $"terrain '{t.Id}' resource";
            CheckGood(TerrainFile, n, r.Good, goods, errors);
            CheckRange(TerrainFile, n + " amount", r.Amount, 1, ushort.MaxValue, errors);
            CheckRange(TerrainFile, n + " ticksPerUnit", r.TicksPerUnit, 1, 10_000, errors);
            CheckWork(TerrainFile, n, r.Work, errors);
            if (terrains.FirstOrDefault(d => d.Id == r.DepletedTerrain) is not { Walkable: true, Resource: null })
                errors.Add($"{TerrainFile}: {n} depletedTerrain '{r.DepletedTerrain}' must be a walkable terrain without a resource.");
        }

        if (rules.MapSizes.Count == 0)
            errors.Add($"{RulesFile}: at least one map size is required.");
        foreach (var m in rules.MapSizes)
        {
            if (m.Width is < 16 or > 512 || m.Height is < 16 or > 512)
                errors.Add($"{RulesFile}: map size '{m.Id}' must be between 16 and 512 tiles on each side.");
        }
        if (rules.MapSizes.All(m => m.Id != rules.DefaultMapSize))
            errors.Add($"{RulesFile}: defaultMapSize '{rules.DefaultMapSize}' is not a defined map size.");
        if (terrains.All(t => t.Id != rules.DefaultTerrain))
            errors.Add($"{RulesFile}: defaultTerrain '{rules.DefaultTerrain}' is not a defined terrain.");
        else if (terrains.First(t => t.Id == rules.DefaultTerrain) is { Walkable: false } or { Buildable: false })
            errors.Add($"{RulesFile}: defaultTerrain '{rules.DefaultTerrain}' must be walkable and buildable.");

        var gen = rules.MapGeneration;
        if (terrains.All(t => t.Id != gen.WaterTerrain))
            errors.Add($"{RulesFile}: mapGeneration.waterTerrain '{gen.WaterTerrain}' is not a defined terrain.");
        if (terrains.FirstOrDefault(t => t.Id == gen.ForestTerrain) is not { Walkable: true })
            errors.Add($"{RulesFile}: mapGeneration.forestTerrain '{gen.ForestTerrain}' must be a defined, walkable terrain.");
        CheckRange(RulesFile, "mapGeneration.waterPercent", gen.WaterPercent, 0, 60, errors);
        CheckRange(RulesFile, "mapGeneration.forestPercent", gen.ForestPercent, 0, 80, errors);
        CheckRange(RulesFile, "mapGeneration.featureSize", gen.FeatureSize, 4, 64, errors);
        CheckRange(RulesFile, "mapGeneration.startClearRadius", gen.StartClearRadius, 1, 8, errors);
        CheckRange(RulesFile, "mapGeneration.fertility.min", gen.Fertility.Min, 0, 100, errors);
        CheckRange(RulesFile, "mapGeneration.fertility.max", gen.Fertility.Max, gen.Fertility.Min, 100, errors);
        CheckRange(RulesFile, "mapGeneration.fertility.waterBonus", gen.Fertility.WaterBonus, 0, 100, errors);
        CheckRange(RulesFile, "mapGeneration.fertility.waterRadius", gen.Fertility.WaterRadius, 0, 16, errors);

        var calendar = rules.Calendar;
        CheckIds(RulesFile, "season", calendar.Seasons.Select(s => s.Id), errors);
        if (calendar.Seasons.Count == 0)
            errors.Add($"{RulesFile}: calendar needs at least one season.");
        else if (calendar.TicksPerYear % calendar.Seasons.Count != 0)
            errors.Add($"{RulesFile}: calendar.ticksPerYear must be divisible by the number of seasons.");
        CheckRange(RulesFile, "calendar.ticksPerYear", calendar.TicksPerYear, 4, 100_000, errors);
        foreach (var s in calendar.Seasons)
        {
            CheckRange(RulesFile, $"season '{s.Id}' cropGrowthPercent", s.CropGrowthPercent, 0, 1000, errors);
            CheckRange(RulesFile, $"season '{s.Id}' plantRegrowPercent", s.PlantRegrowPercent, 0, 1000, errors);
        }
    }

    private static void ValidateGoods(GoodsFile goods, List<string> errors)
    {
        const string f = GoodsFile;
        CheckIds(f, "good", goods.Goods.Select(g => g.Id), errors);
        foreach (var g in goods.Goods)
        {
            CheckColor(f, $"good '{g.Id}'", g.Color, errors);
            CheckRange(f, $"good '{g.Id}' nutrition", g.Nutrition, 0, 1000, errors);
            CheckRange(f, $"good '{g.Id}' targetPerCapita", g.TargetPerCapita, 0, 10_000, errors);
        }
        if (goods.Goods.All(g => g.Nutrition == 0))
            errors.Add($"{f}: at least one good must be food (nutrition above 0).");
        CheckGood(f, "tools", goods.Tools.Good, goods.Goods.Select(g => g.Id).ToHashSet(), errors);
        CheckRange(f, "tools.durability", goods.Tools.Durability, 1, 1_000_000, errors);
        CheckRange(f, "tools.speedBonusPercent", goods.Tools.SpeedBonusPercent, 0, 1000, errors);
    }

    private static void ValidateCitizens(CitizenRules c, HashSet<string> goods, List<string> errors)
    {
        const string f = CitizensFile;
        CheckRange(f, "adultAgeYears", c.AdultAgeYears, 0, 100, errors);
        CheckRange(f, "oldAgeYears", c.OldAgeYears, c.AdultAgeYears, 200, errors);
        CheckRange(f, "oldAgeDeathPercentPerYear", c.OldAgeDeathPercentPerYear, 1, 100, errors);
        CheckRange(f, "maxHealth", c.MaxHealth, 1, 1_000_000, errors);
        CheckRange(f, "healthRegenPerTick", c.HealthRegenPerTick, 0, c.MaxHealth, errors);
        CheckRange(f, "maxHunger", c.MaxHunger, 1, 1_000_000, errors);
        CheckRange(f, "hungerPerTick", c.HungerPerTick, 0, c.MaxHunger, errors);
        CheckRange(f, "eatAtHunger", c.EatAtHunger, 0, c.MaxHunger, errors);
        CheckRange(f, "foodPerMeal", c.FoodPerMeal, 1, 10_000, errors);
        CheckRange(f, "hungerPerMeal", c.HungerPerMeal, 1, c.MaxHunger, errors);
        CheckRange(f, "starvationDamagePerTick", c.StarvationDamagePerTick, 1, c.MaxHealth, errors);
        CheckRange(f, "moveTicksPerTile", c.MoveTicksPerTile, 1, 100, errors);
        CheckRange(f, "carryCapacity", c.CarryCapacity, 1, 10_000, errors);
        CheckRange(f, "start.citizens", c.Start.Citizens, 1, 100, errors);
        CheckAmounts(f, "start.goods", c.Start.Goods, goods, errors);
        CheckRange(f, "start.minAgeYears", c.Start.MinAgeYears, 0, 200, errors);
        CheckRange(f, "start.maxAgeYears", c.Start.MaxAgeYears, Math.Max(c.Start.MinAgeYears, c.AdultAgeYears), 200, errors);
        CheckRange(f, "camp.shelter", c.Camp.Shelter, 1, 10_000, errors);
        CheckRange(f, "growth.checkIntervalTicks", c.Growth.CheckIntervalTicks, 1, 100_000, errors);
        CheckRange(f, "growth.foodPerCapita", c.Growth.FoodPerCapita, 0, 1_000_000, errors);
        CheckRange(f, "growth.birthFoodCost", c.Growth.BirthFoodCost, 0, 1_000_000, errors);
        CheckRange(f, "growth.birthChancePercent", c.Growth.BirthChancePercent, 0, 100, errors);
        CheckRange(f, "growth.populationCap", c.Growth.PopulationCap, 1, 10_000, errors);
        CheckRange(f, "jobs.checkIntervalTicks", c.Jobs.CheckIntervalTicks, 1, 100_000, errors);
        CheckRange(f, "jobs.foodTargetPerCapita", c.Jobs.FoodTargetPerCapita, 0, 100_000, errors);
        CheckRange(f, "jobs.buildPriority", c.Jobs.BuildPriority, 0, 1_000_000, errors);
        CheckRange(f, "jobs.researchPriority", c.Jobs.ResearchPriority, 0, 1_000_000, errors);
        CheckRange(f, "jobs.maxBuildersPerSite", c.Jobs.MaxBuildersPerSite, 1, 100, errors);
        CheckRange(f, "jobs.gatherRadius", c.Jobs.GatherRadius, 1, 512, errors);
    }

    private static void ValidateNature(NatureFile nature, HashSet<string> goods, List<string> errors)
    {
        const string f = NatureFile;
        CheckIds(f, "plant", nature.Plants.Select(p => p.Id), errors);
        CheckIds(f, "animal", nature.Animals.Select(a => a.Id), errors);
        CheckIds(f, "deposit", nature.Deposits.Select(d => d.Id), errors);
        foreach (var p in nature.Plants)
        {
            string n = $"plant '{p.Id}'";
            CheckColor(f, n, p.Color, errors);
            CheckGood(f, n, p.Good, goods, errors);
            CheckRange(f, n + " maxFood", p.MaxFood, 1, 100_000, errors);
            CheckRange(f, n + " ticksPerFood", p.TicksPerFood, 1, 10_000, errors);
            CheckRange(f, n + " regrowIntervalTicks", p.RegrowIntervalTicks, 1, 100_000, errors);
            CheckRange(f, n + " regrowAmount", p.RegrowAmount, 0, p.MaxFood, errors);
            CheckRange(f, n + " dormantTicks", p.DormantTicks, 0, 1_000_000, errors);
            CheckRange(f, n + " clustersPer10kTiles", p.ClustersPer10kTiles, 0, 1000, errors);
            CheckRange(f, n + " minClusterSize", p.MinClusterSize, 1, 25, errors);
            CheckRange(f, n + " maxClusterSize", p.MaxClusterSize, p.MinClusterSize, 25, errors);
            CheckRange(f, n + " startClusters", p.StartClusters, 0, 10, errors);
        }
        foreach (var a in nature.Animals)
        {
            string n = $"animal '{a.Id}'";
            CheckColor(f, n, a.Color, errors);
            CheckGood(f, n, a.Good, goods, errors);
            CheckRange(f, n + " food", a.Food, 1, 100_000, errors);
            CheckRange(f, n + " killTicks", a.KillTicks, 1, 10_000, errors);
            CheckRange(f, n + " butcherTicksPerFood", a.ButcherTicksPerFood, 1, 10_000, errors);
            CheckRange(f, n + " carcassDecayIntervalTicks", a.CarcassDecayIntervalTicks, 1, 100_000, errors);
            CheckRange(f, n + " moveTicksPerTile", a.MoveTicksPerTile, 1, 100, errors);
            CheckRange(f, n + " wanderRadius", a.WanderRadius, 0, 64, errors);
            CheckRange(f, n + " wanderChancePerTick", a.WanderChancePerTick, 0, 100, errors);
            CheckRange(f, n + " breedIntervalTicks", a.BreedIntervalTicks, 1, 100_000, errors);
            CheckRange(f, n + " breedChancePercent", a.BreedChancePercent, 0, 100, errors);
            CheckRange(f, n + " localCapacity", a.LocalCapacity, 1, 1000, errors);
            CheckRange(f, n + " capacityRadius", a.CapacityRadius, 1, 64, errors);
            CheckRange(f, n + " herdsPer10kTiles", a.HerdsPer10kTiles, 0, 1000, errors);
            CheckRange(f, n + " minHerdSize", a.MinHerdSize, 1, 50, errors);
            CheckRange(f, n + " maxHerdSize", a.MaxHerdSize, a.MinHerdSize, 50, errors);
            CheckRange(f, n + " startHerds", a.StartHerds, 0, 10, errors);
        }
        foreach (var d in nature.Deposits)
        {
            string n = $"deposit '{d.Id}'";
            CheckColor(f, n, d.Color, errors);
            CheckGood(f, n, d.Good, goods, errors);
            CheckWork(f, n, d.Work, errors);
            CheckRange(f, n + " amount", d.Amount, 1, 1_000_000, errors);
            CheckRange(f, n + " ticksPerUnit", d.TicksPerUnit, 1, 10_000, errors);
            CheckRange(f, n + " clustersPer10kTiles", d.ClustersPer10kTiles, 0, 1000, errors);
            CheckRange(f, n + " minClusterSize", d.MinClusterSize, 1, 25, errors);
            CheckRange(f, n + " maxClusterSize", d.MaxClusterSize, d.MinClusterSize, 25, errors);
            CheckRange(f, n + " startClusters", d.StartClusters, 0, 10, errors);
        }
    }

    private static void ValidateBuildings(BuildingsFile file, HashSet<string> goods, List<string> errors)
    {
        const string f = BuildingsFile;
        CheckIds(f, "building", file.Buildings.Select(b => b.Id), errors);
        foreach (var b in file.Buildings)
        {
            string n = $"building '{b.Id}'";
            CheckColor(f, n, b.Color, errors);
            CheckRange(f, n + " size", b.Size, 1, 4, errors);
            CheckAmounts(f, n + " cost", b.Cost, goods, errors);
            CheckRange(f, n + " buildWork", b.BuildWork, 1, 100_000, errors);
            CheckRange(f, n + " shelter", b.Shelter, 0, 1000, errors);
            CheckRange(f, n + " workers", b.Workers, 0, 20, errors);
            CheckRange(f, n + " research", b.Research, 0, 1000, errors);
            int jobs = (b.Recipe != null ? 1 : 0) + (b.Field != null ? 1 : 0) + (b.Research > 0 ? 1 : 0);
            if (jobs > 1)
                errors.Add($"{f}: {n} can have only one of recipe, field and research.");
            if (jobs == 1 && b.Workers == 0)
                errors.Add($"{f}: {n} needs workers for its recipe, field or research.");
            if (b.Recipe is { } r)
            {
                CheckAmounts(f, n + " recipe inputs", r.Inputs, goods, errors);
                CheckAmounts(f, n + " recipe outputs", r.Outputs, goods, errors);
                if (r.Outputs.Values.Sum() == 0)
                    errors.Add($"{f}: {n} recipe must produce something.");
                CheckRange(f, n + " recipe workTicks", r.WorkTicks, 1, 100_000, errors);
            }
            if (b.Field is { } field)
            {
                CheckGood(f, n + " field", field.Good, goods, errors);
                CheckRange(f, n + " field sowWork", field.SowWork, 1, 100_000, errors);
                CheckRange(f, n + " field growTicks", field.GrowTicks, 1, 1_000_000, errors);
                CheckRange(f, n + " field yield", field.Yield, 1, 100_000, errors);
                CheckRange(f, n + " field harvestTicksPerUnit", field.HarvestTicksPerUnit, 1, 10_000, errors);
                CheckRange(f, n + " field fertilityPerHarvest", field.FertilityPerHarvest, 0, 100, errors);
                CheckRange(f, n + " field fertilityRecoveryIntervalTicks", field.FertilityRecoveryIntervalTicks, 1, 100_000, errors);
            }
        }
    }

    private static void ValidateProgress(ProgressRules p, List<string> errors)
    {
        const string f = ProgressFile;
        CheckRange(f, "evaluationIntervalTicks", p.EvaluationIntervalTicks, 1, 100_000, errors);
        CheckRange(f, "luckPercent", p.LuckPercent, 0, 100, errors);
        CheckIds(f, "tech", p.Techs.Select(t => t.Id), errors);
        CheckIds(f, "institution", p.Institutions.Select(i => i.Id), errors);
        CheckIds(f, "era", p.Eras.Select(e => e.Id), errors);
        foreach (var t in p.Techs)
        {
            CheckRange(f, $"tech '{t.Id}' cost", t.Cost, 1, 10_000_000, errors);
            foreach (var (work, weight) in t.Activity)
            {
                CheckWork(f, $"tech '{t.Id}' activity", work, errors);
                CheckRange(f, $"tech '{t.Id}' activity '{work}'", weight, 0, 100_000, errors);
            }
        }
        foreach (var i in p.Institutions)
            CheckRange(f, $"institution '{i.Id}' foodCost", i.FoodCost, 0, 1_000_000, errors);
        if (p.Eras.Count == 0)
            errors.Add($"{f}: at least one era is required.");
    }

    // Conditions refer to goods, buildings, techs and institutions, so they are checked once all those are known.
    private static void ValidateConditions(GoodsFile goods, BuildingsFile buildings, ProgressRules progress, List<string> errors)
    {
        var goodIndex = ContentDatabase.Index(goods.Goods, g => g.Id);
        var buildingIndex = ContentDatabase.Index(buildings.Buildings, b => b.Id);
        var techIndex = ContentDatabase.Index(progress.Techs, t => t.Id);
        var institutionIndex = ContentDatabase.Index(progress.Institutions, i => i.Id);
        FactRef? Resolve(string name) => ContentDatabase.ResolveFact(name, goodIndex, buildingIndex, techIndex, institutionIndex);

        void Check(string file, string owner, string text)
        {
            try
            {
                Condition.Parse(text, Resolve);
            }
            catch (ConditionException e)
            {
                errors.Add($"{file}: {owner}: {e.Message}.");
            }
        }

        foreach (var b in buildings.Buildings) Check(BuildingsFile, $"building '{b.Id}' requires", b.Requires);
        foreach (var t in progress.Techs) Check(ProgressFile, $"tech '{t.Id}' preconditions", t.Preconditions);
        foreach (var i in progress.Institutions) Check(ProgressFile, $"institution '{i.Id}' preconditions", i.Preconditions);
        foreach (var e in progress.Eras) Check(ProgressFile, $"era '{e.Id}' preconditions", e.Preconditions);
    }

    private static void CheckRange(string file, string name, int value, int min, int max, List<string> errors)
    {
        if (value < min || value > max)
            errors.Add($"{file}: {name} is {value} but must be between {min} and {max}.");
    }

    private static void CheckGood(string file, string owner, string good, HashSet<string> goods, List<string> errors)
    {
        if (!goods.Contains(good))
            errors.Add($"{file}: {owner} refers to unknown good '{good}'.");
    }

    private static void CheckAmounts(string file, string owner, IReadOnlyDictionary<string, int> amounts, HashSet<string> goods,
        List<string> errors)
    {
        foreach (var (good, amount) in amounts)
        {
            CheckGood(file, owner, good, goods, errors);
            CheckRange(file, $"{owner} '{good}'", amount, 0, 1_000_000, errors);
        }
    }

    private static void CheckWork(string file, string owner, string work, List<string> errors)
    {
        if (!Enum.GetValues<WorkKind>().Any(k => ContentDatabase.WorkName(k) == work))
            errors.Add($"{file}: {owner} work '{work}' must be one of: " +
                       string.Join(", ", Enum.GetValues<WorkKind>().Select(ContentDatabase.WorkName)) + ".");
    }

    private static void CheckColor(string file, string owner, string color, List<string> errors)
    {
        if (!ColorPattern().IsMatch(color))
            errors.Add($"{file}: {owner} color '{color}' must be in #rrggbb form.");
    }

    private static void CheckIds(string file, string kind, IEnumerable<string> ids, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!IdPattern().IsMatch(id))
                errors.Add($"{file}: {kind} id '{id}' must be lower-case letters, digits, '_' or '-'.");
            else if (!seen.Add(id))
                errors.Add($"{file}: duplicate {kind} id '{id}'.");
        }
    }

    private static string ComputeHash(SortedDictionary<string, string> files)
    {
        var builder = new StringBuilder();
        foreach (var (path, text) in files)
            builder.Append(path).Append('\0').Append(text.ReplaceLineEndings("\n")).Append('\0');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    [GeneratedRegex("^[a-z0-9_-]+$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}
