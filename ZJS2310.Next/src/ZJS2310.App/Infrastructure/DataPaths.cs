namespace ZJS2310.App.Infrastructure;

public sealed class DataPaths
{
    private DataPaths(string applicationDirectory)
    {
        ApplicationDirectory = applicationDirectory;
        DataDirectory = Path.Combine(applicationDirectory, "Data");
        LogsDirectory = Path.Combine(DataDirectory, "Logs");
        ExportsDirectory = Path.Combine(DataDirectory, "Exports");
        PatternsDirectory = Path.Combine(applicationDirectory, "Assets", "Patterns");
        SettingsFile = Path.Combine(DataDirectory, "appsettings.json");
        RecipesFile = Path.Combine(DataDirectory, "recipes.json");
        UsersFile = Path.Combine(DataDirectory, "users.json");
    }

    public string ApplicationDirectory { get; }
    public string DataDirectory { get; }
    public string LogsDirectory { get; }
    public string ExportsDirectory { get; }
    public string PatternsDirectory { get; }
    public string SettingsFile { get; }
    public string RecipesFile { get; }
    public string UsersFile { get; }

    public static DataPaths Create()
    {
        var paths = new DataPaths(AppContext.BaseDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.LogsDirectory);
        Directory.CreateDirectory(paths.ExportsDirectory);
        Directory.CreateDirectory(paths.PatternsDirectory);
        return paths;
    }
}
