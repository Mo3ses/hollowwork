using System.Text;
using Hollowwork.Core.Patches;
using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class PatchApplierTests
{
    [Fact]
    public void Writes_planned_content_and_flushes()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/a.env", "fog");
        var patch = new PatchDefinition(
            "fog-off",
            "Fog off",
            "test",
            "poe1",
            [new PatchTarget("metadata/environmentsettings", [".env"])],
            [new PatchOperation("replace", "fog", "xog")]);

        var plan = new PatchPlanner().Plan(archive, [patch]);
        var report = new PatchApplier().Apply(archive, plan);

        Assert.Equal(1, report.FilesWritten);
        Assert.Equal(1, report.TotalChanges);
        Assert.True(archive.Flushed);
        Assert.Equal("xog", Encoding.UTF8.GetString(archive.Files["metadata/environmentsettings/a.env"]));
    }

    [Fact]
    public void Preserves_utf8_bom()
    {
        using var archive = new MemoryArchive();
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("fog")).ToArray();
        archive.Files["metadata/environmentsettings/a.env"] = bytes;

        var patch = new PatchDefinition(
            "fog-off",
            "Fog off",
            "test",
            "poe1",
            [new PatchTarget("metadata/environmentsettings", [".env"])],
            [new PatchOperation("replace", "fog", "xog")]);

        var plan = new PatchPlanner().Plan(archive, [patch]);
        new PatchApplier().Apply(archive, plan);

        var written = archive.Files["metadata/environmentsettings/a.env"];
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, written.Take(3));
        Assert.Equal("xog", Encoding.UTF8.GetString(written, 3, written.Length - 3));
    }
}
