using System.Diagnostics;
using System.Globalization;
using FutureCity.Content;
using FutureCity.Sim;
using FutureCity.Sim.Content;
using FutureCity.Sim.Persistence;

namespace FutureCity.Headless;

/// <summary>
/// Runs games without graphics, for balancing and regression checks.
/// </summary>
public static class Program
{
    private const string Usage = """
        Future City headless runner

        Usage: FutureCity.Headless [options]

          --seed <n>        Random seed (default 42)
          --map <id>        Map size id from rules.json (default: rules default)
          --ticks <n>       Ticks to simulate (default 600 = 1 minute at 1x)
          --games <n>       Run n games with seeds seed, seed+1, ... (default 1)
          --load <file>     Continue from a save instead of starting a new game
          --save <file>     Write a save after the run (single game only)
          --content <dir>   Load content JSON from a directory instead of the built-in data
          --help            Show this help
        """;

    public static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (options.Help)
            {
                Console.WriteLine(Usage);
                return 0;
            }
            return Run(options);
        }
        catch (Exception e) when (e is ArgumentException or ContentException or SaveGameException or IOException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 1;
        }
    }

    private static int Run(Options options)
    {
        var content = options.ContentDir != null
            ? ContentLoader.LoadDirectory(options.ContentDir)
            : ContentLoader.Load(GameContent.ReadAll());

        if (options.Games > 1 && (options.SavePath != null || options.LoadPath != null))
            throw new ArgumentException("--save and --load work with a single game only.");

        Console.WriteLine($"content {content.Hash[..12]}  ticks {options.Ticks}  games {options.Games}");
        Console.WriteLine();
        Console.WriteLine($"{"seed",-12} {"map",-8} {"tick",8} {"game time",10} {"entities",9} {"ticks/s",10}  state hash");

        for (int game = 0; game < options.Games; game++)
        {
            var sim = options.LoadPath != null
                ? SaveGame.ReadFile(options.LoadPath, content)
                : Simulation.NewGame(content, new GameSetup
                {
                    Seed = options.Seed + (ulong)game,
                    MapSize = options.MapSize ?? content.Rules.DefaultMapSize,
                });

            var timer = Stopwatch.StartNew();
            sim.Step(options.Ticks);
            timer.Stop();

            double ticksPerSecond = options.Ticks / Math.Max(timer.Elapsed.TotalSeconds, 1e-9);
            var w = sim.World;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{w.Setup.Seed,-12} {w.Setup.MapSize,-8} {w.Tick,8} {FormatGameTime(w.Tick),10} {w.Store.Count,9} {ticksPerSecond,10:F0}  {SaveGame.StateHash(sim)[..16]}"));

            if (options.SavePath != null)
            {
                SaveGame.WriteFile(sim, options.SavePath);
                Console.WriteLine($"saved to {Path.GetFullPath(options.SavePath)}");
            }
        }
        return 0;
    }

    private static string FormatGameTime(long ticks)
    {
        long seconds = SimClock.ToSeconds(ticks);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private sealed record Options
    {
        public ulong Seed { get; private init; } = 42;
        public string? MapSize { get; private init; }
        public int Ticks { get; private init; } = 600;
        public int Games { get; private init; } = 1;
        public string? LoadPath { get; private init; }
        public string? SavePath { get; private init; }
        public string? ContentDir { get; private init; }
        public bool Help { get; private init; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
                options = args[i] switch
                {
                    "--seed" => options with { Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture) },
                    "--map" => options with { MapSize = Value() },
                    "--ticks" => options with { Ticks = PositiveInt(Value(), "--ticks") },
                    "--games" => options with { Games = PositiveInt(Value(), "--games") },
                    "--load" => options with { LoadPath = Value() },
                    "--save" => options with { SavePath = Value() },
                    "--content" => options with { ContentDir = Value() },
                    "--help" or "-h" => options with { Help = true },
                    _ => throw new ArgumentException($"Unknown option '{args[i]}'. Use --help."),
                };
            }
            return options;
        }

        private static int PositiveInt(string value, string name) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 0
                ? n
                : throw new ArgumentException($"{name} must be a non-negative whole number.");
    }
}
