using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class PatchLoaderTests
{
    private const string Valid = """
    {
      "id": "poe1.test",
      "name": "Test",
      "description": "Test patch.",
      "game": "poe1",
      "targets": [ { "root": "metadata/environmentsettings", "extensions": [".env"] } ],
      "operations": [ { "type": "replace", "from": "fog", "to": "xog" } ]
    }
    """;

    [Fact]
    public void Loads_valid_patch()
    {
        var patch = PatchLoader.Load(Valid);

        Assert.Equal("poe1.test", patch.Id);
        Assert.Single(patch.Targets);
        Assert.Equal("replace", patch.Operations[0].Type);
    }

    [Fact]
    public void Rejects_unknown_operation()
    {
        var json = Valid.Replace("\"replace\"", "\"explode\"");

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Rejects_unknown_game()
    {
        var json = Valid.Replace("\"poe1\"", "\"poe3\"");

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Rejects_duplicate_ids()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hollowwork-patches-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.json"), Valid);
            File.WriteAllText(Path.Combine(dir, "b.json"), Valid);

            Assert.Throws<PatchValidationException>(() => PatchLoader.LoadDirectory(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Loads_explicit_file_allowlist()
    {
        var json = Valid.Replace(
            "\"extensions\": [\".env\"]",
            "\"extensions\": [\".env\"], \"files\": [\"metadata/environmentsettings/a.env\"]");

        var patch = PatchLoader.Load(json);

        var files = Assert.Single(patch.Targets).Files;
        Assert.NotNull(files);
        Assert.Equal(["metadata/environmentsettings/a.env"], files);
    }

    [Fact]
    public void Rejects_blank_file_entry()
    {
        var json = Valid.Replace(
            "\"extensions\": [\".env\"]",
            "\"extensions\": [\".env\"], \"files\": [\"  \"]");

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Rejects_empty_file_list()
    {
        var json = Valid.Replace(
            "\"extensions\": [\".env\"]",
            "\"extensions\": [\".env\"], \"files\": []");

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Rejects_file_entry_outside_the_root()
    {
        var json = Valid.Replace(
            "\"extensions\": [\".env\"]",
            "\"extensions\": [\".env\"], \"files\": [\"../secrets.env\"]");

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Shipped_patches_load_and_validate()
    {
        var directory = FindRepositoryPatches();

        var patches = PatchLoader.LoadDirectory(directory);

        Assert.NotEmpty(patches);
        Assert.Contains(patches, patch => patch.Id == "poe1.minimap-colors");
        Assert.Contains(patches, patch => patch.Id == "poe1.map-reveal");
    }

    private static string FindRepositoryPatches()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hollowwork.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "patches");
    }

    [Fact]
    public void Loads_target_scoped_operations()
    {
        const string json = """
        {
          "id": "poe1.perf",
          "name": "Perf",
          "description": "test",
          "game": "poe1",
          "targets": [
            {
              "root": "shaders",
              "extensions": [".hlsl"],
              "files": ["shaders/a.hlsl"],
              "operations": [ { "type": "replace", "from": "a", "to": "b" } ]
            }
          ]
        }
        """;

        var patch = PatchLoader.Load(json);

        Assert.Empty(patch.Operations ?? []);
        Assert.Single(Assert.Single(patch.Targets).Operations!);
    }

    [Fact]
    public void Rejects_empty_target_operations()
    {
        const string json = """
        {
          "id": "poe1.perf",
          "name": "Perf",
          "description": "test",
          "game": "poe1",
          "targets": [
            { "root": "shaders", "extensions": [".hlsl"], "operations": [] }
          ]
        }
        """;

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }

    [Fact]
    public void Rejects_patch_without_any_operation()
    {
        const string json = """
        {
          "id": "poe1.perf",
          "name": "Perf",
          "description": "test",
          "game": "poe1",
          "targets": [ { "root": "shaders", "extensions": [".hlsl"] } ]
        }
        """;

        Assert.Throws<PatchValidationException>(() => PatchLoader.Load(json));
    }
}
