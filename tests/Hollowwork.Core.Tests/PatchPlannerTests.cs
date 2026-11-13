using Hollowwork.Core.Patches;
using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class PatchPlannerTests
{
    private static PatchDefinition Patch(
        string id,
        string operationType,
        string from,
        string? to = null,
        string root = "metadata/environmentsettings",
        params string[] extensions) => new(
        id,
        id,
        "test patch",
        "poe1",
        [new PatchTarget(root, extensions.Length == 0 ? [".env"] : extensions)],
        [new PatchOperation(operationType, from, to)]);

    [Fact]
    public void Plans_changes_without_writing()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/a.env", "fog { x }");

        var plan = new PatchPlanner().Plan(archive, [Patch("fog-off", "replace", "fog", "xog")]);

        Assert.Single(plan.Files);
        Assert.Equal(1, plan.TotalChanges);
        Assert.Equal("fog { x }", System.Text.Encoding.UTF8.GetString(archive.Files["metadata/environmentsettings/a.env"]));
    }

    [Fact]
    public void Skips_protected_paths()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/a.env", "fog");
        archive.AddText("metadata/environmentsettings/keep/b.env", "fog");

        var plan = new PatchPlanner().Plan(
            archive,
            [Patch("fog-off", "replace", "fog", "xog")],
            ["metadata/environmentsettings/keep"]);

        Assert.Single(plan.Files);
        Assert.Contains(plan.Skipped, skip => skip.Path.EndsWith("b.env") && skip.Reason == "protected");
    }

    [Fact]
    public void Skips_non_utf8_files()
    {
        using var archive = new MemoryArchive();
        archive.Files["metadata/environmentsettings/bin.env"] = [0x80, 0x81, 0x82];

        var plan = new PatchPlanner().Plan(archive, [Patch("fog-off", "replace", "fog", "xog")]);

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Skipped, skip => skip.Reason == "not utf-8");
    }

    [Fact]
    public void Applies_multiple_patches_to_the_same_file_in_order()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/a.env", "fog sound");

        var plan = new PatchPlanner().Plan(archive,
        [
            Patch("fog-off", "replace", "fog", "xog"),
            Patch("sound-off", "replace", "sound", "xound"),
        ]);

        var file = Assert.Single(plan.Files);
        Assert.Equal("xog xound", file.UpdatedText);
        Assert.Equal(2, plan.TotalChanges);
    }

    [Fact]
    public void Skips_files_that_throw_on_read()
    {
        using var archive = new MemoryArchive();
        const string path = "metadata/environmentsettings/bad.env";
        archive.AddText(path, "fog");
        archive.ThrowOnRead.Add(path);

        var plan = new PatchPlanner().Plan(archive, [Patch("fog-off", "replace", "fog", "xog")]);

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Skipped, skip => skip.Reason.Contains("read failed"));
    }

    [Fact]
    public void Skips_excluded_globs()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/keep/a.env", "fog");
        archive.AddText("metadata/environmentsettings/drop/b.env", "fog");

        var patch = new PatchDefinition(
            "fog-off",
            "Fog off",
            "test",
            "poe1",
            [new PatchTarget("metadata/environmentsettings", [".env"], ["drop/*"])],
            [new PatchOperation("replace", "fog", "xog")]);

        var plan = new PatchPlanner().Plan(archive, [patch]);

        var file = Assert.Single(plan.Files);
        Assert.EndsWith("a.env", file.Path);
        Assert.Contains(plan.Skipped, skip => skip.Reason == "excluded");
    }

    [Fact]
    public void Empty_plan_when_nothing_matches()
    {
        using var archive = new MemoryArchive();
        archive.AddText("metadata/environmentsettings/a.env", "clean");

        var plan = new PatchPlanner().Plan(archive, [Patch("fog-off", "replace", "fog", "xog")]);

        Assert.True(plan.IsEmpty);
        Assert.Equal(0, plan.TotalChanges);
    }
}
