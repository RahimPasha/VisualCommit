using System.Reflection;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A fork's pull request whose <c>FETCH_HEAD</c> another fetch rewrote (docs/phase-7a-design.md
/// 10.5, 10.6; D305, plan risk 33): <see cref="IGitRepository.FetchToFetchHeadAsync"/> answers null,
/// so "Fetch pull request #n" fails with <c>FetchHeadChanged</c>'s text, creates no branch and leaves
/// HEAD where it was. <c>ShellDriver</c> has no seam for the repository, so phase 3's "ops" is opened
/// through <see cref="OperationsHarness"/> with a wrapped repository and a hosting stub whose
/// repository hosting maps <c>origin</c> to the pull request's base.
/// </summary>
public sealed class PullRequestFetchHeadTests
{
    private static readonly HostedRepository Base = new(HostingProvider.GitHub, "github.com", "octo", "ops");
    private static readonly HostedRepository Fork = new(HostingProvider.GitHub, "github.com", "ada", "ops");

    [Fact]
    public async Task A_rewritten_fetch_head_fails_the_fetch_and_creates_no_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        var stub = new HostingSessionStub(new Uri("https://github.com/"), "octo", token: null);
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, wrap: RewrittenFetchHead.Wrap, hosting: stub);
        var head = await repo.HeadAsync();
        var branch = await repo.CurrentBranchAsync();
        var refsBefore = await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)");
        var pullRequest = new PullRequest(
            5,
            "Fix a typo",
            "ada",
            IsDraft: false,
            new Uri("https://github.com/octo/ops/pull/5"),
            "fix-typo",
            new string('f', 40),
            Fork,
            "main",
            Base,
            DateTimeOffset.UnixEpoch);
        var hosting = Assert.Single(stub.Hostings);
        hosting.State = new RepositoryHostingState(
            PullRequestsState.Loaded,
            new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = Base },
            Base,
            new PullRequestList([pullRequest], MoreExist: false),
            null,
            new Dictionary<string, PullRequest>(StringComparer.Ordinal));
        Assert.Equal(PullRequestsState.Loaded, app.Repository.HostingState.PullRequests);

        await app.Operations.CheckOutPullRequestAsync(pullRequest);
        await app.WaitForIdleAsync();

        Assert.Equal(OperationOutcome.Failed, app.LastResult?.Outcome);
        Assert.Equal("Another fetch changed FETCH_HEAD; try again.", app.LastResult?.Failure?.Summary);
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Another fetch changed FETCH_HEAD; try again.", error.Summary);
        Assert.Equal(1, RewrittenFetchHead.Calls);
        Assert.Equal(refsBefore.StandardOutput, (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(branch, await repo.CurrentBranchAsync());
    }

    /// <summary>A repository whose <see cref="IGitRepository.FetchToFetchHeadAsync"/> finds another fetch's line: null, without a fetch.</summary>
    public class RewrittenFetchHead : DispatchProxy
    {
        private static int _calls;
        private IGitRepository _inner = null!;

        public static int Calls => Volatile.Read(ref _calls);

        public static IGitRepository Wrap(IGitRepository inner)
        {
            Volatile.Write(ref _calls, 0);
            var proxy = Create<IGitRepository, RewrittenFetchHead>();
            ((RewrittenFetchHead)(object)proxy)._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IGitRepository.FetchToFetchHeadAsync))
            {
                Interlocked.Increment(ref _calls);
                return Task.FromResult<string?>(null);
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
