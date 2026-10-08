using System.Security.Cryptography;
using System.Text.Json;
using Friflo.Engine.ECS.Serialize;
using FutureCity.Sim.Commands;
using FutureCity.Sim.Content;
using FutureCity.Sim.Map;
using FutureCity.Sim.Random;

namespace FutureCity.Sim.Persistence;

/// <summary>Thrown when a save file cannot be loaded.</summary>
public sealed class SaveGameException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Saves and loads the full simulation state as canonical JSON: the same state always produces
/// the same bytes, so the save doubles as the input for <see cref="StateHash"/>.
/// </summary>
public static class SaveGame
{
    /// <summary>Save format version. Bump when the layout changes.</summary>
    public const int FormatVersion = 1;

    /// <summary>Recommended file extension.</summary>
    public const string FileExtension = ".fcsave";

    /// <summary>Writes the simulation to <paramref name="stream"/>.</summary>
    /// <param name="sim">The simulation to save.</param>
    /// <param name="stream">Destination.</param>
    /// <param name="includeCommandLog">Include the command history (needed for replays; excluded from state hashes).</param>
    public static void Write(Simulation sim, Stream stream, bool includeCommandLog = true)
    {
        var world = sim.World;
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("format", FormatVersion);
        writer.WriteString("contentHash", world.Content.Hash);

        writer.WriteStartObject("setup");
        writer.WriteNumber("seed", world.Setup.Seed);
        writer.WriteString("mapSize", world.Setup.MapSize);
        writer.WriteEndObject();

        writer.WriteNumber("tick", world.Tick);
        writer.WriteNumber("nextEntityId", world.NextEntityId);
        writer.WriteNumber("nextCommandSequence", sim.NextCommandSequence);

        var rng = world.Rng.State;
        writer.WriteStartObject("rng");
        writer.WriteNumber("state", rng.State);
        writer.WriteNumber("increment", rng.Increment);
        writer.WriteEndObject();

        writer.WriteStartObject("map");
        writer.WriteNumber("width", world.Map.Width);
        writer.WriteNumber("height", world.Map.Height);
        writer.WriteBase64String("terrain", world.Map.RawTerrain);
        writer.WriteEndObject();

        WriteCommands(writer, "pendingCommands", OrderForSave(sim.PendingCommands), sim.Config.Commands);
        if (includeCommandLog)
            WriteCommands(writer, "commandLog", sim.CommandLog, sim.Config.Commands);

        writer.WritePropertyName("entities");
        writer.WriteRawValue(WriteEntities(world));

        writer.WriteEndObject();
    }

    /// <summary>Serializes the simulation to bytes.</summary>
    public static byte[] ToBytes(Simulation sim, bool includeCommandLog = true)
    {
        using var stream = new MemoryStream();
        Write(sim, stream, includeCommandLog);
        return stream.ToArray();
    }

    /// <summary>
    /// SHA-256 of the full game state (excluding command history), as lower-case hex.
    /// Two simulations with the same hash are in identical states.
    /// </summary>
    public static string StateHash(Simulation sim) =>
        Convert.ToHexString(SHA256.HashData(ToBytes(sim, includeCommandLog: false))).ToLowerInvariant();

    /// <summary>Loads a simulation. The content must be identical to the content the save was made with.</summary>
    /// <exception cref="SaveGameException">If the save is invalid, from another format version, or from different content.</exception>
    public static Simulation Read(Stream stream, ContentDatabase content, SimulationConfig? config = null)
    {
        config ??= SimulationConfig.CreateDefault();
        try
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            int format = root.GetProperty("format").GetInt32();
            if (format != FormatVersion)
                throw new SaveGameException($"Save format {format} is not supported (expected {FormatVersion}).");
            string contentHash = root.GetProperty("contentHash").GetString() ?? "";
            if (contentHash != content.Hash)
                throw new SaveGameException("This save was made with different game content (another game version or mod).");

            var setupJson = root.GetProperty("setup");
            var setup = new GameSetup
            {
                Seed = setupJson.GetProperty("seed").GetUInt64(),
                MapSize = setupJson.GetProperty("mapSize").GetString() ?? throw new SaveGameException("Save has no map size."),
            };

            var rngJson = root.GetProperty("rng");
            var rng = new Pcg32(new Pcg32State(rngJson.GetProperty("state").GetUInt64(), rngJson.GetProperty("increment").GetUInt64()));

            var mapJson = root.GetProperty("map");
            var map = new TileMap(mapJson.GetProperty("width").GetInt32(), mapJson.GetProperty("height").GetInt32(),
                mapJson.GetProperty("terrain").GetBytesFromBase64());

            var world = new World(content, setup, map, rng, root.GetProperty("tick").GetInt64(),
                root.GetProperty("nextEntityId").GetInt32());
            ReadEntities(world, root.GetProperty("entities"));

            var pending = ReadCommands(root.GetProperty("pendingCommands"), config.Commands);
            var log = root.TryGetProperty("commandLog", out var logJson) ? ReadCommands(logJson, config.Commands) : [];

            return new Simulation(world, config, pending, log, root.GetProperty("nextCommandSequence").GetInt64());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new SaveGameException("The save file is damaged or invalid: " + e.Message, e);
        }
    }

    /// <summary>Saves to a file, writing to a temporary file first so a crash never leaves a half-written save.</summary>
    public static void WriteFile(Simulation sim, string path)
    {
        string temp = path + ".tmp";
        using (var stream = File.Create(temp))
            Write(sim, stream);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Loads from a file.</summary>
    public static Simulation ReadFile(string path, ContentDatabase content, SimulationConfig? config = null)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, content, config);
    }

    private static IEnumerable<ScheduledCommand> OrderForSave(IEnumerable<ScheduledCommand> commands) =>
        commands.OrderBy(c => c.Tick).ThenBy(c => c.Sequence);

    private static void WriteCommands(Utf8JsonWriter writer, string name, IEnumerable<ScheduledCommand> commands, CommandRegistry registry)
    {
        writer.WriteStartArray(name);
        foreach (var scheduled in commands)
        {
            writer.WriteStartObject();
            writer.WriteNumber("tick", scheduled.Tick);
            writer.WriteNumber("sequence", scheduled.Sequence);
            writer.WriteString("type", registry.NameOf(scheduled.Command));
            writer.WritePropertyName("data");
            registry.Serialize(scheduled.Command).WriteTo(writer);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static List<ScheduledCommand> ReadCommands(JsonElement array, CommandRegistry registry)
    {
        var list = new List<ScheduledCommand>();
        foreach (var item in array.EnumerateArray())
        {
            var command = registry.Deserialize(item.GetProperty("type").GetString() ?? "", item.GetProperty("data"));
            list.Add(new ScheduledCommand(item.GetProperty("tick").GetInt64(), item.GetProperty("sequence").GetInt64(), command));
        }
        return list;
    }

    private static byte[] WriteEntities(World world)
    {
        using var stream = new MemoryStream();
        new EntitySerializer().WriteEntities(World.InIdOrder(world.Store.Query()), stream);
        return stream.ToArray();
    }

    private static void ReadEntities(World world, JsonElement entities)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(entities.GetRawText()));
        var result = new EntitySerializer().ReadIntoStore(world.Store, stream);
        if (result.error != null)
            throw new SaveGameException("Entities could not be loaded: " + result.error);
    }
}
