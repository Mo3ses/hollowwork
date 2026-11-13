namespace Hollowwork.App;

public static class AppPaths
{
    public static string BackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hollowwork",
        "backups");
}
