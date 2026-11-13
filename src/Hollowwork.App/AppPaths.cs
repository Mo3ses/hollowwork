namespace Hollowwork.App;

public static class AppPaths
{
    public static string DataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hollowwork");

    public static string BackupRoot => Path.Combine(DataRoot, "backups");

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");
}
