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

        if (rules != null && terrainFile != null)
            Validate(rules, terrainFile.Terrains, errors);

        if (errors.Count > 0)
            throw new ContentException(errors);

        return new ContentDatabase(rules!, terrainFile!.Terrains, ComputeHash(byPath));
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
            if (!ColorPattern().IsMatch(t.Color))
                errors.Add($"{TerrainFile}: terrain '{t.Id}' color '{t.Color}' must be in #rrggbb form.");
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
