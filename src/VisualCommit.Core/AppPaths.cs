namespace VisualCommit.Core;

/// <summary>
/// Where the app keeps its files. Everything lives under one data folder, so that tests can point
/// the whole app at a temporary folder.
/// </summary>
public sealed record AppPaths(string DataDirectory)
{
    /// <summary>Environment variable that overrides the data folder.</summary>
    public const string DataDirectoryVariable = "VISUALCOMMIT_DATA_DIR";

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// The data folder for this process: <see cref="DataDirectoryVariable"/> when it is set,
    /// otherwise the per-user default.
    /// </summary>
    public static AppPaths Resolve()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        return new AppPaths(string.IsNullOrWhiteSpace(overridden)
            ? DefaultDataDirectory()
            : Path.GetFullPath(overridden));
    }

    /// <summary>
    /// %APPDATA%\VisualCommit on Windows, ~/Library/Application Support/VisualCommit on macOS,
    /// $XDG_CONFIG_HOME/VisualCommit (usually ~/.config/VisualCommit) on Linux.
    /// Only names the folder; whoever writes to it creates it.
    /// </summary>
    public static string DefaultDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "VisualCommit");
}
