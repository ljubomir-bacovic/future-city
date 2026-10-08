using System.Diagnostics;
using System.Globalization;
using FutureCity.Content;
using FutureCity.Sim;
using FutureCity.Sim.Ai;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;
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
          --player <name>   Who plays: settle (stand-in bot that builds and farms, default),
                            forage (stand-in bot that only forages) or idle (no orders)
          --timeline        Print the band's state every game minute (single game)
          --debase-at <s>   At this game second, set the coin quality to --quality (inflation experiments)
          --quality <n>     Coin quality in percent for --debase-at (default 50)
          --shock-at <s>    At this game second, destroy half of all grain (price shock experiments)
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

        Console.WriteLine($"content {content.Hash[..12]}  ticks {options.Ticks}  games {options.Games}  player {options.Player}");
        Console.WriteLine();
        if (!options.Timeline) PrintHeader();

        for (int game = 0; game < options.Games; game++)
        {
            var sim = options.LoadPath != null
                ? SaveGame.ReadFile(options.LoadPath, content)
                : Simulation.NewGame(content, new GameSetup
                {
                    Seed = options.Seed + (ulong)game,
                    MapSize = options.MapSize ?? content.Rules.DefaultMapSize,
                });

            Action<Simulation>? bot = options.Player switch
            {
                "settle" => new SettlerBot(Players.Human).Act,
                "forage" => new ForagingBot(Players.Human).Act,
                _ => null,
            };
            var stats = new GameStats();
            if (options.Timeline) PrintHeader();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < options.Ticks; i++)
            {
                bot?.Invoke(sim);
                if (options.DebaseAt is { } debase && sim.World.Tick == SimClock.FromSeconds(debase))
                    sim.Enqueue(new SetCoinQuality(options.Quality) { Player = Players.Human });
                if (options.ShockAt is { } shock && sim.World.Tick == SimClock.FromSeconds(shock))
                    DestroyHalf(sim.World, sim.World.Content.GoodIndex("grain"));
                sim.Step();
                stats.Count(sim.World.Events, sim.World.Tick);
                if (options.Timeline && sim.World.Tick % SimClock.FromSeconds(60) == 0)
                    PrintRow(sim, stats, ticksPerSecond: null);
            }
            timer.Stop();

            double ticksPerSecond = options.Ticks / Math.Max(timer.Elapsed.TotalSeconds, 1e-9);
            if (options.Timeline) Console.WriteLine();
            PrintRow(sim, stats, ticksPerSecond);

            if (options.SavePath != null)
            {
                SaveGame.WriteFile(sim, options.SavePath);
                Console.WriteLine($"saved to {Path.GetFullPath(options.SavePath)}");
            }
        }
        return 0;
    }

    // An experiment, not a game rule: a sudden loss such as a granary fire.
    private static void DestroyHalf(World world, int good)
    {
        foreach (var holder in World.InIdOrder(world.Store.Query<Inventory, Owner>()))
        {
            if (holder.GetComponent<Owner>().Player == Players.Human) holder.GetComponent<Inventory>().Amounts[good] /= 2;
        }
    }

    private static void PrintHeader() =>
        Console.WriteLine($"{"seed",-12} {"map",-7} {"time",6} {"pop",4} {"kids",4} {"food",6} {"born",5} {"starved",7} {"old",4} " +
                          $"{"deer",5} {"huts",4} {"farms",5} {"wood",5} {"stone",5} {"tools",5} {"bread",5} {"techs",5} {"era",-10} " +
                          $"{"reached",7} {"prop",5} {"coin",5} {"guild",5} {"coins",7} {"cpi",4} {"grain$",6} {"bread$",6} " +
                          $"{"tools$",6} {"happy",5} {"ticks/s",9}  state hash");

    private static void PrintRow(Simulation sim, GameStats stats, double? ticksPerSecond)
    {
        var w = sim.World;
        var census = Bands.CensusOf(w, Players.Human);
        int food = Economy.Meals(w, Players.Human);
        int deer = w.Store.Query<Animal>().Count;
        var store = Economy.Holdings(w, Players.Human);
        var built = Buildings.CountCompleted(w, Players.Human);
        int Built(string id) => built[w.Content.BuildingIndex(id)];
        int Stock(string id) => store[w.Content.GoodIndex(id)];
        int techs = w.Content.Techs.Count(t => Civics.Knows(w, Players.Human, t.Index));
        string era = Civics.EraOf(w, Players.Human).Def.Id;
        string reached = stats.EraTick is { } tick ? FormatGameTime(tick) : "-";
        string speed = ticksPerSecond is { } tps ? tps.ToString("F0", CultureInfo.InvariantCulture) : "";
        string When(string institution) =>
            stats.Established.TryGetValue(w.Content.InstitutionIndex(institution), out long t) ? FormatGameTime(t) : "-";
        bool money = Economy.HasMoney(w, Players.Human);
        Economy.TryGetMarket(w, Players.Human, out var market);
        int cpi = market.IsNull ? 0 : Markets.Cpi(w, market);
        string Price(string id) => money ? Economy.Price(w, Players.Human, w.Content.GoodIndex(id)).ToString(CultureInfo.InvariantCulture) : "-";
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{w.Setup.Seed,-12} {w.Setup.MapSize,-7} {FormatGameTime(w.Tick),6} {census.Total,4} {census.Children,4} {food,6} {stats.Births,5} " +
            $"{stats.Starved,7} {stats.OldAge,4} {deer,5} {Built("hut"),4} {Built("farm"),5} {Stock("wood"),5} {Stock("stone"),5} " +
            $"{Stock("tools"),5} {Stock("bread"),5} {techs,5} {era,-10} {reached,7} {When("property"),5} {When("coinage"),5} " +
            $"{When("guilds"),5} {Economy.MoneySupply(w, Players.Human),7} {cpi,4} {Price("grain"),6} {Price("bread"),6} " +
            $"{Price("tools"),6} {Society.AverageHappiness(w, Players.Human),5} {speed,9}  {SaveGame.StateHash(sim)[..16]}"));
    }

    private sealed class GameStats
    {
        public int Births, Starved, OldAge, Kills;
        public long? EraTick;
        public readonly Dictionary<int, long> Established = [];

        public long Tick;

        public void Count(IReadOnlyList<SimEvent> events, long tick)
        {
            Tick = tick;
            foreach (var e in events)
            {
                if (e.Player != Players.Human && e.Kind != SimEventKind.AnimalKilled) continue;
                switch (e.Kind)
                {
                    case SimEventKind.Birth: Births++; break;
                    case SimEventKind.DiedOfStarvation: Starved++; break;
                    case SimEventKind.DiedOfOldAge: OldAge++; break;
                    case SimEventKind.AnimalKilled: Kills++; break;
                    case SimEventKind.EraReached: EraTick = e.Player == Players.Human ? Tick : EraTick; break;
                    case SimEventKind.InstitutionEstablished: Established.TryAdd(e.Detail, Tick); break;
                }
            }
        }
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
        public string Player { get; private init; } = "settle";
        public bool Timeline { get; private init; }
        public int? DebaseAt { get; private init; }
        public int Quality { get; private init; } = 50;
        public int? ShockAt { get; private init; }
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
                    "--player" => options with { Player = PlayerName(Value()) },
                    "--timeline" => options with { Timeline = true },
                    "--debase-at" => options with { DebaseAt = PositiveInt(Value(), "--debase-at") },
                    "--quality" => options with { Quality = Percent(Value(), "--quality") },
                    "--shock-at" => options with { ShockAt = PositiveInt(Value(), "--shock-at") },
                    "--help" or "-h" => options with { Help = true },
                    _ => throw new ArgumentException($"Unknown option '{args[i]}'. Use --help."),
                };
            }
            return options;
        }

        private static string PlayerName(string value) =>
            value is "settle" or "forage" or "idle" ? value : throw new ArgumentException("--player must be 'settle', 'forage' or 'idle'.");

        private static int Percent(string value, string name) =>
            PositiveInt(value, name) is var n and >= 1 and <= 100 ? n : throw new ArgumentException($"{name} must be a percentage from 1 to 100.");

        private static int PositiveInt(string value, string name) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 0
                ? n
                : throw new ArgumentException($"{name} must be a non-negative whole number.");
    }
}
