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

    [Fact]
    public void Map_reveal_inserts_the_floor_into_a_vanilla_shader()
    {
        using var archive = new MemoryArchive();
        archive.AddText(
            "shaders/minimap_visibility_pixel.hlsl",
            "if(visibility_fully_revealed > 0.5f)\n\tres_color = float4(1.0f, 0.0f, 0.0f, 1.0f);\nif(visibility_walkable_revealed > 0.5f)\n");

        var patch = LoadShipped("poe1.map-reveal");
        var plan = new PatchPlanner().Plan(archive, [Select(patch, "0.35")]);

        var file = Assert.Single(plan.Files);
        Assert.Contains("res_color = max(res_color, 0.35);", file.UpdatedText);
        Assert.Equal(1, CountOccurrences(file.UpdatedText, "res_color = max(res_color,"));
    }

    [Fact]
    public void Map_reveal_updates_an_already_patched_shader()
    {
        using var archive = new MemoryArchive();
        archive.AddText(
            "shaders/minimap_visibility_pixel.hlsl",
            "\tres_color = float4(1.0f, 0.0f, 0.0f, 1.0f);\n\tres_color = max(res_color, 1.0f);\n");

        var patch = LoadShipped("poe1.map-reveal");
        var plan = new PatchPlanner().Plan(archive, [Select(patch, "0.3")]);

        var file = Assert.Single(plan.Files);
        Assert.Contains("res_color = max(res_color, 0.3);", file.UpdatedText);
        Assert.DoesNotContain("max(res_color, 1.0)", file.UpdatedText);
    }

    [Fact]
    public void Map_reveal_is_idempotent()
    {
        using var archive = new MemoryArchive();
        archive.AddText(
            "shaders/minimap_visibility_pixel.hlsl",
            "\tres_color = float4(1.0f, 0.0f, 0.0f, 1.0f);\n\tres_color = max(res_color, 0.3);\n");

        var patch = LoadShipped("poe1.map-reveal");
        var plan = new PatchPlanner().Plan(archive, [Select(patch, "0.3")]);

        Assert.True(plan.IsEmpty);
    }

    private static PatchSelection Select(PatchDefinition patch, string reveal) =>
        new(patch, new Dictionary<string, string> { ["reveal"] = reveal });
}
