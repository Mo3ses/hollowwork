using Hollowwork.Core.Archives;
using Hollowwork.Core.Protection;
using Hollowwork.Core.Text;
using Hollowwork.Patches;

namespace Hollowwork.Core.Patches;

public sealed record OperationOutcome(string Type, int Changes);

public sealed record PlannedFile(
    string Path,
    string OriginalText,
    string UpdatedText,
    bool HasBom,
    IReadOnlyList<OperationOutcome> Operations);

public sealed record SkippedFile(string Path, string Reason);

public sealed record PatchPlan(IReadOnlyList<PlannedFile> Files, IReadOnlyList<SkippedFile> Skipped)
{
    public int TotalChanges => Files.Sum(file => file.Operations.Sum(operation => operation.Changes));

    public bool IsEmpty => Files.Count == 0;
}

/// <summary>Builds a dry-run plan without writing anything.</summary>
public sealed class PatchPlanner
{
    public PatchPlan Plan(
        IArchiveSession archive,
        IReadOnlyList<PatchDefinition> patches,
        IReadOnlyList<string>? protectedRoots = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(patches);

        var roots = protectedRoots ?? [];
        var working = new Dictionary<string, WorkingFile>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<SkippedFile>();

        foreach (var patch in patches)
        {
            foreach (var target in patch.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var path in archive.ListFiles(target.Root, target.Extensions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (PathProtection.IsProtected(path, roots))
                    {
                        skipped.Add(new SkippedFile(path, "protected"));
                        continue;
                    }

                    if (!working.TryGetValue(path, out var file))
                    {
                        if (!archive.TryRead(path, out var bytes))
                        {
                            skipped.Add(new SkippedFile(path, "not found"));
                            continue;
                        }

                        if (!TextContent.TryDecode(bytes, out var text, out var hasBom))
                        {
                            skipped.Add(new SkippedFile(path, "not utf-8"));
                            continue;
                        }

                        file = new WorkingFile(path, text, hasBom);
                        working.Add(path, file);
                    }

                    foreach (var operation in patch.Operations)
                    {
                        var result = PatchOperations.Apply(file.Current, operation);
                        file.Current = result.Text;
                        file.Outcomes.Add(new OperationOutcome(operation.Type, result.Changes));
                    }
                }
            }
        }

        var planned = working.Values
            .Where(file => file.Current != file.Original)
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Select(file => new PlannedFile(file.Path, file.Original, file.Current, file.HasBom, file.Outcomes))
            .ToList();

        return new PatchPlan(planned, skipped);
    }

    private sealed class WorkingFile(string path, string original, bool hasBom)
    {
        public string Path { get; } = path;

        public string Original { get; } = original;

        public bool HasBom { get; } = hasBom;

        public string Current { get; set; } = original;

        public List<OperationOutcome> Outcomes { get; } = [];
    }
}
