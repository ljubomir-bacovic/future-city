using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

        if (rules != null && terrainFile != null)
            Validate(rules, terrainFile.Terrains, errors);
        if (citizens != null)
            ValidateCitizens(citizens, errors);
        if (nature != null)
            ValidateNature(nature, errors);

        if (errors.Count > 0)
            throw new ContentException(errors);

        return new ContentDatabase(rules!, terrainFile!.Terrains, citizens!, nature!.Plants, nature.Animals, ComputeHash(byPath));
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

    private static void Validate(GameRules rules, IReadOnlyList<TerrainDef> terrains, List<string> errors)
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
    }

    private static void ValidateCitizens(CitizenRules c, List<string> errors)
    {
        const string f = CitizensFile;
        CheckRange(f, "ticksPerYear", c.TicksPerYear, 1, 100_000, errors);
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
        CheckRange(f, "start.food", c.Start.Food, 0, 1_000_000, errors);
        CheckRange(f, "start.minAgeYears", c.Start.MinAgeYears, c.AdultAgeYears, 200, errors);
        CheckRange(f, "start.maxAgeYears", c.Start.MaxAgeYears, c.Start.MinAgeYears, 200, errors);
        CheckRange(f, "camp.shelter", c.Camp.Shelter, 1, 10_000, errors);
        CheckRange(f, "growth.checkIntervalTicks", c.Growth.CheckIntervalTicks, 1, 100_000, errors);
        CheckRange(f, "growth.foodPerCapita", c.Growth.FoodPerCapita, 0, 1_000_000, errors);
        CheckRange(f, "growth.birthFoodCost", c.Growth.BirthFoodCost, 0, 1_000_000, errors);
        CheckRange(f, "growth.birthChancePercent", c.Growth.BirthChancePercent, 0, 100, errors);
        CheckRange(f, "growth.populationCap", c.Growth.PopulationCap, 1, 10_000, errors);
    }

    private static void ValidateNature(NatureFile nature, List<string> errors)
    {
        const string f = NatureFile;
        CheckIds(f, "plant", nature.Plants.Select(p => p.Id), errors);
        CheckIds(f, "animal", nature.Animals.Select(a => a.Id), errors);
        foreach (var p in nature.Plants)
        {
            string n = $"plant '{p.Id}'";
            CheckColor(f, n, p.Color, errors);
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
    }

    private static void CheckRange(string file, string name, int value, int min, int max, List<string> errors)
    {
        if (value < min || value > max)
            errors.Add($"{file}: {name} is {value} but must be between {min} and {max}.");
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
