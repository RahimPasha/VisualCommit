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
    /// <see cref="CeilingDirectory"/>, none of <see cref="InheritedVariables"/>, dates shown
    /// in UTC by the app under test (D43), and <see cref="MachineIndependence"/>: no askpass
    /// program, editor or proxy of the machine, auto-fetch off (D112). For the git calls of code under
    /// test that does not take <see cref="TempRepo.Environment"/>, such as the app's own
    /// repository reading.
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
        foreach (var (variable, value) in AppCommitDates)
        {
            Environment.SetEnvironmentVariable(variable, value);
        }

        foreach (var variable in InheritedVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        foreach (var (variable, value) in MachineIndependence)
        {
            Environment.SetEnvironmentVariable(variable, value);
        }
    }

    /// <summary>
    /// Applies <see cref="MachineIndependence"/> to the environment of a process about to start
    /// (<c>ProcessStartInfo.Environment</c>): removes the variables it removes, in every case the
    /// dictionary holds them in, and sets the others. For a process that does not inherit the
    /// test process's isolated environment, such as the app the real-window pass starts (D112).
    /// </summary>
    public static void ApplyMachineIndependence(IDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        foreach (var (variable, value) in MachineIndependence)
        {
            if (value is null)
            {
                // A dictionary that tells cases apart (on Linux and macOS) may hold Http_Proxy too;
                // on Windows the environment knows no case, and neither does ProcessStartInfo's.
                foreach (var key in environment.Keys.Where(key => string.Equals(key, variable, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    environment.Remove(key);
                }
            }
            else
            {
                environment[variable] = value;
            }
        }
    }

    /// <summary>
    /// The environment variable that overrides the auto-fetch interval of the app under test
    /// (<c>AppSettings.AutoFetchMinutes</c>); the tests set it to 0, so that no fetch starts
    /// that no check asked for (D85, D112).
    /// </summary>
    public const string AutoFetchMinutesVariable = "VISUALCOMMIT_AUTO_FETCH_MINUTES";

    /// <summary>
    /// What the hosts of the tests' own servers are reached by: never through a proxy
    /// (<c>no_proxy</c>, <c>NO_PROXY</c>, D112).
    /// </summary>
    public const string NoProxyHosts = "127.0.0.1,localhost";

    /// <summary>
    /// Programs that answer git's and ssh's prompts in place of the app (VS Code sets
    /// <c>GIT_ASKPASS</c> and <c>SSH_ASKPASS</c> in its terminals; Git Credential Manager reads
    /// <c>GCM_INTERACTIVE</c>), and the ssh commands that override a repository's
    /// <c>core.sshCommand</c> and <c>ssh.variant</c> (TortoiseGit and PuTTY users have
    /// <c>GIT_SSH</c> set): none of them may answer or reroute a test's prompt (D112, D115).
    /// </summary>
    public static IReadOnlyList<string> PromptVariables { get; } =
    [
        "GIT_ASKPASS",
        "SSH_ASKPASS",
        "SSH_ASKPASS_REQUIRE",
        "GCM_INTERACTIVE",
        "GIT_SSH",
        "GIT_SSH_COMMAND",
        "GIT_SSH_VARIANT",
    ];

    /// <summary>
    /// The proxies a machine may name, which git (through curl) would use for a request to a
    /// test server on 127.0.0.1 (D112). curl reads the lower-case names (and <c>HTTPS_PROXY</c>,
    /// <c>ALL_PROXY</c>); both cases are removed.
    /// </summary>
    public static IReadOnlyList<string> ProxyVariables { get; } =
    [
        "http_proxy",
        "https_proxy",
        "all_proxy",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "ALL_PROXY",
    ];

    /// <summary>
    /// The editors a machine may name, which git starts for a commit message or a rebase's todo
    /// (<c>git rebase --continue</c> opens one on <c>COMMIT_EDITMSG</c>; <c>vi</c> when none is
    /// set): none of them may hold up a test (docs/phase-4-design.md 0.5, D151). The app's runner
    /// sets <c>GIT_EDITOR</c> and <c>GIT_SEQUENCE_EDITOR</c> to <c>:</c> for its own calls (D102);
    /// <see cref="TempRepo"/>'s calls get <c>:</c> too.
    /// </summary>
    public static IReadOnlyList<string> EditorVariables { get; } =
    [
        "EDITOR",
        "VISUAL",
        "GIT_EDITOR",
        "GIT_SEQUENCE_EDITOR",
    ];

    /// <summary>
    /// The variables beyond git's configuration that make the app's and git's behaviour depend
    /// on the machine, with what the tests give them instead (D112): <see cref="PromptVariables"/>,
    /// <see cref="ProxyVariables"/> and <see cref="EditorVariables"/> removed (null), <c>no_proxy</c>
    /// and <c>NO_PROXY</c> set to <see cref="NoProxyHosts"/>, and auto-fetch turned off
    /// (<see cref="AutoFetchMinutesVariable"/> 0). Applied to the test process
    /// (<see cref="IsolateThisProcess"/>), to the app the real-window pass starts
    /// (<c>RealApp.Launch</c>) and to <see cref="TempRepo"/>'s git calls, which then set
    /// <c>GIT_EDITOR</c> and <c>GIT_SEQUENCE_EDITOR</c> to <c>:</c>.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> MachineIndependence { get; } =
    [
        .. PromptVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. ProxyVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. EditorVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        new("no_proxy", NoProxyHosts),
        new("NO_PROXY", NoProxyHosts),
        new(AutoFetchMinutesVariable, "0"),
    ];

    /// <summary>
    /// The author and committer date of every commit the app under test makes (D72): 2026-01-02
    /// 09:00 UTC, the day after the scenario repos' own clock starts. With the identity that
    /// scenario repos keep in their configuration (<see cref="TempRepo.CreateAsync"/>), a commit
    /// made through the app gets the same SHA in every run. <see cref="TempRepo"/>'s own commits
    /// set their dates per call, which takes precedence.
    /// </summary>
    public static DateTimeOffset AppCommitDate { get; } = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The environment variables that give the app's commits <see cref="AppCommitDate"/>, for the real-window pass to hand the app.</summary>
    public static IReadOnlyDictionary<string, string> AppCommitDates { get; } = new Dictionary<string, string>
    {
        ["GIT_AUTHOR_DATE"] = "2026-01-02T09:00:00+00:00",
        ["GIT_COMMITTER_DATE"] = "2026-01-02T09:00:00+00:00",
    };

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
