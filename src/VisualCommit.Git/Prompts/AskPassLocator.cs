namespace VisualCommit.Git.Prompts;

/// <summary>
/// Finds the askpass helper, <c>visualcommit-askpass</c> (docs/phase-3-design.md 3.1). It is built
/// by its own project, which <c>VisualCommit.Git</c> references, so its app host lies next to this
/// assembly in the app's folder and in every test project's output folder.
/// </summary>
public static class AskPassLocator
{
    /// <summary>The helper's file name on this platform.</summary>
    public static string FileName { get; } = OperatingSystem.IsWindows() ? "visualcommit-askpass.exe" : "visualcommit-askpass";

    /// <summary>The folder the helper is expected in: the folder of this assembly.</summary>
    public static string Folder
    {
        get
        {
            var location = typeof(AskPassLocator).Assembly.Location;
            return string.IsNullOrEmpty(location) ? AppContext.BaseDirectory : Path.GetDirectoryName(location)!;
        }
    }

    /// <summary>The full path the helper is expected at, whether or not it is there.</summary>
    public static string ExpectedPath => Path.GetFullPath(Path.Combine(Folder, FileName));

    /// <summary>The helper's full path, or null when it is missing.</summary>
    public static string? Find() => File.Exists(ExpectedPath) ? ExpectedPath : null;
}
