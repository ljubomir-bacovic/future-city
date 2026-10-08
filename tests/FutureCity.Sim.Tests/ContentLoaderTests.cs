using FutureCity.Content;
using FutureCity.Sim.Content;

namespace FutureCity.Sim.Tests;

public class ContentLoaderTests
{
    private const string ValidRules = """
        { "mapSizes": [ { "id": "small", "name": "Small", "width": 32, "height": 32 } ],
          "defaultMapSize": "small", "defaultTerrain": "grass",
          "mapGeneration": { "waterTerrain": "water", "forestTerrain": "grass", "waterPercent": 10, "forestPercent": 0,
                             "featureSize": 8, "startClearRadius": 2 } }
        """;

    private const string ValidTerrain = """
        { "terrains": [
            { "id": "grass", "name": "Grass", "color": "#00ff00", "walkable": true, "buildable": true, "moveCost": 100 },
            { "id": "water", "name": "Water", "color": "#0000ff", "walkable": false, "buildable": false, "moveCost": 100 } ] }
        """;

    private static readonly IReadOnlyDictionary<string, string> Shipped = GameContent.ReadAll().ToDictionary(f => f.Key, f => f.Value);

    // Minimal rules and terrain, plus the shipped citizen and nature files.
    private static Dictionary<string, string> Files(string rules = ValidRules, string terrain = ValidTerrain) =>
        new()
        {
            ["rules.json"] = rules,
            ["terrain.json"] = terrain,
            ["citizens.json"] = Shipped["citizens.json"],
            ["nature.json"] = Shipped["nature.json"],
        };

    private static ContentException LoadFails(Dictionary<string, string> files) =>
        Assert.Throws<ContentException>(() => ContentLoader.Load(files));

    [Fact]
    public void Shipped_content_loads()
    {
        var content = ContentLoader.Load(GameContent.ReadAll());
        Assert.Contains(content.Terrains, t => t.Id == content.Rules.DefaultTerrain);
        Assert.NotNull(content.MapSize(content.Rules.DefaultMapSize));
        Assert.Matches("^[0-9a-f]{64}$", content.Hash);
    }

    [Fact]
    public void Shipped_content_loads_from_directory_with_same_hash()
    {
        string dir = Path.Combine(RepoPaths.Root, "src", "FutureCity.Content", "Data");
        Assert.Equal(ContentLoader.Load(GameContent.ReadAll()).Hash, ContentLoader.LoadDirectory(dir).Hash);
    }

    [Fact]
    public void Minimal_valid_content_loads()
    {
        var content = ContentLoader.Load(Files());
        Assert.Equal(0, content.TerrainIndex("grass"));
        Assert.Equal(32, content.MapSize("small").Width);
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        var files = Files();
        files.Remove("terrain.json");
        Assert.Contains(LoadFails(files).Errors, e => e.Contains("terrain.json: file is missing"));
    }

    [Fact]
    public void Unknown_property_is_rejected()
    {
        var error = LoadFails(Files(terrain: ValidTerrain.Replace("\"walkable\"", "\"speed\": 2, \"walkable\"")));
        Assert.Contains(error.Errors, e => e.StartsWith("terrain.json:") && e.Contains("speed"));
    }

    [Fact]
    public void Missing_required_property_is_rejected()
    {
        var error = LoadFails(Files(terrain: ValidTerrain.Replace(", \"buildable\": true", "")));
        Assert.Contains(error.Errors, e => e.StartsWith("terrain.json:") && e.Contains("buildable"));
    }

    [Fact]
    public void Every_validation_error_is_reported_at_once()
    {
        const string terrain = """
            { "terrains": [
                { "id": "grass", "name": "Grass", "color": "green", "walkable": true, "buildable": true, "moveCost": 100 },
                { "id": "grass", "name": "Grass 2", "color": "#00ff00", "walkable": true, "buildable": true, "moveCost": 100 },
                { "id": "Bad Id", "name": "Bad", "color": "#00ff00", "walkable": true, "buildable": true, "moveCost": 0 } ] }
            """;
        const string rules = """
            { "mapSizes": [ { "id": "tiny", "name": "Tiny", "width": 4, "height": 32 } ],
              "defaultMapSize": "huge", "defaultTerrain": "lava",
              "mapGeneration": { "waterTerrain": "sea", "forestTerrain": "grass", "waterPercent": 99, "forestPercent": 0,
                                 "featureSize": 8, "startClearRadius": 2 } }
            """;
        var errors = LoadFails(Files(rules, terrain)).Errors;
        Assert.Contains(errors, e => e.Contains("color 'green'"));
        Assert.Contains(errors, e => e.Contains("duplicate terrain id 'grass'"));
        Assert.Contains(errors, e => e.Contains("'Bad Id'"));
        Assert.Contains(errors, e => e.Contains("map size 'tiny' must be between"));
        Assert.Contains(errors, e => e.Contains("defaultMapSize 'huge'"));
        Assert.Contains(errors, e => e.Contains("defaultTerrain 'lava'"));
        Assert.Contains(errors, e => e.Contains("moveCost is 0"));
        Assert.Contains(errors, e => e.Contains("waterTerrain 'sea'"));
        Assert.Contains(errors, e => e.Contains("waterPercent is 99"));
    }

    [Fact]
    public void Citizen_and_nature_numbers_are_range_checked()
    {
        var files = Files();
        files["citizens.json"] = files["citizens.json"].Replace("\"ticksPerYear\": 120", "\"ticksPerYear\": 0");
        files["nature.json"] = files["nature.json"].Replace("\"maxFood\": 120", "\"maxFood\": -5");
        var errors = LoadFails(files).Errors;
        Assert.Contains(errors, e => e.StartsWith("citizens.json: ticksPerYear is 0"));
        Assert.Contains(errors, e => e.StartsWith("nature.json: plant 'berry_bush' maxFood is -5"));
    }

    [Fact]
    public void Shipped_content_defines_food_sources()
    {
        var content = ContentLoader.Load(GameContent.ReadAll());
        Assert.Equal(0, content.PlantIndex("berry_bush"));
        Assert.Equal(0, content.AnimalIndex("deer"));
        Assert.True(content.Terrains[content.TerrainIndex("forest")].MoveCost > 100, "forest should slow walkers");
    }

    [Fact]
    public void Hash_ignores_line_endings_but_not_content()
    {
        var a = ContentLoader.Load(Files());
        var b = ContentLoader.Load(Files(ValidRules.ReplaceLineEndings("\r\n")));
        var c = ContentLoader.Load(Files(ValidRules.Replace("\"width\": 32", "\"width\": 33")));
        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(a.Hash, c.Hash);
    }
}
