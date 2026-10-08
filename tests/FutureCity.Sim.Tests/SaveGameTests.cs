using System.Text;
using FutureCity.Sim.Content;
using FutureCity.Sim.Persistence;

namespace FutureCity.Sim.Tests;

public class SaveGameTests
{
    private static Simulation Load(byte[] bytes, ContentDatabase? content = null) =>
        SaveGame.Read(new MemoryStream(bytes), content ?? TestSupport.Content, TestSupport.Config());

    [Fact]
    public void Round_trip_produces_identical_bytes()
    {
        var sim = TestSupport.RunScript(42, 300);
        sim.Enqueue(new SpawnWanderers(2, 1, 1) { Player = 3 }, 400);
        var bytes = SaveGame.ToBytes(sim);

        var loaded = Load(bytes);

        Assert.Equal(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(SaveGame.ToBytes(loaded)));
    }

    [Fact]
    public void Round_trip_restores_world_fields()
    {
        var sim = TestSupport.RunScript(42, 300);
        sim.World.Map.SetTerrain(3, 4, TestSupport.Content.TerrainIndex("water"));
        var loaded = Load(SaveGame.ToBytes(sim));

        Assert.Equal(sim.World.Tick, loaded.World.Tick);
        Assert.Equal(sim.World.NextEntityId, loaded.World.NextEntityId);
        Assert.Equal(sim.World.Rng.State, loaded.World.Rng.State);
        Assert.Equal(sim.World.Setup, loaded.World.Setup);
        Assert.Equal(sim.World.Store.Count, loaded.World.Store.Count);
        Assert.Equal(sim.World.Map.RawTerrain.ToArray(), loaded.World.Map.RawTerrain.ToArray());
        Assert.Equal(sim.CommandLog.Count, loaded.CommandLog.Count);
        Assert.Equal(sim.CommandLog.Select(c => c.Command), loaded.CommandLog.Select(c => c.Command));

        foreach (var entity in World.InIdOrder(sim.World.Store.Query<Wanderer>()))
        {
            var copy = loaded.World.Store.GetEntityById(entity.Id);
            Assert.Equal(entity.GetComponent<Wanderer>(), copy.GetComponent<Wanderer>());
            Assert.Equal(entity.GetComponent<Age>(), copy.GetComponent<Age>());
        }
    }

    [Fact]
    public void Loaded_world_keeps_allocating_fresh_entity_ids()
    {
        var sim = TestSupport.RunScript(42, 300);
        var loaded = Load(SaveGame.ToBytes(sim));
        int next = sim.World.NextEntityId;
        Assert.Equal(next, loaded.World.CreateEntity().Id);
    }

    [Fact]
    public void State_hash_ignores_command_history()
    {
        var sim = TestSupport.RunScript(42, 300);
        var loaded = Load(SaveGame.ToBytes(sim, includeCommandLog: false));
        Assert.Empty(loaded.CommandLog);
        Assert.Equal(SaveGame.StateHash(sim), SaveGame.StateHash(loaded));
    }

    [Fact]
    public void Save_from_different_content_is_rejected()
    {
        var bytes = SaveGame.ToBytes(TestSupport.RunScript(42, 10));
        var otherContent = ContentLoader.Load(FutureCity.Content.GameContent.ReadAll()
            .Select(f => f.Key == "terrain.json" ? new KeyValuePair<string, string>(f.Key, f.Value.Replace("Grassland", "Meadow")) : f));
        var error = Assert.Throws<SaveGameException>(() => Load(bytes, otherContent));
        Assert.Contains("different game content", error.Message);
    }

    [Fact]
    public void Unsupported_format_is_rejected()
    {
        var text = Encoding.UTF8.GetString(SaveGame.ToBytes(TestSupport.RunScript(42, 10)));
        var bytes = Encoding.UTF8.GetBytes(text.Replace($"\"format\": {SaveGame.FormatVersion}", "\"format\": 999"));
        Assert.Throws<SaveGameException>(() => Load(bytes));
    }

    [Fact]
    public void Damaged_save_is_rejected()
    {
        var bytes = SaveGame.ToBytes(TestSupport.RunScript(42, 10));
        Assert.Throws<SaveGameException>(() => Load(bytes[..(bytes.Length / 2)]));
    }

    [Fact]
    public void Save_files_are_written_atomically()
    {
        string dir = Directory.CreateTempSubdirectory("fc-save-").FullName;
        try
        {
            string path = Path.Combine(dir, "test" + SaveGame.FileExtension);
            var sim = TestSupport.RunScript(42, 100);
            SaveGame.WriteFile(sim, path);
            SaveGame.WriteFile(sim, path); // overwrite
            Assert.False(File.Exists(path + ".tmp"));
            var loaded = SaveGame.ReadFile(path, TestSupport.Content, TestSupport.Config());
            Assert.Equal(SaveGame.StateHash(sim), SaveGame.StateHash(loaded));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
