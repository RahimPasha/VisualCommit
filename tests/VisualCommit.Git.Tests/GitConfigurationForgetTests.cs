using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using HeldToolHelpRunner = VisualCommit.Git.Tests.MergeToolHelpTests.HeldToolHelpRunner;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitConfiguration.ForgetToolLookup"/> (docs/phase-8a-design.md 1.2, 5.3): "Check again"
/// found git, so the merge tools' lookup that ran without it is dropped and the next asks the new
/// git; a lookup that still runs is kept. Over <see cref="MergeToolHelpTests"/>' held runner, never
/// the real <c>git mergetool --tool-help</c>.
/// </summary>
public class GitConfigurationForgetTests
{
    private const string Listed = "'git mergetool --tool=<tool>' may be set to one of the following:\n\t\tvimdiff\n";

    [Fact]
    public async Task A_lookup_that_ended_is_forgotten_and_the_next_asks_the_git_of_now()
    {
        using var lab = new ConfigLab();
        var gits = new Queue<IGitRunner>();
        var missing = new GitUnavailableException("Git was not found on this computer.");
        var asked = 0;
        using var configuration = new GitConfiguration(
            _ =>
            {
                asked++;
                return gits.TryDequeue(out var runner) ? Task.FromResult(runner) : Task.FromException<IGitRunner>(missing);
            },
            lab.DataDirectory,
            lab.MergeToolsFile,
            lab.Environment,
            lab.Log);

        // Without git the lookup fails, and stays failed: it is not asked again.
        Assert.True((await configuration.ReadToolHelpAsync(TestCancelled)).Failed);
        Assert.True((await configuration.ReadToolHelpAsync(TestCancelled)).Failed);
        Assert.Equal(1, asked);

        // Git is found: forgotten, the next lookup asks the new git.
        var found = new HeldToolHelpRunner();
        gits.Enqueue(found);
        configuration.ForgetToolLookup();
        var answer = configuration.ReadToolHelpAsync(TestCancelled);
        await found.ToolHelpStarted.WaitAsync(TestCancelled);
        found.Release(new GitResult(0, Listed, string.Empty, TimeSpan.Zero));

        var help = await answer;
        Assert.False(help.Failed);
        Assert.Equal(["vimdiff"], help.Found.Select(tool => tool.Name));
        Assert.Equal(2, asked);
        Assert.Equal(1, found.ToolHelpCalls);
    }

    [Fact]
    public async Task A_lookup_that_still_runs_is_kept()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);

        var first = configuration.ReadToolHelpAsync(TestCancelled);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        configuration.ForgetToolLookup();
        var second = configuration.ReadToolHelpAsync(TestCancelled);
        runner.Release(new GitResult(0, Listed, string.Empty, TimeSpan.Zero));

        Assert.Equal(["vimdiff"], (await first).Found.Select(tool => tool.Name));
        Assert.Equal(["vimdiff"], (await second).Found.Select(tool => tool.Name));
        Assert.Equal(1, runner.ToolHelpCalls);
    }

    [Fact]
    public void The_interfaces_default_keeps_everything()
    {
        IGitConfiguration configuration = new NothingForgotten();

        configuration.ForgetToolLookup();
    }

    /// <summary>A configuration that leaves <see cref="IGitConfiguration.ForgetToolLookup"/> to the interface's default.</summary>
    private sealed class NothingForgotten : IGitConfiguration
    {
        public string GlobalFile => string.Empty;

        public string XdgFile => string.Empty;

        public Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default) => Task.FromResult(new GitConfigRead([], false));

        public Task<IReadOnlyList<GitConfigValue>> ReadIncludedAsync(GitConfigValue directive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GitConfigValue>>([]);

        public Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default) => Task.FromResult(new GitIdentityRead(null, null, null));

        public string? Variable(string name) => null;

        public Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default) => Task.FromResult(MergeToolHelp.FailedLookup);

        public void Dispose()
        {
        }
    }
}
