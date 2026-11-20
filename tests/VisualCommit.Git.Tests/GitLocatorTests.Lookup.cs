using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>The lookup's order and rules (docs/phase-8a-design.md 5.3, 5.4; D423, D449).</summary>
public partial class GitLocatorTests
{
    /// <summary>F6's text, which the stand-in reports as a git with a broken global file.</summary>
    private const string FakeConfigFault = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig";

    /// <summary>The stand-in for git, built next to this assembly.</summary>
    private static readonly string StandIn = Path.Combine(AppContext.BaseDirectory, GitStandIn.FileName);

    /// <summary>git's file name on the machine the tests run on.</summary>
    private static readonly string GitName = OperatingSystem.IsWindows() ? "git.exe" : "git";

    public static TheoryData<string> NoStandIn => ["none", Rooted("gone", GitStandIn.FileName)];

    public static TheoryData<string> RelativePaths => [GitName, Path.Combine("tools", GitName), Path.Combine(".", GitStandIn.FileName)];

    [Fact]
    public async Task The_chosen_git_comes_first_and_is_run_whatever_it_answers()
    {
        // A git on the PATH and a stand-in that is not run: the setting wins, also when it answers badly.
        var onPath = Rooted("tools", GitName);
        var context = Machine(Rooted("tools"), [], onPath);
        var lookup = new GitLookup(StandIn, Rooted("other", GitStandIn.FileName));

        var tooOld = await DetectAsync(lookup, context, environment: StandInEnvironment());
        var broken = await DetectAsync(lookup, context, environment: StandInEnvironment(failure: "fatal: the stand-in is broken"));

        Assert.Equal((GitAvailability.TooOld, StandIn, GitSource.Setting, "2.29.0"), (tooOld.Availability, tooOld.ExecutablePath, tooOld.Source, tooOld.Version.ToString()));
        Assert.Equal((GitAvailability.Broken, StandIn, GitSource.Setting, (int?)128, "fatal: the stand-in is broken"), (broken.Availability, broken.ExecutablePath, broken.Source, broken.ExitCode, broken.ErrorText));
    }

    [Fact]
    public async Task A_chosen_git_with_no_file_is_chosen_missing_with_the_paths_git_as_its_candidate_and_runs_nothing()
    {
        var gone = Rooted("gone", GitName);
        var onPath = Rooted("tools", GitName);
        var calls = new GitCallLog();
        var log = new ListLog();

        var found = await DetectAsync(new GitLookup(gone, null), Machine(Rooted("tools"), [], onPath), calls, log);

        Assert.Equal(GitAvailability.NotFound, found.Availability);
        Assert.Equal((gone, onPath, GitSource.Setting), (found.ChosenMissing, found.PathCandidate, found.Source));
        Assert.Null(found.ExecutablePath);
        Assert.Empty(calls.Snapshot());
        Assert.Contains((LogLevel.Warning, $"The Git chosen in Settings was not found at {gone}."), log.Entries);
        Assert.Equal(GitSetupState.ChosenMissing, GitSetupStates.Of(found, null));
    }

    [Fact]
    public async Task A_chosen_git_with_no_file_and_no_other_git_has_no_candidate()
    {
        var found = await DetectAsync(new GitLookup(Rooted("gone", GitName), null), Machine(Rooted("empty"), []));

        Assert.NotNull(found.ChosenMissing);
        Assert.Null(found.PathCandidate);
    }

    [Fact]
    public async Task Without_a_setting_the_stand_in_is_the_machines_only_git()
    {
        var onPath = Rooted("tools", GitName);

        var found = await DetectAsync(new GitLookup(null, StandIn), Machine(Rooted("tools"), [], onPath), environment: StandInEnvironment());

        Assert.Equal((GitAvailability.TooOld, StandIn, GitSource.StandIn), (found.Availability, found.ExecutablePath, found.Source));
    }

    [Theory]
    [MemberData(nameof(NoStandIn))]
    public async Task A_stand_in_of_none_or_without_a_file_means_the_machine_has_no_git(string standIn)
    {
        var calls = new GitCallLog();

        var found = await DetectAsync(new GitLookup(null, standIn), Machine(Rooted("tools"), [], Rooted("tools", GitName)), calls);

        Assert.Equal(GitAvailability.NotFound, found.Availability);
        Assert.Null(found.ChosenMissing);
        Assert.Empty(calls.Snapshot());
    }

    [Theory]
    [MemberData(nameof(RelativePaths))]
    public async Task A_setting_that_is_not_a_full_path_is_not_run(string chosen)
    {
        // The file "exists" under that name: a relative path would resolve against the app's current folder.
        var calls = new GitCallLog();
        var log = new ListLog();

        var found = await DetectAsync(new GitLookup(chosen, null), Machine(null, [], chosen), calls, log);

        Assert.Equal((GitAvailability.NotFound, chosen), (found.Availability, found.ChosenMissing));
        Assert.Empty(calls.Snapshot());
        Assert.Contains((LogLevel.Warning, $"The Git chosen in Settings is not used: {chosen} is not a full path."), log.Entries);
        Assert.False(GitLocator.IsRunnablePath(chosen));
    }

    [Theory]
    [MemberData(nameof(RelativePaths))]
    public async Task A_stand_in_that_is_not_a_full_path_counts_as_none(string standIn)
    {
        // A git on the PATH too: counted as none, the stand-in leaves the machine unsearched.
        var calls = new GitCallLog();

        var found = await DetectAsync(new GitLookup(null, standIn), Machine(Rooted("tools"), [], standIn, Rooted("tools", GitName)), calls);

        Assert.Equal((GitAvailability.NotFound, (string?)null, GitSource.StandIn), (found.Availability, found.ExecutablePath, found.Source));
        Assert.Empty(calls.Snapshot());
    }

    [Fact]
    public async Task On_Windows_a_setting_or_a_stand_in_that_is_not_an_exe_is_not_run_elsewhere_it_is()
    {
        // A .cmd runs through cmd.exe, where the quoting of git's arguments does not hold (D449).
        var script = Rooted("tools", "git.cmd");
        var calls = new GitCallLog();
        var log = new ListLog();

        var chosen = await DetectAsync(new GitLookup(script, null), Machine(null, [], script), calls, log);
        var standIn = await DetectAsync(new GitLookup(null, script), Machine(Rooted("tools"), [], script, Rooted("tools", GitName)), calls, log);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal((GitAvailability.NotFound, script), (chosen.Availability, chosen.ChosenMissing));
            Assert.Contains((LogLevel.Warning, $"The Git chosen in Settings is not used: {script} is not an .exe file."), log.Entries);

            // A git on the PATH too: the stand-in counts as none and the machine is not searched.
            Assert.Equal((GitAvailability.NotFound, (string?)null, GitSource.StandIn), (standIn.Availability, standIn.ExecutablePath, standIn.Source));
            Assert.Empty(calls.Snapshot());
            Assert.False(GitLocator.IsRunnablePath(script));
        }
        else
        {
            // The file is not there to start: run, it could not be started.
            Assert.Equal((GitAvailability.Broken, script, GitSource.Setting, (int?)null), (chosen.Availability, chosen.ExecutablePath, chosen.Source, chosen.ExitCode));
            Assert.Equal((GitAvailability.Broken, GitSource.StandIn), (standIn.Availability, standIn.Source));
            Assert.True(GitLocator.IsRunnablePath(script));
        }
    }

    [Fact]
    public async Task On_macOS_a_chosen_usr_bin_git_is_not_run_while_xcode_select_fails_and_nothing_else_is()
    {
        var asked = 0;
        var context = new GitSearchContext(false, "/opt/homebrew/bin", [], file => file is "/usr/bin/git" or "/opt/homebrew/bin/git")
        {
            IsMacOS = true,
            CommandLineToolsInstalled = () =>
            {
                asked++;
                return false;
            },
        };
        var calls = new GitCallLog();

        var found = await DetectAsync(new GitLookup("/usr/bin/git", null), context, calls);

        Assert.Equal(GitAvailability.NotFound, found.Availability);
        Assert.True(found.CommandLineToolsMissing);
        Assert.Null(found.ChosenMissing);
        Assert.Equal(GitSetupState.Missing, GitSetupStates.Of(found, null));
        Assert.Equal(1, asked);
        Assert.Empty(calls.Snapshot());
    }

    [Fact]
    public async Task On_macOS_usr_bin_git_on_the_path_or_in_the_usual_folders_is_skipped_while_the_tools_are_missing_asked_once()
    {
        var asked = 0;
        var installed = false;
        GitSearchContext Context(string? path, params string[] files) => new(false, path, ["/usr/bin/git", "/opt/homebrew/bin/git"], file => files.Contains(file))
        {
            IsMacOS = true,
            CommandLineToolsInstalled = () =>
            {
                asked++;
                return installed;
            },
        };

        Assert.Equal("/opt/homebrew/bin/git", GitLocator.FindExecutable(Context("/usr/bin", "/usr/bin/git", "/opt/homebrew/bin/git")));
        Assert.Equal(1, asked);

        asked = 0;
        var none = await DetectAsync(new GitLookup(null, null), Context("/usr/bin", "/usr/bin/git"));
        Assert.Equal((GitAvailability.NotFound, true), (none.Availability, none.CommandLineToolsMissing));
        Assert.Equal(1, asked);

        installed = true;
        asked = 0;
        Assert.Equal("/usr/bin/git", GitLocator.FindExecutable(Context("/usr/bin", "/usr/bin/git", "/opt/homebrew/bin/git")));
        Assert.Equal(1, asked);

        // Elsewhere than macOS nothing is asked.
        asked = 0;
        var linux = new GitSearchContext(false, null, ["/usr/bin/git"], file => file == "/usr/bin/git") { CommandLineToolsInstalled = () => ++asked > 0 };
        Assert.Equal("/usr/bin/git", GitLocator.FindExecutable(linux));
        Assert.Equal(0, asked);
    }

    [Fact]
    public void A_relative_path_entry_that_holds_git_is_skipped()
    {
        var relative = Path.Combine("relative", "git.exe");
        var here = Path.Combine(".", "git.exe");
        var full = Path.Combine(@"C:\Tools", "git.exe");

        Assert.Equal(full, GitLocator.FindExecutable(Windows(@"relative;.;C:\Tools", [], relative, here, full)));
        Assert.Null(GitLocator.FindExecutable(Windows("relative;.", [], relative, here)));
        Assert.Null(GitLocator.FindExecutable(Unix("bin:.", [], Path.Combine("bin", "git"), Path.Combine(".", "git"))));
    }

    [Fact]
    public async Task The_stand_in_answers_too_old_broken_with_128_and_a_line_that_is_no_version()
    {
        var lookup = new GitLookup(null, StandIn);
        var context = Machine(null, []);

        var tooOld = await DetectAsync(lookup, context, environment: StandInEnvironment());
        var broken = await DetectAsync(lookup, context, environment: StandInEnvironment(failure: FakeConfigFault));
        var noVersion = await DetectAsync(lookup, context, environment: StandInEnvironment(version: "this is no version line"));

        Assert.Equal((GitAvailability.TooOld, (GitVersion?)new GitVersion(2, 29, 0)), (tooOld.Availability, tooOld.Version));
        Assert.Equal((GitAvailability.Broken, (int?)128, FakeConfigFault), (broken.Availability, broken.ExitCode, broken.ErrorText));
        Assert.Equal(new GitConfigFault("C:/Users/demo/.gitconfig", 1), broken.ConfigFault);
        Assert.Equal((GitAvailability.Broken, (int?)0, "this is no version line"), (noVersion.Availability, noVersion.ExitCode, noVersion.ErrorText));
        Assert.Null(noVersion.ConfigFault);
    }

    [Fact]
    public async Task A_stand_in_found_as_a_working_git_answers_only_version_through_the_runner_it_gives()
    {
        // The runner the search gives runs the stand-in with the search's environment: anything but --version is refused.
        var found = await DetectAsync(new GitLookup(null, StandIn), Machine(null, []), environment: StandInEnvironment(version: "git version 2.47.0.windows.1"));

        Assert.Equal((GitAvailability.Available, StandIn, GitSource.StandIn), (found.Availability, found.ExecutablePath, found.Source));
        var status = await found.Runner!.RunAsync(new GitCommand("status"), TestCancelled);
        Assert.Equal((1, string.Empty), (status.ExitCode, status.StandardOutput));
        Assert.Equal("fatal: VisualCommit's stand-in for git answers only --version", status.StandardError.Trim());
    }

    [Fact]
    public async Task A_git_that_cannot_be_started_is_broken_with_the_starts_message_and_no_exit_code()
    {
        var gone = Rooted("gone", GitName);
        var context = new GitSearchContext(OperatingSystem.IsWindows(), null, [], file => file == gone);

        var found = await DetectAsync(new GitLookup(gone, null), context);

        Assert.Equal((GitAvailability.Broken, gone, (int?)null), (found.Availability, found.ExecutablePath, found.ExitCode));
        Assert.False(string.IsNullOrWhiteSpace(found.ErrorText));
        Assert.DoesNotContain("could not be started", found.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task On_Windows_a_git_that_is_not_Git_for_Windows_is_used_with_a_warning()
    {
        var plainLog = new ListLog();
        var plain = await DetectAsync(new GitLookup(null, StandIn), Machine(null, []), log: plainLog, environment: StandInEnvironment(version: "git version 2.47.0"));
        var forWindowsLog = new ListLog();
        await DetectAsync(new GitLookup(null, StandIn), Machine(null, []), log: forWindowsLog, environment: StandInEnvironment(version: "git version 2.47.0.windows.1"));

        Assert.Equal(GitAvailability.Available, plain.Availability);
        Assert.Contains((LogLevel.Info, $"Using Git 2.47.0 at {StandIn}."), plainLog.Entries);
        var warning = (LogLevel.Warning, $"Git 2.47.0 at {StandIn} is not Git for Windows; VisualCommit is tested with Git for Windows.");
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(warning, plainLog.Entries);
        }
        else
        {
            Assert.DoesNotContain(warning, plainLog.Entries);
        }

        Assert.DoesNotContain(forWindowsLog.Entries, entry => entry.Message.Contains("is not Git for Windows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Real_git_with_a_broken_global_file_names_its_line()
    {
        // Real git, on every CI job (the floor's 2.34.1 among them): "[user" on line 1 of the session's global file.
        using var lab = new ConfigLab();
        await File.WriteAllTextAsync(lab.GlobalFile, "[user\n", TestCancelled);
        var git = NewRunner().ExecutablePath;
        var log = new ListLog();

        var found = await GitLocator.DetectAsync(new GitLookup(git, null), null, log, null, lab.Environment, TestCancelled);

        Assert.Equal((GitAvailability.Broken, git, GitSource.Setting, (int?)128), (found.Availability, found.ExecutablePath, found.Source, found.ExitCode));
        Assert.StartsWith("fatal: bad config line 1 in file ", found.ErrorText, StringComparison.Ordinal);
        Assert.NotNull(found.ConfigFault);
        Assert.Equal(1, found.ConfigFault.Line);
        Assert.Equal(Normalized(lab.GlobalFile), Normalized(found.ConfigFault.File));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("fatal: bad config line 1 in file", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fatal: bad config line 12 in file C:/Users/demo/.gitconfig", "C:/Users/demo/.gitconfig", 12)]
    [InlineData("warning: something\r\nfatal: bad config line 3 in file /home/demo/.gitconfig\r\n", "/home/demo/.gitconfig", 3)]
    [InlineData("fatal: bad config line 2 in file /home/demo/my config", "/home/demo/my config", 2)]
    public void A_bad_config_line_is_read_from_gits_words(string said, string file, int line) =>
        Assert.Equal(new GitConfigFault(file, line), GitLocator.ConfigFaultOf(said));

    [Theory]
    [InlineData("fatal: not a git repository")]
    [InlineData("error: bad config line 1 in file x")]
    [InlineData("")]
    public void Other_words_name_no_config_line(string said) => Assert.Null(GitLocator.ConfigFaultOf(said));

    /// <summary>A full path on the machine the tests run on, under its root.</summary>
    private static string Rooted(params string[] parts) => Path.Combine([OperatingSystem.IsWindows() ? @"C:\" : "/", .. parts]);

    /// <summary>A context of this machine's kind whose PATH, usual folders and files are the test's, the stand-in always there.</summary>
    private static GitSearchContext Machine(string? path, string[] wellKnown, params string[] existingFiles) =>
        new(OperatingSystem.IsWindows(), path, wellKnown, file => file == StandIn || existingFiles.Contains(file, StringComparer.Ordinal));

    /// <summary>The stand-in's environment: its version line, and the failure it reports.</summary>
    private static Dictionary<string, string?> StandInEnvironment(string? version = null, string? failure = null) => new()
    {
        [GitStandIn.VersionVariable] = version,
        [GitStandIn.FailureVariable] = failure,
    };

    private static Task<GitDetection> DetectAsync(GitLookup lookup, GitSearchContext context, IGitCallLog? calls = null, IAppLog? log = null, IReadOnlyDictionary<string, string?>? environment = null) =>
        GitLocator.DetectAsync(lookup, context, calls, log, null, environment, TestCancelled);

    /// <summary>A path as one file whatever its separators and case.</summary>
    private static string Normalized(string path) => Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar)).ToUpperInvariant();
}
