using Hollowwork.Core.Patches;
using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

/// <summary>Runs the patches shipped in <c>patches/</c> against synthetic archives.</summary>
public class ShippedPatchTests
{
    [Theory]
    [InlineData("CreateCameraZoomNode(5000.0, 5000.0, 2.1);")]
    [InlineData("CreateCameraZoomNode( 5000.0, 5000.0, 2.1 );")]
    [InlineData("CreateCameraZoomNode(5000.0,5000.0,2.1);")]
    public void Camera_zoom_rewrites_the_player_node_regardless_of_spacing(string node)
    {
        using var archive = new MemoryArchive();
        archive.AddText(
            "metadata/characters/character.ot",
            $"Positioned\n{{\n\tteam = 1\n\ton_initial_position_set = {{{node}}} \n}}\n");

        var patch = LoadShipped("poe1.camera-zoom");
        var selection = new PatchSelection(patch, new Dictionary<string, string> { ["zoom"] = "1.7" });

        var plan = new PatchPlanner().Plan(archive, [selection]);

        var file = Assert.Single(plan.Files);
        Assert.Contains("CreateCameraZoomNode(5000.0, 5000.0, 1.7)", file.UpdatedText);
        Assert.DoesNotContain("2.1", file.UpdatedText);
    }

    [Fact]
    public void Camera_zoom_does_not_inject_a_second_node()
    {
        using var archive = new MemoryArchive();
        archive.AddText(
            "metadata/characters/character.ot",
            "Positioned\n{\n\tteam = 1\n\ton_initial_position_set = {CreateCameraZoomNode(5000.0, 5000.0, 2.1);} \n}\n");

        var patch = LoadShipped("poe1.camera-zoom");
        var selection = new PatchSelection(patch, new Dictionary<string, string> { ["zoom"] = "1.7" });

        var plan = new PatchPlanner().Plan(archive, [selection]);

        var file = Assert.Single(plan.Files);
        Assert.Equal(1, CountOccurrences(file.UpdatedText, "on_initial_position_set"));
        Assert.Equal(1, CountOccurrences(file.UpdatedText, "CreateCameraZoomNode"));
    }

    private static PatchDefinition LoadShipped(string id)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hollowwork.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var patches = PatchLoader.LoadDirectory(Path.Combine(directory.FullName, "patches"));
        return Assert.Single(patches, patch => patch.Id == id);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
