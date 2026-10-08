using FutureCity.Content;
using FutureCity.Sim.Content;

namespace FutureCity.Sim.Tests;

public class ContentLoaderTests
{
    private const string ValidRules = """
        { "mapSizes": [ { "id": "small", "name": "Small", "width": 32, "height": 32 } ],
          "defaultMapSize": "small", "defaultTerrain": "grass",
          "mapGeneration": { "waterTerrain": "water", "forestTerrain": "grass", "waterPercent": 10, "forestPercent": 0,
                             "featureSize": 8, "startClearRadius": 2, "fertility": { "min": 40, "max": 80, "waterBonus": 10, "waterRadius": 2 } },
          "calendar": { "ticksPerYear": 480, "seasons": [
            { "id": "warm", "name": "Warm", "cropGrowthPercent": 100, "plantRegrowPercent": 100, "sowing": true, "cropsRot": false },
            { "id": "cold", "name": "Cold", "cropGrowthPercent": 0, "plantRegrowPercent": 0, "sowing": false, "cropsRot": true } ] } }
        """;

    private const string ValidTerrain = """
        { "terrains": [
            { "id": "grass", "name": "Grass", "color": "#00ff00", "walkable": true, "buildable": true, "moveCost": 100 },
            { "id": "water", "name": "Water", "color": "#0000ff", "walkable": false, "buildable": false, "moveCost": 100 } ] }
        """;

    private static readonly IReadOnlyDictionary<string, string> Shipped = GameContent.ReadAll().ToDictionary(f => f.Key, f => f.Value);

    // Minimal rules and terrain, plus the other shipped files.
    private static Dictionary<string, string> Files(string rules = ValidRules, string terrain = ValidTerrain) =>
        new()
        {
            ["rules.json"] = rules,
            ["terrain.json"] = terrain,
            ["citizens.json"] = Shipped["citizens.json"],
            ["nature.json"] = Shipped["nature.json"],
            ["goods.json"] = Shipped["goods.json"],
            ["buildings.json"] = Shipped["buildings.json"],
            ["progress.json"] = Shipped["progress.json"],
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
                                 "featureSize": 8, "startClearRadius": 2, "fertility": { "min": 40, "max": 80, "waterBonus": 10, "waterRadius": 2 } },
            "calendar": { "ticksPerYear": 480, "seasons": [
              { "id": "warm", "name": "Warm", "cropGrowthPercent": 100, "plantRegrowPercent": 100, "sowing": true, "cropsRot": false },
              { "id": "cold", "name": "Cold", "cropGrowthPercent": 0, "plantRegrowPercent": 0, "sowing": false, "cropsRot": true } ] } }
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
        files["citizens.json"] = files["citizens.json"].Replace("\"carryCapacity\": 10", "\"carryCapacity\": 0");
        files["nature.json"] = files["nature.json"].Replace("\"maxFood\": 120", "\"maxFood\": -5");
        var errors = LoadFails(files).Errors;
        Assert.Contains(errors, e => e.StartsWith("citizens.json: carryCapacity is 0"));
        Assert.Contains(errors, e => e.StartsWith("nature.json: plant 'berry_bush' maxFood is -5"));
    }

    [Fact]
    public void Calendar_must_split_into_whole_seasons()
    {
        var errors = LoadFails(Files(ValidRules.Replace("\"ticksPerYear\": 480", "\"ticksPerYear\": 481"))).Errors;
        Assert.Contains(errors, e => e.Contains("calendar.ticksPerYear must be divisible"));
    }

    [Fact]
    public void Unknown_goods_are_reported()
    {
        var files = Files();
        files["buildings.json"] = files["buildings.json"].Replace("\"cost\": { \"wood\": 20 }", "\"cost\": { \"gold\": 20 }");
        files["nature.json"] = files["nature.json"].Replace("\"good\": \"stone\"", "\"good\": \"marble\"");
        var errors = LoadFails(files).Errors;
        Assert.Contains(errors, e => e.StartsWith("buildings.json: building 'storehouse' cost refers to unknown good 'gold'"));
        Assert.Contains(errors, e => e.StartsWith("nature.json: deposit 'stone_outcrop' refers to unknown good 'marble'"));
    }

    [Fact]
    public void Broken_conditions_are_reported()
    {
        var files = Files();
        files["progress.json"] = files["progress.json"]
            .Replace("building.storehouse >= 1\"", "building.palace >= 1\"")
            .Replace("\"tech.farming && tech.toolmaking", "\"tech.farming && (tech.toolmaking");
        var errors = LoadFails(files).Errors;
        Assert.Contains(errors, e => e.StartsWith("progress.json: tech 'toolmaking' preconditions: unknown fact 'building.palace'"));
        Assert.Contains(errors, e => e.StartsWith("progress.json: era 'dark_ages' preconditions: missing ')'"));
    }

    [Fact]
    public void Unknown_work_kinds_are_reported()
    {
        var files = Files();
        files["progress.json"] = files["progress.json"].Replace("\"forage\": 100", "\"dance\": 100");
        Assert.Contains(LoadFails(files).Errors, e => e.Contains("work 'dance' must be one of"));
    }

    [Fact]
    public void Shipped_content_resolves_references()
    {
        var content = ContentLoader.Load(GameContent.ReadAll());
        var farm = content.Buildings[content.BuildingIndex("farm")];
        Assert.Equal(content.GoodIndex("grain"), farm.FieldGood);
        Assert.False(farm.Requires.IsAlways);
        var mill = content.Buildings[content.BuildingIndex("mill")];
        Assert.True(mill.Inputs[content.GoodIndex("grain")] > 0);
        Assert.True(mill.Outputs[content.GoodIndex("flour")] > 0);
        Assert.Equal(content.GoodIndex("wood"), content.TerrainSource(content.TerrainIndex("forest"))?.Good);
        Assert.Equal(content.TerrainIndex("grass"), content.DepletedTerrain(content.TerrainIndex("forest")));
        Assert.Equal(content.GoodIndex("stone"), content.DepositSource(content.DepositIndex("stone_outcrop")).Good);
        Assert.True(content.Techs[content.TechIndex("farming")].Activity[(int)WorkKind.Forage] > 0);
        Assert.Equal("Dark Ages", content.Eras[1].Def.Name);
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
