using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadPullFastForwardSettingAsync"/> (docs/phase-6-design.md 8.9, D231):
/// the rebase chosen for one pull reads <c>pull.ff</c> alone, as <c>git pull --rebase</c> does
/// before it fetches: <c>git config --get pull.ff</c>, and <c>git config --type=bool --get
/// pull.ff</c> only for a value that is no common boolean word; never a rebase setting, an invalid
/// one included. A value git refuses is the configuration's problem, with <c>pull.ff</c> as its key.
/// </summary>
public class PullFastForwardSettingTests
{
    private const string Get = "git config --get pull.ff";

    private const string AsBool = "git config --type=bool --get pull.ff";

    public static TheoryData<string?, PullFastForwardSetting, bool, string[]> Values => new()
    {
        { null, PullFastForwardSetting.Unset, false, [Get] },
        { "true", PullFastForwardSetting.Always, false, [Get] },
        { "false", PullFastForwardSetting.Never, false, [Get] },
        { "1", PullFastForwardSetting.Always, false, [Get, AsBool] },
        { "only", PullFastForwardSetting.Only, false, [Get, AsBool] },
        { "foo", PullFastForwardSetting.Unset, true, [Get, AsBool] },
        { "ONLY", PullFastForwardSetting.Unset, true, [Get, AsBool] },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public async Task Pull_ff_is_read_alone_and_no_rebase_setting(string? value, PullFastForwardSetting setting, bool refused, string[] commands)
    {
        using var repo = await TempRepo.CreateAsync("pull-ff");
        await repo.CommitFileAsync("README", "base\n", "base");
        if (value is not null)
        {
            await repo.ConfigAsync("pull.ff", value);
        }

        // Rebase settings that git pull --rebase never reads, an invalid one among them.
        await repo.ConfigAsync("branch.main.rebase", "preserve");
        await repo.ConfigAsync("pull.rebase", "foo");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var mark = calls.Snapshot().Count;

        var read = await repository.ReadPullFastForwardSettingAsync(TestCancelled);

        Assert.Equal(commands, calls.Snapshot().Skip(mark).Select(call => call.CommandText));
        Assert.Equal((setting, PullRebaseSetting.None, value), (read.FastForward, read.Rebase, read.FastForwardValue));
        Assert.Null(read.RebaseValue);
        if (refused)
        {
            Assert.Equal(("pull.ff", $"pull.ff is set to {value}, which this app does not understand."), (read.ProblemKey, read.Problem));
            Assert.Equal("git config --get pull.ff", read.NotUnderstood().CommandText);
        }
        else
        {
            Assert.Null(read.Problem);
        }
    }
}
