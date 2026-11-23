using System.Reflection;

namespace VisualCommit.GuideShots;

/// <summary>
/// Where the guide's pictures are made and saved (docs/phase-8a-design.md 1.14, 11.3.5), and the
/// machine's facts that <see cref="LeakGuard"/> keeps out of them, all read once at the process's
/// start (<see cref="Capture"/>), before the harness changes the environment.
/// <para>
/// The demo's root (<see cref="RootVariable"/>) holds the demo repositories and the app's data
/// folder while a shot runs, so that the pictures show its paths (<c>/home/demo</c> in the
/// workflow): it must be absolute, outside the temp folder and the repository, and empty or
/// holding this project's marker (<see cref="Marker"/>). Only this project's own folders there
/// are ever deleted (<see cref="OwnFolders"/>).
/// </para>
/// </summary>
public static class GuideSettings
{
    /// <summary>The demo's root: an empty folder outside the repository and the temp folder.</summary>
    public const string RootVariable = "VISUALCOMMIT_GUIDE_ROOT";

    /// <summary>The pictures' folder; <c>artifacts/guide/pictures/</c> under the repository when unset.</summary>
    public const string PicturesVariable = "VISUALCOMMIT_GUIDE_PICTURES";

    /// <summary>The file this project writes into the demo's root, by which it knows a root it has used before.</summary>
    public const string Marker = ".visualcommit-guide";

    /// <summary>What every shot fails with while the demo's root is not set as it must be.</summary>
    public const string RootRefusal =
        "Set VISUALCOMMIT_GUIDE_ROOT to an empty folder outside the repository and the temp folder, such as /home/demo or C:\\demo.";

    /// <summary>The folders this project makes in the demo's root, and the only ones it deletes there.</summary>
    public static IReadOnlyList<string> OwnFolders { get; } = ["Projects", "remotes", ".config", ".template"];

    private static readonly Lock Gate = new();
    private static bool picturesStarted;

    /// <summary>The repository's root, as the build gave it.</summary>
    public static string RepoRoot { get; } =
        typeof(GuideSettings).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    /// <summary><see cref="RootVariable"/> as the process started with it.</summary>
    public static string? RootSetting { get; private set; }

    /// <summary><see cref="PicturesVariable"/> as the process started with it.</summary>
    public static string? PicturesSetting { get; private set; }

    /// <summary>The machine's facts that no picture may show, read at the start.</summary>
    public static LeakFacts Machine { get; private set; } = LeakFacts.None;

    /// <summary>
    /// Reads the two variables and the machine's facts: the user's name, the machine's name, the
    /// temp folder, the home folder and the repository's path. Called once, by the module's
    /// initializer, before the harness isolates the process.
    /// </summary>
    public static void Capture()
    {
        RootSetting = Environment.GetEnvironmentVariable(RootVariable);
        PicturesSetting = Environment.GetEnvironmentVariable(PicturesVariable);
        Machine = new LeakFacts(
            Environment.UserName,
            Environment.MachineName,
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            RepoRoot);
    }

    /// <summary>
    /// Why <paramref name="root"/> cannot be the demo's root, or null when it can: it must be set
    /// and absolute, lie outside <paramref name="tempPath"/> and <paramref name="repoRoot"/> (and
    /// hold neither), and be a folder that is missing, empty or marked by <see cref="Marker"/>.
    /// Pure but for the look at the folder's entries.
    /// </summary>
    public static string? WhyNotRoot(string? root, string repoRoot, string tempPath)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
        {
            return RootRefusal;
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (IsWithin(full, repoRoot) || IsWithin(repoRoot, full) || IsWithin(full, tempPath) || IsWithin(tempPath, full))
        {
            return RootRefusal;
        }

        if (File.Exists(full))
        {
            return RootRefusal;
        }

        if (Directory.Exists(full)
            && Directory.EnumerateFileSystemEntries(full).Any()
            && !File.Exists(Path.Combine(full, Marker)))
        {
            return RootRefusal;
        }

        return null;
    }

    /// <summary>
    /// The demo's root, made and marked when missing or empty.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="RootRefusal"/>: the root is not set as it must be.</exception>
    public static string Root()
    {
        var why = WhyNotRoot(RootSetting, RepoRoot, Machine.TempPath);
        if (why is not null)
        {
            throw new InvalidOperationException(why);
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RootSetting!));
        Directory.CreateDirectory(full);
        var marker = Path.Combine(full, Marker);
        if (!File.Exists(marker))
        {
            File.WriteAllText(marker, "The VisualCommit user guide's pictures are made here (tests/VisualCommit.GuideShots). Its folders are deleted and made again at each run.\n");
        }

        return full;
    }

    /// <summary>
    /// The pictures' folder: <see cref="PicturesVariable"/>, else <c>artifacts/guide/pictures/</c>
    /// under the repository. The run's first call empties it of <c>*.png</c>, so that it holds
    /// only this run's pictures.
    /// </summary>
    public static string PicturesFolder()
    {
        var folder = string.IsNullOrWhiteSpace(PicturesSetting)
            ? Path.Combine(RepoRoot, "artifacts", "guide", "pictures")
            : Path.GetFullPath(PicturesSetting);
        lock (Gate)
        {
            Directory.CreateDirectory(folder);
            if (!picturesStarted)
            {
                foreach (var old in Directory.EnumerateFiles(folder, "*.png"))
                {
                    File.Delete(old);
                }

                picturesStarted = true;
            }
        }

        return folder;
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or lies inside it, ignoring case and the kind of separator.</summary>
    internal static bool IsWithin(string path, string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        var inner = Normalise(path);
        var outer = Normalise(folder);
        return inner.Equals(outer, StringComparison.OrdinalIgnoreCase)
            || inner.StartsWith(outer + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalise(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Replace('\\', '/').TrimEnd('/');
}
