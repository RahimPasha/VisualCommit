using Avalonia.Headless.XUnit;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A bulk run forgets its kept answers when a row fails to sign in (ruling 9,
/// docs/phase-7a-design.md 4.7): the run's <see cref="Core.Operations.PromptGroup.AnswersVersion"/>
/// moves on after an <c>AuthenticationFailed</c> row, and not after another failure.
/// </summary>
public class BulkRunForgetAnswersTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task A_row_failing_to_authenticate_raises_the_answers_version()
    {
        Assert.Equal(1, await AnswersVersionAfterAsync(new GitException(
            "git fetch --progress origin",
            128,
            "remote: Invalid username or token.\nfatal: Authentication failed for 'https://github.com/o/r.git/'\n")));
    }

    [AvaloniaFact]
    public async Task Another_failure_does_not()
    {
        Assert.Equal(0, await AnswersVersionAfterAsync(BulkRepository.CannotFetch("../remotes/missing.git")));
    }

    private static async Task<int> AnswersVersionAfterAsync(Exception failure)
    {
        using var provider = new BulkProvider();
        provider.Add("api").FetchFailure = failure;
        provider.Add("docs");
        using var host = await BulkHost.StartAsync(provider, ["api", "docs"]);

        var run = host.Bulk.FetchAllAsync();
        var current = host.Bulk.Current ?? throw new InvalidOperationException("Fetch all did not start.");
        await run.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal("Fetch failed", host["api"].Operation.Text);
        return current.PromptGroup.AnswersVersion;
    }
}
