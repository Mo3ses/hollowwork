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
}
