using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Git;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The harness of phase 8a tried once, without pictures (docs/phase-8a-design.md 0.3, 1.2, 1.4,
/// 1.6, 1.14, 1.15): the phase's names, sizes and picture pattern, the pinned version, the
/// stand-in's variables kept out of the process, the settings file's <c>gitExecutable</c> only while
/// set, and a headless start's seams: the pinned identity, no start-up timeline, the probe for git
/// and the helper's path a test gives, and the git chosen in Settings, which the probe is told.
/// </summary>
public sealed class Phase8aSupportTests : Phase8aChecks
{
    [Fact]
    public void The_phase_the_sizes_the_picture_names_and_the_folder_are_8as()
    {
        Assert.Equal("8a", Phase);
        Assert.Equal((1100, 700), DefaultSize);
        Assert.Equal((1920, 1080), FullSize);
        Assert.EndsWith(Path.Combine("artifacts", "visual", "phase-8a", "scripted"), VisualRun.FolderFor(Phase), StringComparison.Ordinal);

        foreach (var name in new[] { "a1a-about-dark", "b2-icon-light", "f3a-chosen-gone", "g2a-first-run-guide-link", "i2b-notice-try-again", "k10f-palette-end", "l6c-head-row", "u1a-row-name" })
        {
            Assert.Matches(PictureName(), name);
        }

        foreach (var name in new[] { "c1a-commit", "o1-x", "q1-x", "r3-real", "t2-x", "p02b-gitlab-rows", "1a-start", "f1a_bad", "f1abcd-too-long-step", "F1a-upper", "f1a-", "f1a-two--hyphens" })
        {
            Assert.DoesNotMatch(PictureName(), name);
        }

        Assert.Throws<ArgumentException>(() => ClaimPictureName("p02b-gitlab-rows"));
    }

    [Fact]
    public void The_pinned_version_is_the_test_identitys()
    {
        Assert.Equal("0.1.0 (1a2b3c4)", PinnedVersion);
        Assert.Equal(PinnedVersion, TestIdentity.Pinned.Version.Display);
        Assert.Equal(("VisualCommit", AppIdentity.CreatorName, "Copyright (c) 2026 Rahim Khajei"), (TestIdentity.Pinned.Name, TestIdentity.Pinned.Creator, TestIdentity.Pinned.Copyright));
    }

    [Fact]
    public void The_stand_ins_variables_are_out_of_the_process_and_of_every_process_the_tests_start()
    {
        string[] variables = ["VISUALCOMMIT_GIT_STAND_IN", "VISUALCOMMIT_GIT_STAND_IN_VERSION", "VISUALCOMMIT_GIT_STAND_IN_FAILURE"];

        Assert.Equal(variables, new[] { GitLocator.StandInVariable, GitStandIn.VersionVariable, GitStandIn.FailureVariable });
        Assert.Equal(variables, GitIsolation.GitStandInVariables);
        Assert.All(variables, variable => Assert.Contains(new KeyValuePair<string, string?>(variable, null), GitIsolation.MachineIndependence));
        Assert.All(variables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
        Assert.Equal(OperatingSystem.IsWindows() ? "visualcommit-git-stand-in.exe" : "visualcommit-git-stand-in", GitStandIn.FileName);
    }

    [Fact]
    public void The_settings_file_holds_gitExecutable_only_while_it_is_set()
    {
        using var data = new TempDirectory("phase8a support");
        var file = Path.Combine(data.Path, "settings.json");
        var chosen = Path.Combine(data.Path, "git", "bin", "git.exe");

        new JsonSettingsStore(file).Update(settings => settings with { Theme = AppTheme.Light });
        Assert.DoesNotContain("gitExecutable", File.ReadAllText(file), StringComparison.Ordinal);

        new JsonSettingsStore(file).Update(settings => settings with { GitExecutable = chosen });
        Assert.Contains("\"gitExecutable\"", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Equal((chosen, AppTheme.Light), (new JsonSettingsStore(file).Current.GitExecutable, new JsonSettingsStore(file).Current.Theme));

        new JsonSettingsStore(file).Update(settings => settings with { GitExecutable = null });
        Assert.DoesNotContain("gitExecutable", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Null(new JsonSettingsStore(file).Current.GitExecutable);
    }

    [AvaloniaFact]
    public async Task A_headless_start_has_the_pinned_identity_no_timeline_and_the_machines_search_for_git()
    {
        using var data = new TempDirectory("phase8a support");
        using var app = await StartEmptyAsync(data);

        Assert.Same(TestIdentity.Pinned, app.Session.Services.Identity);
        Assert.Same(NullStartupTimeline.Instance, app.Session.Services.Startup);
        Assert.Same(NullStartupTimeline.Instance, AppLaunch.Current.Timeline);
        Assert.Equal(GitAvailability.Available, app.Session.Shell.Git?.Availability);
        Assert.Equal(AskPassLocator.ExpectedPath, ((AskPassBroker)app.Session.Services.Prompts).HelperPath);
    }

    [AvaloniaFact]
    public async Task A_start_takes_the_probe_and_the_helpers_path_a_test_gives()
    {
        using var data = new TempDirectory("phase8a support");
        using var helperFolder = new TempDirectory("phase8a helper");
        var helper = Path.Combine(helperFolder.Path, AskPassLocator.FileName);
        await File.WriteAllTextAsync(helper, string.Empty, TestContext.Current.CancellationToken);
        var probe = new AnsweringProbe(new GitDetection(GitAvailability.NotFound, null, null, null));

        using var app = await StartEmptyAsync(data, gitProbe: probe, askPassHelper: helper);

        Assert.Same(probe.Answer, app.Session.Shell.Git);
        Assert.Equal("Git not found", app.Session.Shell.GitStatusText);
        Assert.Equal(new[] { new GitLookup(null, null) }, probe.Lookups);
        Assert.Equal(helper, ((AskPassBroker)app.Session.Services.Prompts).HelperPath);
        Assert.True(((AskPassBroker)app.Session.Services.Prompts).HelperFound);
    }

    [AvaloniaFact]
    public async Task A_start_tells_the_probe_the_git_chosen_in_settings()
    {
        // docs/phase-8a-design.md 1.10: the session's search is told the git chosen in Settings
        // (GitLookup.ChosenPath), however the probe answers.
        using var data = new TempDirectory("phase8a support");
        var chosen = Path.Combine(data.Path, "git", "bin", OperatingSystem.IsWindows() ? "git.exe" : "git");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { GitExecutable = chosen });
        var probe = new AnsweringProbe(new GitDetection(GitAvailability.NotFound, null, null, null));

        using var app = await StartEmptyAsync(data, gitProbe: probe);

        Assert.Equal(new[] { new GitLookup(chosen, null) }, probe.Lookups);
    }

    /// <summary>A probe that answers every search with one detection and keeps what each search was told.</summary>
    private sealed class AnsweringProbe(GitDetection answer) : IGitSetupProbe
    {
        private readonly Lock _gate = new();
        private readonly List<GitLookup> _lookups = [];

        public GitDetection Answer { get; } = answer;

        public IReadOnlyList<GitLookup> Lookups
        {
            get
            {
                lock (_gate)
                {
                    return [.. _lookups];
                }
            }
        }

        public Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _lookups.Add(lookup);
            }

            return Task.FromResult(Answer);
        }

        public GitGuidance ReadGuidance() => new(GuidancePlatform.Windows, null, null);
    }
}
