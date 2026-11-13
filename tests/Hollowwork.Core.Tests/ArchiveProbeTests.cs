using Hollowwork.Core.Archives;

namespace Hollowwork.Core.Tests;

public class ArchiveProbeTests
{
    [Fact]
    public void Detects_ggpk_by_magic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwork-{Guid.NewGuid():N}.ggpk");
        try
        {
            File.WriteAllBytes(path, "GGPK"u8.ToArray().Concat(new byte[64]).ToArray());
            Assert.Equal(ArchiveKind.Ggpk, ArchiveProbe.DetectKind(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Detects_bundle_index_by_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hollowwork-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "_.index.bin");
        try
        {
            File.WriteAllBytes(path, [0, 0, 0, 4, 0, 0, 0, 0]);
            Assert.Equal(ArchiveKind.BundleIndex, ArchiveProbe.DetectKind(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unknown_for_random_bytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwork-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);
            Assert.Equal(ArchiveKind.Unknown, ArchiveProbe.DetectKind(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class BundlesProbeTests
{
    [Fact]
    public void Detects_bundles_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hollowwork-bundles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "_.index.bin"), [0, 0, 0, 0]);
            Assert.Equal(ArchiveKind.BundleIndex, ArchiveProbe.DetectKind(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unknown_directory_without_index()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hollowwork-bundles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(ArchiveKind.Unknown, ArchiveProbe.DetectKind(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class ArchiveSessionFactoryTests
{
    [Fact]
    public void Rejects_unknown_archive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hollowwork-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            Assert.Throws<NotSupportedException>(() => ArchiveSessionFactory.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
