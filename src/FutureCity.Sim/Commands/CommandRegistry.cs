using System.Text.Json;

namespace FutureCity.Sim.Commands;

/// <summary>
/// Maps command types to stable names so commands can be saved, loaded and replayed.
/// Every command type must be registered; names must never change once saves exist.
/// </summary>
public sealed class CommandRegistry
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _byType = new();

    /// <summary>Creates a registry containing all built-in game commands.</summary>
    public static CommandRegistry CreateDefault()
    {
        var registry = new CommandRegistry();
        // Names are part of the save format: never rename them.
        return registry
            .Register<MoveUnits>("moveUnits")
            .Register<Gather>("gather")
            .Register<Hunt>("hunt")
            .Register<ReturnToCamp>("returnToCamp")
            .Register<GatherTile>("gatherTile")
            .Register<Build>("build")
            .Register<AssignWork>("assignWork")
            .Register<PlaceBuilding>("placeBuilding")
            .Register<CancelBuilding>("cancelBuilding")
            .Register<SetResearchFocus>("setResearchFocus")
            .Register<EstablishInstitution>("establishInstitution")
            .Register<SetTaxes>("setTaxes")
            .Register<SetCoinQuality>("setCoinQuality")
            .Register<Recruit>("recruit")
            .Register<Disband>("disband")
            .Register<SetRallyPoint>("setRallyPoint")
            .Register<Attack>("attack")
            .Register<AttackMove>("attackMove")
            .Register<Loot>("loot")
            .Register<DeclareWar>("declareWar");
    }

    /// <summary>Registers a command type under a stable name.</summary>
    public CommandRegistry Register<T>(string name) where T : Command
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Command name is required.", nameof(name));
        if (_byName.ContainsKey(name)) throw new InvalidOperationException($"Command name '{name}' is already registered.");
        if (_byType.ContainsKey(typeof(T))) throw new InvalidOperationException($"Command type {typeof(T).Name} is already registered.");
        _byName[name] = typeof(T);
        _byType[typeof(T)] = name;
        return this;
    }

    /// <summary>Whether the command's type is registered.</summary>
    public bool IsRegistered(Command command) => _byType.ContainsKey(command.GetType());

    internal string NameOf(Command command) =>
        _byType.TryGetValue(command.GetType(), out var name)
            ? name
            : throw new InvalidOperationException($"Command type {command.GetType().Name} is not registered.");

    internal JsonElement Serialize(Command command) =>
        JsonSerializer.SerializeToElement(command, command.GetType(), JsonOptions);

    internal Command Deserialize(string name, JsonElement data)
    {
        if (!_byName.TryGetValue(name, out var type))
            throw new InvalidOperationException($"Unknown command '{name}'.");
        return (Command)(data.Deserialize(type, JsonOptions)
            ?? throw new InvalidOperationException($"Command '{name}' has no data."));
    }
}
