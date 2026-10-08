namespace FutureCity.Sim.Tests;

internal static class RepoPaths
{
    /// <summary>The repository root (the folder containing FutureCity.sln).</summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FutureCity.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find FutureCity.sln above the test output folder.");
    }
}
