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
    /// configuration needs Git 2.32 or newer to be left out), none of
    /// <see cref="InheritedVariables"/>, and dates shown in UTC by the app under test (D43). For
    /// the git calls of code under test that does not take <see cref="TempRepo.Environment"/>,
    /// such as the app's own repository reading.
    /// </summary>
    public static void IsolateThisProcess()
    {
        var folder = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "isolation");
        var emptyGitConfig = Path.Combine(folder, "gitconfig");
        Directory.CreateDirectory(folder);
        if (!File.Exists(emptyGitConfig))
        {
            File.WriteAllText(emptyGitConfig, string.Empty);
        }

        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", emptyGitConfig);
        Environment.SetEnvironmentVariable(Core.DateDisplay.TimeZoneVariable, "UTC");

        foreach (var variable in InheritedVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

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
