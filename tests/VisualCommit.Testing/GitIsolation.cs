namespace VisualCommit.Testing;

/// <summary>
/// What tests take away from git so that it behaves the same on every machine: the system and
/// user configuration, and anything the surrounding process hands git through its environment.
/// <see cref="TempRepo"/> applies it to its own git calls; <see cref="Headless.HeadlessTestApp"/>
/// applies it to the test process, for the git calls of the app under test.
/// </summary>
public static class GitIsolation
{
    /// <summary>
    /// Cuts every git process this test process starts off from the machine, through the test
    /// process's own environment, which git inherits: no system or user configuration (the user
    /// configuration needs Git 2.32 or newer to be left out), no search for a repository above
    /// <see cref="CeilingDirectory"/>, none of <see cref="InheritedVariables"/>, and dates shown
    /// in UTC by the app under test (D43). For the git calls of code under test that does not take
    /// <see cref="TempRepo.Environment"/>, such as the app's own repository reading.
    /// </summary>
    public static void IsolateThisProcess()
    {
        var folder = Path.Combine(CeilingDirectory, "isolation");
        var emptyGitConfig = Path.Combine(folder, "gitconfig");
        Directory.CreateDirectory(folder);
        if (!File.Exists(emptyGitConfig))
        {
            File.WriteAllText(emptyGitConfig, string.Empty);
        }

        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", emptyGitConfig);
        Environment.SetEnvironmentVariable(CeilingVariable, CeilingDirectory);
        Environment.SetEnvironmentVariable(Core.DateDisplay.TimeZoneVariable, "UTC");

        foreach (var variable in InheritedVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>The environment variable that names the folders git does not search above.</summary>
    public const string CeilingVariable = "GIT_CEILING_DIRECTORIES";

    /// <summary>
    /// The folder every test folder lies in (<see cref="TempDirectory"/>). Git, looking for the
    /// repository a folder belongs to, searches upwards from it; with this folder as its ceiling
    /// it never leaves the tests' own folders, so a repository somewhere above the temp folder
    /// (a home folder kept in git) cannot turn a test folder into part of it.
    /// <para>
    /// It is written as <see cref="Path.GetTempPath"/> gives it. Git resolves symbolic links in a
    /// ceiling (unless an empty entry comes first, which must not be added), so macOS's /var,
    /// a link to /private/var, still matches the physical folder git works in.
    /// </para>
    /// </summary>
    public static string CeilingDirectory { get; } = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests");

    /// <summary>
    /// Environment variables through which a surrounding process (a git hook, a rebase, a
    /// hosting tool) can give git extra configuration or point it at another repository. These
    /// are the ones git itself lists with <c>git rev-parse --local-env-vars</c>, plus three that
    /// change how a repository is created or addressed.
    /// </summary>
    public static IReadOnlyList<string> InheritedVariables { get; } =
    [
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_CONFIG",
        "GIT_CONFIG_PARAMETERS",
        "GIT_CONFIG_COUNT",
        "GIT_OBJECT_DIRECTORY",
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_IMPLICIT_WORK_TREE",
        "GIT_GRAFT_FILE",
        "GIT_INDEX_FILE",
        "GIT_NO_REPLACE_OBJECTS",
        "GIT_REPLACE_REF_BASE",
        "GIT_PREFIX",
        "GIT_INTERNAL_SUPER_PREFIX",
        "GIT_SHALLOW_FILE",
        "GIT_COMMON_DIR",
        "GIT_NAMESPACE",
        "GIT_TEMPLATE_DIR",
        "GIT_DEFAULT_HASH",
    ];
}
