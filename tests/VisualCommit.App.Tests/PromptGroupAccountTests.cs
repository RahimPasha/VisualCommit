using System.Net;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.AskPass;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The prompt group per account (ruling 9, docs/phase-7a-design.md 4.7): kept answers are keyed by
/// the prompt's account and text, and <see cref="PromptGroup.ForgetAnswers"/> drops them.
/// </summary>
public class PromptGroupAccountTests
{
    private static readonly AccountKey Octocat = new(HostingProvider.GitHub, "github.com", "octocat");
    private static readonly AccountKey Hubot = new(HostingProvider.GitHub, "github.com", "hubot");

    [Fact]
    public async Task One_text_under_two_accounts_asks_twice_and_under_one_once()
    {
        using var group = new PromptGroup();
        var presenter = new CountingPresenter();
        var dialogs = new PromptDialogs(presenter);

        Assert.Equal(PromptAnswer.Of("a1"), await dialogs.AnswerAsync(Username(group, "op-1", Octocat), TestContext.Current.CancellationToken));
        Assert.Equal(PromptAnswer.Of("a1"), await dialogs.AnswerAsync(Username(group, "op-2", Octocat), TestContext.Current.CancellationToken));
        Assert.Equal(PromptAnswer.Of("a2"), await dialogs.AnswerAsync(Username(group, "op-3", Hubot), TestContext.Current.CancellationToken));
        Assert.Equal(PromptAnswer.Of("a3"), await dialogs.AnswerAsync(Username(group, "op-4", null), TestContext.Current.CancellationToken));

        Assert.Equal(3, presenter.Shown);
    }

    [Fact]
    public async Task Forgetting_the_answers_asks_again_also_after_a_kept_cancel_and_for_a_kept_password()
    {
        using var group = new PromptGroup();
        var presenter = new CountingPresenter(cancelFirst: true);
        var dialogs = new PromptDialogs(presenter);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(PromptAnswer.Cancelled, await dialogs.AnswerAsync(Username(group, "op-1", Octocat), token));
        Assert.Equal(PromptAnswer.Cancelled, await dialogs.AnswerAsync(Username(group, "op-2", Octocat), token));
        group.ForgetAnswers();

        Assert.Equal(PromptAnswer.Of("a2"), await dialogs.AnswerAsync(Username(group, "op-3", Octocat), token));
        var password = new GitPrompt(GitPromptKind.Password, "Password for 'https://a2@github.com': ", "op-3", "Fetch", "repo", Group: group, Account: Octocat);
        Assert.Equal(PromptAnswer.Of("pw2"), await dialogs.AnswerAsync(password, token));
        Assert.Equal(2, presenter.Shown);

        group.ForgetAnswers();
        Assert.Equal(PromptAnswer.Of("pw3"), await dialogs.AnswerAsync(password with { ScopeId = "op-4" }, token));
        Assert.Equal(3, presenter.Shown);
    }

    private static GitPrompt Username(PromptGroup group, string scope, AccountKey? account) =>
        new(GitPromptKind.Username, "Username for 'https://github.com': ", scope, "Fetch", "repo", Url: "https://github.com", Group: group, Account: account);

    /// <summary>Answers the n-th dialog with <c>a&lt;n&gt;</c> and the password <c>pw&lt;n&gt;</c>, the first with Cancel when asked to.</summary>
    private sealed class CountingPresenter(bool cancelFirst = false) : IPromptDialogPresenter
    {
        private int _shown;

        public int Shown => Volatile.Read(ref _shown);

        public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _shown);
            return Task.FromResult(cancelFirst && n == 1
                ? new PromptDialogAnswer(null, null, Cancelled: true)
                : new PromptDialogAnswer(prompt.Kind == GitPromptKind.Password ? $"pw{n}" : $"a{n}", $"pw{n}"));
        }
    }
}
