using Hollowwork.Core.Patches;
using Hollowwork.Patches;

namespace Hollowwork.Core.Tests;

public class PatchOperationsTests
{
    [Fact]
    public void Replace_counts_and_replaces_all_occurrences()
    {
        var result = PatchOperations.Apply("fog and fog and fog", new PatchOperation("replace", "fog", "xog"));

        Assert.Equal("xog and xog and xog", result.Text);
        Assert.Equal(3, result.Changes);
    }

    [Fact]
    public void Replace_is_case_sensitive_and_literal()
    {
        var result = PatchOperations.Apply("Fog fog f.o.g", new PatchOperation("replace", "fog", "xog"));

        Assert.Equal("Fog xog f.o.g", result.Text);
        Assert.Equal(1, result.Changes);
    }

    [Fact]
    public void SetBool_rewrites_true_to_false()
    {
        var json = "{ \"shadows_enabled\": true }";
        var result = PatchOperations.Apply(json, new PatchOperation("setBool", "shadows_enabled", "false"));

        Assert.Equal("{ \"shadows_enabled\": false }", result.Text);
        Assert.Equal(1, result.Changes);
    }

    [Fact]
    public void SetBool_no_change_when_value_already_matches()
    {
        var json = "{ \"shadows_enabled\": false }";
        var result = PatchOperations.Apply(json, new PatchOperation("setBool", "shadows_enabled", "false"));

        Assert.Equal(0, result.Changes);
    }

    [Fact]
    public void StripBlock_empties_nested_block_and_keeps_braces()
    {
        var text = "fog { a { b } c } tail";
        var result = PatchOperations.Apply(text, new PatchOperation("stripBlock", "fog"));

        Assert.Equal("fog {} tail", result.Text);
        Assert.Equal(1, result.Changes);
    }

    [Fact]
    public void StripBlock_ignores_braces_inside_strings()
    {
        var text = "fog { name \"a}b\" } tail";
        var result = PatchOperations.Apply(text, new PatchOperation("stripBlock", "fog"));

        Assert.Equal("fog {} tail", result.Text);
    }

    [Fact]
    public void StripBlock_ignores_partial_identifier_matches()
    {
        var text = "xfog { keep } fog { drop }";
        var result = PatchOperations.Apply(text, new PatchOperation("stripBlock", "fog"));

        Assert.Equal("xfog { keep } fog {}", result.Text);
    }

    [Fact]
    public void RemoveLines_drops_matching_lines()
    {
        var text = "keep\nDROP me\nkeep\n";
        var result = PatchOperations.Apply(text, new PatchOperation("removeLines", "DROP"));

        Assert.Equal("keep\nkeep\n", result.Text);
        Assert.Equal(1, result.Changes);
    }

    [Fact]
    public void ReplaceExtension_swaps_references()
    {
        var result = PatchOperations.Apply("a.pet b.pet c.trl", new PatchOperation("replaceExtension", ".pet", ".trl"));

        Assert.Equal("a.trl b.trl c.trl", result.Text);
        Assert.Equal(2, result.Changes);
    }

    [Fact]
    public void Replace_requires_from()
    {
        Assert.Throws<PatchValidationException>(() =>
            PatchOperations.Apply("x", new PatchOperation("replace", null, "y")));
    }
}
