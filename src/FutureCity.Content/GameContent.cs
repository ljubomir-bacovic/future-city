using System.Reflection;

namespace FutureCity.Content;

/// <summary>
/// Gives access to the game's JSON content files, which are embedded in this assembly.
/// </summary>
public static class GameContent
{
    private const string Prefix = "content/";

    /// <summary>
    /// Returns every content file as (path relative to Data/, file text), ordered by path.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ReadAll()
    {
        var assembly = typeof(GameContent).Assembly;
        var files = new List<KeyValuePair<string, string>>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing embedded content resource '{name}'.");
            using var reader = new StreamReader(stream);
            files.Add(new(name[Prefix.Length..].Replace('\\', '/'), reader.ReadToEnd()));
        }
        files.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return files;
    }
}
