using System.Reflection;
using System.Text.RegularExpressions;

namespace FutureCity.Sim.Tests;

/// <summary>Guards the non-negotiable architecture and determinism rules from AGENTS.md.</summary>
public class ArchitectureTests
{
    private static readonly Assembly SimAssembly = typeof(Simulation).Assembly;

    [Fact]
    public void Sim_does_not_reference_Godot()
    {
        Assert.DoesNotContain(SimAssembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sim_state_has_no_floating_point_fields()
    {
        var floatTypes = new[] { typeof(float), typeof(double), typeof(decimal), typeof(Half) };
        var offenders = SimAssembly.GetTypes()
            .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => floatTypes.Contains(f.FieldType))
                .Select(f => $"{t.FullName}.{f.Name}"))
            .ToList();
        Assert.Empty(offenders);
    }

    public static TheoryData<string, string> ForbiddenPatterns => new()
    {
        { @"using\s+Godot", "Godot in the simulation" },
        { @"System\.Random|new\s+Random\s*\(", "System.Random (use World.Rng)" },
        { @"DateTime(Offset)?\.(Now|UtcNow|Today)|Stopwatch|Environment\.TickCount", "wall-clock time" },
        { @"Guid\.NewGuid", "random GUIDs" },
        { @"\b(float|double)\b", "floating point (use integer or fixed-point math)" },
        { @"Parallel\.|Task\.Run|new\s+Thread\b", "multithreading" },
        { @"Store\.CreateEntity\(\s*\)", "Friflo-allocated entity ids (use World.CreateEntity)" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenPatterns))]
    public void Sim_source_avoids_forbidden_apis(string pattern, string description)
    {
        string simDir = Path.Combine(RepoPaths.Root, "src", "FutureCity.Sim");
        var regex = new Regex(pattern);
        var hits = Directory.EnumerateFiles(simDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
            .Where(x => !x.line.TrimStart().StartsWith("//") && !x.line.TrimStart().StartsWith("///") && regex.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(simDir, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();
        Assert.True(hits.Count == 0, $"Forbidden {description}:\n" + string.Join("\n", hits));
    }
}
