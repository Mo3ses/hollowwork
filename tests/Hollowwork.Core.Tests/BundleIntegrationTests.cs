using Hollowwork.Core.Archives;

namespace Hollowwork.Core.Tests;

/// <summary>
/// Runs only when HOLLOWWORK_TEST_BUNDLES and HOLLOWWORK_TEST_OODLE are set.
/// CI has neither, so the test returns early.
/// </summary>
public class BundleIntegrationTests
{
    [Fact]
    public void Reads_a_real_bundles2_archive()
    {
        var bundles = Environment.GetEnvironmentVariable("HOLLOWWORK_TEST_BUNDLES");
        var oodle = Environment.GetEnvironmentVariable("HOLLOWWORK_TEST_OODLE");
        if (string.IsNullOrWhiteSpace(bundles) || string.IsNullOrWhiteSpace(oodle))
        {
            return;
        }

        using var session = new BundleArchiveSession(bundles, oodle);
        var files = session.ListFiles("metadata/environmentsettings", [".env"]);

        Assert.NotEmpty(files);
        Assert.True(session.TryRead(files[0], out var content));
        Assert.NotEmpty(content);
    }
}
