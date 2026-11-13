using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hollowwork.Patches;

public sealed record PatchTarget(string Root, IReadOnlyList<string> Extensions, IReadOnlyList<string>? Exclude = null);

public sealed record PatchOperation(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("from")] string? From = null,
    [property: JsonPropertyName("to")] string? To = null,
    [property: JsonPropertyName("unless")] string? Unless = null);

public sealed record PatchParameter(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("min")] double Min,
    [property: JsonPropertyName("max")] double Max,
    [property: JsonPropertyName("step")] double Step,
    [property: JsonPropertyName("default")] double Default);

public sealed record PatchDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("targets")] IReadOnlyList<PatchTarget> Targets,
    [property: JsonPropertyName("operations")] IReadOnlyList<PatchOperation> Operations,
    [property: JsonPropertyName("parameters")] IReadOnlyList<PatchParameter>? Parameters = null);

public sealed record PatchSelection(PatchDefinition Definition, IReadOnlyDictionary<string, string> Values)
{
    public static PatchSelection WithoutValues(PatchDefinition definition) =>
        new(definition, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}

public sealed class PatchValidationException(string message) : Exception(message);

public static class PatchLoader
{
    private static readonly string[] Games = ["poe1", "poe2"];
    private static readonly string[] OperationTypes = ["replace", "replaceExtension", "setBool", "stripBlock", "removeLines", "setText", "replaceRegex"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static PatchDefinition Load(string json)
    {
        var definition = JsonSerializer.Deserialize<PatchDefinition>(json, Options)
            ?? throw new PatchValidationException("Patch is empty.");

        Validate(definition);
        return definition;
    }

    public static IReadOnlyList<PatchDefinition> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var result = new List<PatchDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Order())
        {
            result.Add(Load(File.ReadAllText(file)));
        }

        EnsureUniqueIds(result);
        return result;
    }

    public static void Validate(PatchDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)) throw new PatchValidationException("Patch id is required.");
        if (string.IsNullOrWhiteSpace(definition.Name)) throw new PatchValidationException($"Patch '{definition.Id}' has no name.");
        if (string.IsNullOrWhiteSpace(definition.Game)) throw new PatchValidationException($"Patch '{definition.Id}' has no game.");
        if (!Games.Contains(definition.Game)) throw new PatchValidationException($"Patch '{definition.Id}' has unknown game '{definition.Game}'.");
        if (definition.Targets is null || definition.Targets.Count == 0) throw new PatchValidationException($"Patch '{definition.Id}' has no targets.");
        if (definition.Operations is null || definition.Operations.Count == 0) throw new PatchValidationException($"Patch '{definition.Id}' has no operations.");

        foreach (var target in definition.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Root)) throw new PatchValidationException($"Patch '{definition.Id}' has a target without root.");
            if (target.Extensions is null || target.Extensions.Count == 0) throw new PatchValidationException($"Patch '{definition.Id}' has a target without extensions.");
        }

        foreach (var operation in definition.Operations)
        {
            if (!OperationTypes.Contains(operation.Type))
            {
                throw new PatchValidationException($"Patch '{definition.Id}' uses unknown operation '{operation.Type}'.");
            }

            if (operation.Type is "replace" or "replaceExtension" or "setBool" or "replaceRegex" && string.IsNullOrEmpty(operation.From))
            {
                throw new PatchValidationException($"Patch '{definition.Id}' operation '{operation.Type}' requires 'from'.");
            }

            if (operation.Type == "setText" && operation.To is null)
            {
                throw new PatchValidationException($"Patch '{definition.Id}' operation 'setText' requires 'to'.");
            }
        }
    }

    private static void EnsureUniqueIds(IEnumerable<PatchDefinition> definitions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (!seen.Add(definition.Id))
            {
                throw new PatchValidationException($"Duplicate patch id '{definition.Id}'.");
            }
        }
    }
}
