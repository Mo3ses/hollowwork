namespace Hollowwork.App.Settings;

public sealed class AppSettings
{
    public string? LastArchive { get; set; }

    public string Game { get; set; } = "poe1";

    public Dictionary<string, Dictionary<string, string>> PatchParameters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
