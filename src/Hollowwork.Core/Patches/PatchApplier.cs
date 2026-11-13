using Hollowwork.Core.Archives;
using Hollowwork.Core.Text;

namespace Hollowwork.Core.Patches;

public sealed record PatchApplyReport(int FilesWritten, int TotalChanges, IReadOnlyList<string> Paths);

/// <summary>Writes a plan to the archive. Call only after a verified backup.</summary>
public sealed class PatchApplier
{
    public PatchApplyReport Apply(IArchiveSession archive, PatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(plan);

        var written = new List<string>(plan.Files.Count);

        foreach (var file in plan.Files)
        {
            archive.Write(file.Path, TextContent.Encode(file.UpdatedText, file.Encoding));
            written.Add(file.Path);
        }

        if (written.Count > 0)
        {
            archive.Flush();
        }

        return new PatchApplyReport(written.Count, plan.TotalChanges, written);
    }
}
