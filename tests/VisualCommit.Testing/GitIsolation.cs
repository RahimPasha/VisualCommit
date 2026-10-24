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
    /// program, editor, proxy or identity of the machine, auto-fetch off (D112). For the git calls of code under
    /// test that does not take <see cref="TempRepo.Environment"/>, such as the app's own
    /// repository reading.
    /// <para>
    /// Phase 6 (docs/phase-6-design.md 8.12, 17.2): the user configuration is a file in a folder
    /// nobody makes (<see cref="NoGlobalConfigFile"/>), so that reads see none and a write outside an
    /// app session's own environment (<see cref="SessionGitEnvironment"/>) fails, with exit 255, in
    /// the test that made it, where the shared empty file of phases 1 to 5 took it and handed it to
    /// every later test; and <c>XDG_CONFIG_HOME</c> is an empty folder of the tests'
    /// (<see cref="ProcessXdgConfigHome"/>), so that the user's <c>~/.config/git/ignore</c> and
    /// <c>attributes</c> reach no git call, which <c>GIT_CONFIG_GLOBAL</c> alone does not stop
    /// (0.10). <see cref="GlobalConfigurationWrittenOutsideASession"/> is the tripwire.
    /// </para>
    /// </summary>
    public static void IsolateThisProcess()
    {
        Directory.CreateDirectory(ProcessXdgConfigHome);

        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", NoGlobalConfigFile);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", ProcessXdgConfigHome);
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

    /// <summary>The folder of the test processes' own isolation files: <c>&lt;ceiling&gt;/isolation</c>.</summary>
    public static string IsolationFolder => Path.Combine(CeilingDirectory, "isolation");

    /// <summary>
    /// The test processes' <c>GIT_CONFIG_GLOBAL</c> (docs/phase-6-design.md 17.2): a file in
    /// <c>&lt;ceiling&gt;/isolation/no-global/</c>, a folder nothing makes. Git reads no global
    /// configuration from it (a missing file counts as empty), and a <c>git config --global</c>
    /// write fails with exit 255 and makes no folder (0.10), so a global write outside an app
    /// session fails where it was made.
    /// </summary>
    public static string NoGlobalConfigFile => Path.Combine(IsolationFolder, "no-global", "gitconfig");

    /// <summary>
    /// The test processes' <c>XDG_CONFIG_HOME</c>: an empty <c>&lt;ceiling&gt;/isolation/xdg</c>,
    /// so that no git call of a test reads the user's <c>~/.config/git/</c> files (its
    /// <c>ignore</c> and <c>attributes</c>, which <c>GIT_CONFIG_GLOBAL</c> does not cover, 0.10).
    /// On Linux and macOS .NET's <c>SpecialFolder.ApplicationData</c> follows it too (8.12).
    /// </summary>
    public static string ProcessXdgConfigHome => Path.Combine(IsolationFolder, "xdg");

    /// <summary>
    /// The shared empty global configuration that phases 1 to 5 gave every test process and the
    /// real-window app: no build of this code uses it any more. A stale build would (the
    /// real-window pass's project is not rebuilt by <c>dotnet build</c> at the root), so the tripwire
    /// checks that it stayed empty.
    /// </summary>
    public static string OldSharedGlobalConfigFile => Path.Combine(IsolationFolder, "gitconfig");

    /// <summary>The folder of an app session's own harness files in its data folder: <c>&lt;data&gt;/harness</c>.</summary>
    public static string SessionHarnessFolder(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        return Path.Combine(dataDirectory, "harness");
    }

    /// <summary>
    /// An app session's global git configuration (docs/phase-6-design.md 8.12, 17.2):
    /// <c>&lt;data&gt;/harness/global.gitconfig</c>, which git makes at its first write. Whatever
    /// the app (or a check, <c>ShellDriver.SeedGlobalGitConfig</c>) writes there is that data
    /// folder's alone, and a restart on the same folder keeps it.
    /// </summary>
    public static string SessionGlobalConfigFile(string dataDirectory) =>
        Path.Combine(SessionHarnessFolder(dataDirectory), "global.gitconfig");

    /// <summary>
    /// The git environment of an app session on <paramref name="dataDirectory"/>
    /// (docs/phase-6-design.md 8.12, 17.2, amending D112): <c>GIT_CONFIG_GLOBAL</c> =
    /// <see cref="SessionGlobalConfigFile"/> and <c>XDG_CONFIG_HOME</c> =
    /// <c>&lt;data&gt;/harness/xdg</c>, both made here (the file by git's first write). Both
    /// harnesses hand it to the app: <c>ShellDriver.Start</c> through <c>AppSession.Start</c>'s
    /// <c>gitEnvironment</c>, which the session's one runner applies, and <c>RealApp.Launch</c>
    /// through the app's environment. Only this and <c>FetchHold.Install</c> make the harness
    /// folder, never a git call.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> SessionGitEnvironment(string dataDirectory)
    {
        var xdg = Path.Combine(SessionHarnessFolder(dataDirectory), "xdg");
        Directory.CreateDirectory(xdg);
        return new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = SessionGlobalConfigFile(dataDirectory),
            ["XDG_CONFIG_HOME"] = xdg,
        };
    }

    /// <summary>
    /// The tripwire (docs/phase-6-design.md 17.2, 8.12): what shows that some git call wrote a
    /// global configuration outside an app session's own environment. Empty when nothing did; else
    /// one line per finding: <c>&lt;ceiling&gt;/isolation/no-global</c> exists (git never makes it,
    /// so something else did and a write may have landed there), <see cref="ProcessXdgConfigHome"/>
    /// holds a <c>git</c> folder (where git's <c>config</c>, <c>ignore</c> and <c>attributes</c> would
    /// lie), or <see cref="OldSharedGlobalConfigFile"/>, which a stale build would use, is not empty.
    /// </summary>
    public static IReadOnlyList<string> GlobalConfigurationWrittenOutsideASession() =>
        GlobalConfigurationWrittenOutsideASession(IsolationFolder);

    /// <summary>The tripwire over another isolation folder laid out as <see cref="IsolationFolder"/>, so that its rules are tested without touching the one every test process shares.</summary>
    internal static IReadOnlyList<string> GlobalConfigurationWrittenOutsideASession(string isolationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isolationFolder);
        var findings = new List<string>();
        var noGlobal = Path.Combine(isolationFolder, "no-global");
        if (Directory.Exists(noGlobal))
        {
            findings.Add($"{noGlobal} exists: the test processes' global git configuration must lie in a folder nothing makes.");
        }

        var xdgGit = Path.Combine(isolationFolder, "xdg", "git");
        if (Directory.Exists(xdgGit))
        {
            findings.Add($"{xdgGit} exists: git's user files there would reach every test's git calls.");
        }

        var oldShared = Path.Combine(isolationFolder, "gitconfig");
        if (File.Exists(oldShared) && new FileInfo(oldShared).Length > 0)
        {
            findings.Add($"{oldShared} is not empty: a stale build wrote global git configuration into the file every test shared.");
        }

        return findings;
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
    /// The variables that a shell hands every program it starts and that git's own shells set for
    /// themselves: Git Bash's <c>MSYSTEM</c> (<c>MINGW64</c>). Git for Windows sets it again for the
    /// shell it starts for a hook, an alias or <c>core.sshCommand</c> when it is missing, so removing
    /// it changes nothing for git and keeps the tests from depending on the shell that started them:
    /// a run started from Git Bash got it, one started from PowerShell did not. The phase 6 research
    /// saw Visual Studio's trimmed Git 2.55 crash running a shell under it (docs/plan.md risk 28);
    /// a check on 2026-10-18 (a hook, a shell <c>core.sshCommand</c>, <c>FakeSshTests</c>) did not
    /// reproduce that.
    /// </summary>
    public static IReadOnlyList<string> ShellVariables { get; } =
    [
        "MSYSTEM",
    ];

    /// <summary>
    /// The identity a machine may hand git through its environment, which beats every
    /// configuration file (docs/phase-6-design.md 0.3, 0.10): a check of the settings screen's
    /// identity (8.6) must see what the files say. Not the <c>*_DATE</c> variables: the harnesses
    /// set those for the app's commits (<see cref="AppCommitDates"/>, D72). <see cref="TempRepo"/>
    /// sets its own author after the isolation.
    /// </summary>
    public static IReadOnlyList<string> IdentityVariables { get; } =
    [
        "GIT_AUTHOR_NAME",
        "GIT_AUTHOR_EMAIL",
        "GIT_COMMITTER_NAME",
        "GIT_COMMITTER_EMAIL",
        "EMAIL",
    ];

    /// <summary>
    /// <c>PATHEXT</c> as the tests have it: Windows' default
    /// (<see cref="Core.ToolCatalogues.DefaultWindowsPathExtensions"/>, the value the harnesses'
    /// fixed catalogues give, docs/phase-6-design.md 12.9), not the machine's, to which installers
    /// add (<c>.PY;.PYW</c> from Python's), for the tests that build pieces of the app without a
    /// session. Set on every platform; only Windows reads it.
    /// </summary>
    public const string PathExtensionsVariable = "PATHEXT";

    /// <summary>
    /// The variables beyond git's configuration that make the app's and git's behaviour depend
    /// on the machine, with what the tests give them instead (D112): <see cref="PromptVariables"/>,
    /// <see cref="ProxyVariables"/>, <see cref="EditorVariables"/>, <see cref="ShellVariables"/>
    /// and <see cref="IdentityVariables"/> removed (null), <c>no_proxy</c> and <c>NO_PROXY</c> set
    /// to <see cref="NoProxyHosts"/>, auto-fetch turned off (<see cref="AutoFetchMinutesVariable"/>
    /// 0) and <c>PATHEXT</c> fixed (<see cref="PathExtensionsVariable"/>). Applied to the test
    /// process (<see cref="IsolateThisProcess"/>), to the app the real-window pass starts
    /// (<c>RealApp.Launch</c>) and to <see cref="TempRepo"/>'s git calls, which then set
    /// <c>GIT_EDITOR</c> and <c>GIT_SEQUENCE_EDITOR</c> to <c>:</c> and their own author.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> MachineIndependence { get; } =
    [
        .. PromptVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. ProxyVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. EditorVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. ShellVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        .. IdentityVariables.Select(variable => new KeyValuePair<string, string?>(variable, null)),
        new("no_proxy", NoProxyHosts),
        new("NO_PROXY", NoProxyHosts),
        new(AutoFetchMinutesVariable, "0"),
        new(PathExtensionsVariable, Core.ToolCatalogues.DefaultWindowsPathExtensions),
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
