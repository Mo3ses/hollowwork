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
    TextEncodingKind Encoding,
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
        CancellationToken cancellationToken = default) =>
        Plan(
            archive,
            patches.Select(PatchSelection.WithoutValues).ToList(),
            protectedRoots,
            cancellationToken);

    public PatchPlan Plan(
        IArchiveSession archive,
        IReadOnlyList<PatchSelection> selections,
        IReadOnlyList<string>? protectedRoots = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(selections);

        var roots = protectedRoots ?? [];
        var working = new Dictionary<string, WorkingFile>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<SkippedFile>();

        foreach (var selection in selections)
        {
            var patch = selection.Definition;
            var operations = patch.Operations
                .Select(operation => Substitute(operation, selection.Values))
                .ToList();

            foreach (var target in patch.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var root = target.Root.Replace('\\', '/').Trim('/');
                foreach (var path in archive.ListFiles(root, target.Extensions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (PathProtection.IsProtected(path, roots))
                    {
                        skipped.Add(new SkippedFile(path, "protected"));
                        continue;
                    }

                    var relative = root.Length == 0
                        ? path
                        : path[(root.Length + 1)..];
                    if (GlobMatcher.IsMatch(relative, target.Exclude))
                    {
                        skipped.Add(new SkippedFile(path, "excluded"));
                        continue;
                    }

                    if (!working.TryGetValue(path, out var file))
                    {
                        byte[] bytes;
                        try
                        {
                            if (!archive.TryRead(path, out bytes))
                            {
                                skipped.Add(new SkippedFile(path, "not found"));
                                continue;
                            }
                        }
                        catch (Exception exception)
                        {
                            skipped.Add(new SkippedFile(path, $"read failed: {exception.GetType().Name}"));
                            continue;
                        }

                        if (!TextContent.TryDecode(bytes, out var text, out var encoding))
                        {
                            skipped.Add(new SkippedFile(path, "not utf-8"));
                            continue;
                        }

                        file = new WorkingFile(path, text, encoding);
                        working.Add(path, file);
                    }

                    foreach (var operation in operations)
                    {
                        if (!string.IsNullOrEmpty(operation.Unless)
                            && file.Current.Contains(operation.Unless, StringComparison.Ordinal))
                        {
                            file.Outcomes.Add(new OperationOutcome(operation.Type, 0));
                            continue;
                        }

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
            .Select(file => new PlannedFile(file.Path, file.Original, file.Current, file.Encoding, file.Outcomes))
            .ToList();

        return new PatchPlan(planned, skipped);
    }

    private static PatchOperation Substitute(PatchOperation operation, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return operation;
        }

        return operation with
        {
            From = ReplacePlaceholders(operation.From, values),
            To = ReplacePlaceholders(operation.To, values),
            Unless = ReplacePlaceholders(operation.Unless, values),
        };
    }

    private static string? ReplacePlaceholders(string? text, IReadOnlyDictionary<string, string> values)
    {
        if (text is null)
        {
            return null;
        }

        foreach (var (key, value) in values)
        {
            text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    private sealed class WorkingFile(string path, string original, TextEncodingKind encoding)
    {
        public string Path { get; } = path;

        public string Original { get; } = original;

        public TextEncodingKind Encoding { get; } = encoding;

        public string Current { get; set; } = original;

        public List<OperationOutcome> Outcomes { get; } = [];
    }
}
