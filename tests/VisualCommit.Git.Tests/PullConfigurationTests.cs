using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadPullConfigurationAsync"/>, Pull's own reader (docs/phase-6-design.md
/// 8.9), for every row of facts 0.3's pull settings, <c>m</c> and the unit letters as each git
/// reads them. <see cref="PullConfigurationAgreementTests"/> has its agreement with the settings
/// screen's reading, <see cref="PullConfigurationIntegrationTests"/> the integration by a
/// configuration read before the fetch. <see cref="PullParityTests"/> and
/// <see cref="PullRebaseParityTests"/> stay unchanged.
/// </summary>
public class PullConfigurationTests
{
    /// <summary>
    /// The repository's own settings ("key=value" pairs, space-separated; "none") and what Pull reads
    /// for main: the fast-forward setting, the rebase setting, the rebase key and value, and the key
    /// of a value it does not understand (null: none).
    /// </summary>
    public static TheoryData<string, PullFastForwardSetting, PullRebaseSetting, string, string?, string?> Settings => new()
    {
        { "none", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null },

        // One key at a time.
        { "pull.rebase=true", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "true", null },
        { "pull.rebase=merges", PullFastForwardSetting.Unset, PullRebaseSetting.Merges, "pull.rebase", "merges", null },
        { "pull.rebase=interactive", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "interactive", null },
        { "pull.rebase=i", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "i", null },
        { "pull.rebase=1", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "1", null },
        { "pull.rebase=yes", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "yes", null },
        { "pull.rebase=false", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "false", null },
        { "pull.rebase=preserve", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "preserve", "pull.rebase" },
        { "pull.rebase=p", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "p", "pull.rebase" },
        { "pull.rebase=b", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "b", "pull.rebase" },
        { "pull.rebase=foo", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "foo", "pull.rebase" },
        { "pull.ff=true", PullFastForwardSetting.Always, PullRebaseSetting.None, "pull.rebase", null, null },
        { "pull.ff=false", PullFastForwardSetting.Never, PullRebaseSetting.None, "pull.rebase", null, null },
        { "pull.ff=only", PullFastForwardSetting.Only, PullRebaseSetting.None, "pull.rebase", null, null },
        { "pull.ff=foo", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, "pull.ff" },

        // Case and numbers.
        { "pull.rebase=TRUE", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "TRUE", null },
        { "pull.rebase=True", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "True", null },
        { "pull.rebase=Yes", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "Yes", null },
        { "pull.rebase=ON", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "ON", null },
        { "pull.rebase=2", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "2", null },
        { "pull.rebase=0x10", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "0x10", null },
        { "pull.rebase=-1", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "-1", null },
        { "pull.rebase=010", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "010", null },
        { "pull.rebase=1K", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "1K", null },
        { "pull.rebase=0x0", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "0x0", null },
        { "pull.rebase=00", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "00", null },
        { "pull.rebase=99999999999", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "99999999999", "pull.rebase" },
        { "pull.rebase=MERGES", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "MERGES", "pull.rebase" },
        { "pull.rebase=Interactive", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "Interactive", "pull.rebase" },
        { "pull.rebase=I", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "I", "pull.rebase" },
        { "pull.ff=ONLY", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, "pull.ff" },
        { "pull.ff=Only", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, "pull.ff" },

        // Combinations: pull.ff = only beats every rebase setting (the integration chooses
        // git merge --ff-only), branch.<b>.rebase beats pull.rebase both ways, and an invalid
        // pull.rebase is then never read. pull.ff is read first, as git pull reads it.
        { "pull.ff=only pull.rebase=true", PullFastForwardSetting.Only, PullRebaseSetting.Rebase, "pull.rebase", "true", null },
        { "pull.ff=only pull.rebase=merges", PullFastForwardSetting.Only, PullRebaseSetting.Merges, "pull.rebase", "merges", null },
        { "pull.ff=only branch.main.rebase=true", PullFastForwardSetting.Only, PullRebaseSetting.Rebase, "branch.main.rebase", "true", null },
        { "branch.main.rebase=true pull.rebase=foo", PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "branch.main.rebase", "true", null },
        { "branch.main.rebase=false pull.rebase=true", PullFastForwardSetting.Unset, PullRebaseSetting.None, "branch.main.rebase", "false", null },
        { "branch.main.rebase=merges", PullFastForwardSetting.Unset, PullRebaseSetting.Merges, "branch.main.rebase", "merges", null },
        { "branch.main.rebase=Merges", PullFastForwardSetting.Unset, PullRebaseSetting.None, "branch.main.rebase", "Merges", "branch.main.rebase" },
        { "pull.ff=false pull.rebase=true", PullFastForwardSetting.Never, PullRebaseSetting.Rebase, "pull.rebase", "true", null },
        { "pull.ff=foo pull.rebase=bar", PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, "pull.ff" },
        { "pull.ff=only pull.rebase=bar", PullFastForwardSetting.Only, PullRebaseSetting.None, "pull.rebase", "bar", "pull.rebase" },
    };

    [Theory]
    [MemberData(nameof(Settings))]
    public async Task Pull_reads_its_settings_as_git_pull_does(
        string settings,
        PullFastForwardSetting fastForward,
        PullRebaseSetting rebase,
        string rebaseKey,
        string? rebaseValue,
        string? problemKey)
    {
        using var repo = await RepositoryWithAsync(settings);
        var repository = await OpenAsync(repo);

        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);

        var fastForwardValue = settings.Split(' ').Select(pair => pair.Split('=', 2)).FirstOrDefault(pair => pair[0] == "pull.ff")?[1];
        var problem = problemKey is null
            ? null
            : $"{problemKey} is set to {(problemKey == "pull.ff" ? fastForwardValue : rebaseValue)}, which this app does not understand.";
        Assert.Equal(new PullConfiguration(fastForward, rebase, rebaseKey, rebaseValue, fastForwardValue, problem, problemKey), read);
    }

    [Fact]
    public async Task A_bare_key_rebases_and_an_empty_value_merges()
    {
        using var repo = await RepositoryWithAsync("none");
        var repository = await OpenAsync(repo);
        var config = Path.Combine(repo.Path, ".git", "config");

        await File.AppendAllTextAsync(config, "[pull]\n\trebase\n", TestCancelled);
        Assert.Equal(PullRebaseSetting.Rebase, (await repository.ReadPullConfigurationAsync("main", TestCancelled)).Rebase);

        await repo.GitAsync("config", "--unset-all", "pull.rebase");
        await File.AppendAllTextAsync(config, "[pull]\n\trebase =\n", TestCancelled);
        var empty = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        Assert.Equal((PullRebaseSetting.None, (string?)null), (empty.Rebase, empty.Problem));
    }

    [Fact]
    public async Task M_is_read_as_each_git_reads_it()
    {
        using var repo = await RepositoryWithAsync("pull.rebase=m");
        var repository = await OpenAsync(repo);

        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);

        // Git 2.36 reads m as the number 0 with a unit, so false: git pull merges; newer gits read
        // it as merges and rebase keeping merge commits (0.3).
        Assert.Null(read.Problem);
        var expected = await GitConfigTestSupport.GitIs236Async() ? PullRebaseSetting.None : await GitReadsAsBooleanAsync(repo, "m") switch
        {
            null => PullRebaseSetting.Merges,
            true => PullRebaseSetting.Rebase,
            false => PullRebaseSetting.None,
        };
        Assert.Equal(expected, read.Rebase);
    }

    [Theory]
    [InlineData("k")]
    [InlineData("G")]
    [InlineData("M")]
    public async Task A_unit_letter_alone_is_read_as_each_git_reads_it(string value)
    {
        using var repo = await RepositoryWithAsync($"pull.rebase={value} pull.ff={value}");
        var repository = await OpenAsync(repo);

        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);

        // Git 2.36 reads them as false (git pull merges); newer gits refuse them (2.55 does, 0.3).
        // Pull follows its git either way.
        var gitBoolean = await GitReadsAsBooleanAsync(repo, value);
        if (await GitConfigTestSupport.GitIs236Async())
        {
            Assert.False(gitBoolean);
        }

        if (gitBoolean is null)
        {
            Assert.Equal("pull.ff", read.ProblemKey);
            Assert.Equal($"pull.ff is set to {value}, which this app does not understand.", read.Problem);
        }
        else
        {
            Assert.False(gitBoolean);
            Assert.Equal(new PullConfiguration(PullFastForwardSetting.Never, PullRebaseSetting.None, "pull.rebase", value, value, null, null), read);
        }
    }

    [Fact]
    public async Task A_branch_key_names_its_branch_exactly()
    {
        using var repo = await RepositoryWithAsync("branch.Feature/x.y.rebase=true");
        var repository = await OpenAsync(repo);

        var exact = await repository.ReadPullConfigurationAsync("Feature/x.y", TestCancelled);
        var otherCase = await repository.ReadPullConfigurationAsync("feature/x.y", TestCancelled);

        Assert.Equal((PullRebaseSetting.Rebase, "branch.Feature/x.y.rebase"), (exact.Rebase, exact.RebaseKey));
        Assert.Equal((PullRebaseSetting.None, "pull.rebase"), (otherCase.Rebase, otherCase.RebaseKey));
    }

    [Fact]
    public async Task Precedence_is_per_key_across_scopes()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal("[pull]\n\tff = only\n");
        using var repo = await RepositoryWithAsync("pull.rebase=true");
        var repository = await lab.OpenAsync(repo);

        // The global pull.ff = only still applies: git pull refuses diverged branches.
        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        Assert.Equal((PullFastForwardSetting.Only, PullRebaseSetting.Rebase), (read.FastForward, read.Rebase));

        // A mode writes both keys: then the repository's pull.ff = true wins and git pull rebases.
        await repo.ConfigAsync("pull.ff", "true");
        read = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        Assert.Equal((PullFastForwardSetting.Always, PullRebaseSetting.Rebase), (read.FastForward, read.Rebase));
    }

    [Fact]
    public void A_configuration_without_a_problem_has_nothing_to_report()
    {
        var configuration = new PullConfiguration(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null, null, null);

        Assert.Throws<InvalidOperationException>(configuration.NotUnderstood);
    }

    /// <summary>A repository on an unborn main with <paramref name="settings"/> ("key=value" pairs, space-separated; "none") in its own configuration.</summary>
    internal static async Task<TempRepo> RepositoryWithAsync(string settings)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            if (settings != "none")
            {
                foreach (var pair in settings.Split(' ').Select(setting => setting.Split('=', 2)))
                {
                    await repo.ConfigAsync(pair[0], pair[1]);
                }
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>How the repository's git reads <paramref name="value"/> as a boolean: true, false, or null when it is none.</summary>
    internal static async Task<bool?> GitReadsAsBooleanAsync(TempRepo repo, string value)
    {
        var result = await repo.GitAllowingFailureAsync("-c", "probe.value=" + value, "config", "--type=bool", "--get", "probe.value");
        return result.Succeeded ? result.StandardOutput.Trim() == "true" : null;
    }
}

/// <summary>
/// <see cref="PullConfigurationTests"/>: Pull's reader and the settings screen's reading
/// (<see cref="PullModes"/>, <see cref="GitBoolean"/>) agree on each value <see cref="PullModesTests"/>
/// names; for a unit letter alone and <c>m</c> Pull follows its git and the screen shows "Other". A
/// class of its own, so that it runs beside the others.
/// </summary>
public class PullConfigurationAgreementTests
{
    /// <summary>The values <see cref="PullModesTests"/> names, for <c>pull.rebase</c> and for <c>pull.ff</c>.</summary>
    public static TheoryData<string, string> NamedValues()
    {
        var data = new TheoryData<string, string>();
        foreach (var value in new[] { "true", "false", "TRUE", "Yes", "ON", "1", "10k", "0x10", "-1", "010", "1K", "0x0", "00", "99999999999", "merges", "MERGES", "m", "M", "k", "interactive", "i", "only", "ONLY", "preserve", "sometimes", "foo" })
        {
            data.Add("pull.rebase", value);
            data.Add("pull.ff", value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NamedValues))]
    public async Task Pulls_reader_and_the_screens_reading_agree(string key, string value)
    {
        using var repo = await PullConfigurationTests.RepositoryWithAsync($"{key}={value}");
        var repository = await OpenAsync(repo);
        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        var screen = key == "pull.rebase" ? PullModes.Read(value, rebaseIsBare: false, null) : PullModes.Read(null, rebaseIsBare: false, value);
        var is236 = await GitConfigTestSupport.GitIs236Async();

        if (screen.OtherNote == PullModes.RefusedNote)
        {
            Assert.Equal(key, read.ProblemKey);
            return;
        }

        if (screen.OtherNote is PullModes.UnitLetterNote or PullModes.MNote)
        {
            // Pull follows its git; the screen shows "Other" with the line that the gits differ.
            var gitBoolean = await PullConfigurationTests.GitReadsAsBooleanAsync(repo, value);
            if (is236)
            {
                Assert.False(gitBoolean);
            }

            if (gitBoolean is { } flag)
            {
                Assert.Null(read.Problem);
                if (key == "pull.rebase")
                {
                    Assert.Equal(flag ? PullRebaseSetting.Rebase : PullRebaseSetting.None, read.Rebase);
                }
                else
                {
                    Assert.Equal(flag ? PullFastForwardSetting.Always : PullFastForwardSetting.Never, read.FastForward);
                }
            }
            else if (value == "m" && key == "pull.rebase")
            {
                Assert.Equal((PullRebaseSetting.Merges, (string?)null), (read.Rebase, read.Problem));
            }
            else
            {
                Assert.Equal(key, read.ProblemKey);
            }

            return;
        }

        Assert.Null(read.Problem);
        if (key == "pull.rebase")
        {
            var expected = screen switch
            {
                { Mode: PullMode.Rebase } or { OtherNote: PullModes.InteractiveNote } => PullRebaseSetting.Rebase,
                { Mode: PullMode.RebaseMerges } => PullRebaseSetting.Merges,
                { Mode: PullMode.Merge } => PullRebaseSetting.None,
                _ => throw new InvalidOperationException($"pull.rebase={value} reads as {screen}."),
            };
            Assert.Equal(expected, read.Rebase);
            Assert.Equal(GitBoolean.TryParse(value, bare: false, out var flag) ? (flag ? PullRebaseSetting.Rebase : PullRebaseSetting.None) : expected, read.Rebase);
        }
        else
        {
            var expected = screen.Mode switch
            {
                PullMode.FastForwardOnly => PullFastForwardSetting.Only,
                PullMode.Merge => PullFastForwardSetting.Always,
                PullMode.MergeCommit => PullFastForwardSetting.Never,
                _ => throw new InvalidOperationException($"pull.ff={value} reads as {screen}."),
            };
            Assert.Equal(expected, read.FastForward);
        }
    }

    [Theory]
    [InlineData("k")]
    [InlineData("G")]
    [InlineData("M")]
    [InlineData("m")]
    public async Task Under_pull_ff_only_pull_rebase_is_still_read_as_git_pull_reads_it(string value)
    {
        using var repo = await PullConfigurationTests.RepositoryWithAsync($"pull.ff=only pull.rebase={value}");
        var repository = await OpenAsync(repo);
        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        var screen = PullModes.Read(value, rebaseIsBare: false, "only");

        // git pull reads both keys before anything else; without a remote here nothing is fetched,
        // and a value it accepts ends at "There is no tracking information" (exit 1); one it
        // refuses at "fatal: invalid value for 'pull.rebase'" (128), on every git. The words are
        // asserted on the report's git only.
        var pull = await PullParity.RunGitAsync(repo, "pull");
        Assert.True(pull.ExitCode is 1 or 128, $"git pull exited {pull.ExitCode}: {pull.StandardError}");
        var gitRefuses = pull.ExitCode == 128;
        if (await GitConfigTestSupport.GitIs236Async())
        {
            Assert.Contains(gitRefuses ? "invalid value for 'pull.rebase'" : "There is no tracking information", pull.StandardError, StringComparison.Ordinal);
        }

        Assert.Equal(gitRefuses, read.ProblemKey == "pull.rebase");
        if (!gitRefuses)
        {
            Assert.Equal((PullFastForwardSetting.Only, (string?)null), (read.FastForward, read.Problem));
        }

        if (value == "m")
        {
            // Both gits accept m (2.36 as false, newer ones as merges): fast-forward only.
            Assert.False(gitRefuses);
            Assert.Equal(new PullModeReading(PullMode.FastForwardOnly, null, null), screen);
            return;
        }

        // A unit letter alone: 2.36 fast-forwards only, 2.55 refuses every pull; the screen shows
        // "Other" with the line that the gits differ, and Pull does what its git does.
        Assert.Equal(new PullModeReading(null, $"Other: pull.rebase = {value}", PullModes.UnitLetterNote), screen);
        var version = await GitConfigTestSupport.GitVersionAsync();
        if (version is { Major: 2, Minor: 36 })
        {
            Assert.False(gitRefuses);
        }
        else if (version >= new GitVersion(2, 55, 0))
        {
            Assert.True(gitRefuses, pull.StandardError);
        }
    }
}

/// <summary>
/// <see cref="PullConfigurationTests"/>: <see cref="GitRepository.IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>
/// with a configuration read before the fetch (8.9) integrates as with its own read, decides by it
/// without reading again, and throws its problem before anything changes; the chosen rebase reads
/// <c>pull.ff</c> alone and refuses a value git would refuse, as <c>git pull --rebase</c> does. A
/// class of its own, so that it runs beside the others.
/// </summary>
public class PullConfigurationIntegrationTests
{
    [Fact]
    public async Task A_configuration_read_before_integrates_as_the_integrations_own_read()
    {
        var template = await PullParityTemplates.SyncAsync(PullState.Diverged);
        using var given = await template.CopyAsync();
        using var own = await template.CopyAsync();
        await given.ConfigAsync("pull.rebase", "true");
        await own.ConfigAsync("pull.rebase", "true");

        var withConfiguration = await IntegrateAsync(given, read => new PullIntegrationOptions(read));
        var without = await IntegrateAsync(own, _ => null);

        Assert.Equal(PullOutcome.Rebased, without.Outcome);
        Assert.Equal(without, withConfiguration);
        Assert.Equal(await own.HeadAsync(), await given.HeadAsync());
    }

    [Fact]
    public async Task A_given_configuration_decides_and_nothing_is_read_again()
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Diverged)).CopyAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var branch = refs.Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);

        // Nothing is set here, so a read would merge; the configuration given says rebase, as one
        // read before a terminal's change would.
        var configuration = new PullConfiguration(PullFastForwardSetting.Unset, PullRebaseSetting.Rebase, "pull.rebase", "true", null, null, null);
        var mark = calls.Snapshot().Count == 0 ? 0 : calls.Snapshot()[^1].Sequence;
        var result = await repository.IntegratePullAsync(branch, PullIntegration.AsConfigured, null, pullOptions: new PullIntegrationOptions(configuration), cancellationToken: TestCancelled);

        Assert.Equal(PullOutcome.Rebased, result.Outcome);
        Assert.DoesNotContain(
            calls.Snapshot().Where(call => call.Sequence > mark),
            call => call.CommandText.StartsWith("git config", StringComparison.Ordinal) && call.CommandText.Contains("pull.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_terminals_change_after_the_read_is_not_seen()
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Diverged)).CopyAsync();
        await repo.ConfigAsync("pull.rebase", "true");

        // Read before the fetch; the terminal then sets the pull to merge.
        var result = await IntegrateAsync(repo, read => new PullIntegrationOptions(read), afterRead: () => repo.ConfigAsync("pull.rebase", "false"));

        Assert.Equal(PullOutcome.Rebased, result.Outcome);
    }

    [Fact]
    public async Task A_configuration_with_a_problem_fails_before_anything_changes()
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Diverged)).CopyAsync();
        await repo.ConfigAsync("pull.rebase", "preserve");
        var head = await repo.HeadAsync();
        var repository = await OpenAsync(repo);
        var read = await repository.ReadPullConfigurationAsync("main", TestCancelled);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);

        var failure = await Assert.ThrowsAsync<GitException>(() =>
            repository.IntegratePullAsync(branch, PullIntegration.AsConfigured, null, pullOptions: new PullIntegrationOptions(read), cancellationToken: TestCancelled));

        Assert.Equal(("git config --get pull.rebase", (int?)0, "pull.rebase is set to preserve, which this app does not understand."), (failure.CommandText, failure.ExitCode, failure.StandardError));
        Assert.Equal(read.NotUnderstood().Message, failure.Message);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("ONLY")]
    [InlineData("k")]
    public async Task The_chosen_rebase_refuses_an_invalid_pull_ff_as_git_pull_rebase_does(string value)
    {
        var template = await PullParityTemplates.SyncAsync(PullState.Diverged);
        using var appCopy = await template.CopyAsync();
        using var gitCopy = await template.CopyAsync();
        await appCopy.ConfigAsync("pull.ff", value);
        await gitCopy.ConfigAsync("pull.ff", value);
        var head = await appCopy.HeadAsync();
        var upstream = (await gitCopy.GitAsync("rev-parse", "origin/main")).StandardOutput.Trim();

        // git pull --rebase reads pull.ff (no --ff option given) and refuses before it fetches
        // ("fatal: invalid value for 'pull.ff'", 128): foo and ONLY on every git, k on newer gits
        // only (2.36 reads it as false, 0.3). The words are asserted on the report's git only.
        var pull = await PullParity.RunGitAsync(gitCopy, "pull", "--rebase");
        var gitRefuses = pull.ExitCode == 128;
        if (gitRefuses && await GitConfigTestSupport.GitIs236Async())
        {
            Assert.Contains("invalid value for 'pull.ff'", pull.StandardError, StringComparison.Ordinal);
        }

        if (value != "k")
        {
            Assert.True(gitRefuses, pull.StandardError);
        }

        // The app: its fetch, then the chosen rebase with no configuration given.
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), appCopy.Path, TestCancelled);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);
        var mark = calls.Snapshot()[^1].Sequence;
        PullResult? result = null;
        GitException? failure = null;
        try
        {
            result = await repository.IntegratePullAsync(branch, PullIntegration.Rebase, null, cancellationToken: TestCancelled);
        }
        catch (GitException exception)
        {
            failure = exception;
        }

        Assert.Equal(gitRefuses, failure is not null);
        if (failure is not null)
        {
            Assert.Equal(("git config --get pull.ff", (int?)0, $"pull.ff is set to {value}, which this app does not understand."), (failure.CommandText, failure.ExitCode, failure.StandardError));
            Assert.Equal(head, await appCopy.HeadAsync());
            Assert.Equal(string.Empty, (await appCopy.GitAsync("status", "--porcelain")).StandardOutput);
            Assert.Equal(upstream, (await gitCopy.GitAsync("rev-parse", "origin/main")).StandardOutput.Trim());
        }
        else
        {
            // pull.ff = false gives way to the rebase.
            Assert.Equal(PullOutcome.Rebased, result!.Outcome);
            Assert.True(pull.Succeeded, pull.StandardError);
        }

        // pull.ff alone was read: no rebase setting, as git pull --rebase reads none.
        Assert.DoesNotContain(
            calls.Snapshot().Where(call => call.Sequence > mark),
            call => call.CommandText.StartsWith("git config", StringComparison.Ordinal)
                && (call.CommandText.Contains("pull.rebase", StringComparison.Ordinal) || call.CommandText.Contains("branch.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_configuration_given_to_the_chosen_rebase_counts_only_for_pull_ff()
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Diverged)).CopyAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);
        var head = await repo.HeadAsync();
        var mark = calls.Snapshot()[^1].Sequence;
        var fastForwardProblem = new PullConfiguration(
            PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, "foo", "pull.ff is set to foo, which this app does not understand.", "pull.ff");
        var rebaseProblem = new PullConfiguration(
            PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "preserve", null, "pull.rebase is set to preserve, which this app does not understand.", "pull.rebase");

        var failure = await Assert.ThrowsAsync<GitException>(() =>
            repository.IntegratePullAsync(branch, PullIntegration.Rebase, null, pullOptions: new PullIntegrationOptions(fastForwardProblem), cancellationToken: TestCancelled));
        Assert.Equal(fastForwardProblem.NotUnderstood().Message, failure.Message);
        Assert.Equal(head, await repo.HeadAsync());

        // A rebase setting git pull --rebase never reads: ignored, an invalid one included.
        var result = await repository.IntegratePullAsync(branch, PullIntegration.Rebase, null, pullOptions: new PullIntegrationOptions(rebaseProblem), cancellationToken: TestCancelled);
        Assert.Equal(PullOutcome.Rebased, result.Outcome);

        // Given a configuration, nothing of pull's is read again.
        Assert.DoesNotContain(
            calls.Snapshot().Where(call => call.Sequence > mark),
            call => call.CommandText.StartsWith("git config", StringComparison.Ordinal) && call.CommandText.Contains("pull.", StringComparison.Ordinal));
    }

    /// <summary>
    /// Fetches <paramref name="repo"/>'s upstream, then integrates main with the options
    /// <paramref name="options"/> makes of the configuration read before the fetch (8.9's order);
    /// <paramref name="afterRead"/> runs between the read and the fetch.
    /// </summary>
    private static async Task<PullResult> IntegrateAsync(TempRepo repo, Func<PullConfiguration, PullIntegrationOptions?> options, Func<Task>? afterRead = null)
    {
        var repository = await OpenAsync(repo);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        var read = await repository.ReadPullConfigurationAsync(branch.Name, TestCancelled);
        if (afterRead is not null)
        {
            await afterRead();
        }

        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);
        return await repository.IntegratePullAsync(branch, PullIntegration.AsConfigured, null, pullOptions: options(read), cancellationToken: TestCancelled);
    }
}
