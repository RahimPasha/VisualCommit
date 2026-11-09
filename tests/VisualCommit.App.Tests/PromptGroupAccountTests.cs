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
/// the prompt's account and text, and <see cref="PromptGroup.ForgetAnswers"/> drops them. Since 7b
/// (docs/phase-7b-design.md 8.3): on dev.azure.com an organization's prompts are kept under its
/// account and a path-less one's under none.
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

        // a2 is not octocat: the broker gives git's password prompt for a2 no account.
        var password = Password(group, "op-3", "a2", null);
        Assert.Equal(PromptAnswer.Of("pw2"), await dialogs.AnswerAsync(password, token));
        Assert.Equal(2, presenter.Shown);

        group.ForgetAnswers();
        Assert.Equal(PromptAnswer.Of("pw3"), await dialogs.AnswerAsync(password with { ScopeId = "op-4" }, token));
        Assert.Equal(3, presenter.Shown);
    }

    [Theory]
    [InlineData("octocat", true)]
    [InlineData("OctoCat", true)]
    [InlineData(AskPassProtocol.CredentialUserName, true)]
    [InlineData("alice", false)]
    public async Task The_password_typed_with_a_user_name_is_kept_under_the_account_its_prompt_will_carry(string user, bool accounts)
    {
        using var group = new PromptGroup();
        var presenter = new CountingPresenter(user: user);
        var dialogs = new PromptDialogs(presenter);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(PromptAnswer.Of(user), await dialogs.AnswerAsync(Username(group, "op-1", Octocat), token));

        // What the broker sends next (AskPassBroker.IsAccountUser): the account for its own user, none for another.
        var carried = accounts ? Octocat : null;
        Assert.Equal(PromptAnswer.Of("pw1"), await dialogs.AnswerAsync(Password(group, "op-1", user, carried), token));
        Assert.Equal(1, presenter.Shown);

        // The same text under the other key is another answer.
        Assert.Equal(PromptAnswer.Of("pw2"), await dialogs.AnswerAsync(Password(group, "op-2", user, accounts ? null : Octocat), token));
        Assert.Equal(2, presenter.Shown);
    }

    [Fact]
    public async Task An_organizations_prompts_are_kept_under_its_account_and_a_path_less_ones_under_none()
    {
        // What the broker sends on dev.azure.com (docs/phase-7b-design.md 8.3): a user name prompt
        // whose path names the organization carries its account, a path-less one none.
        using var group = new PromptGroup();
        var presenter = new CountingPresenter();
        var dialogs = new PromptDialogs(presenter);
        var token = TestContext.Current.CancellationToken;
        const string Named = "Username for 'https://dev.azure.com/myorg/p/_git/r': ";
        const string PathLess = "Username for 'https://dev.azure.com': ";

        Assert.Equal(PromptAnswer.Of("a1"), await dialogs.AnswerAsync(AzureUsername(group, "op-1", Named, MyOrg), token));
        Assert.Equal(PromptAnswer.Of("a1"), await dialogs.AnswerAsync(AzureUsername(group, "op-2", Named, MyOrg), token));
        Assert.Equal(PromptAnswer.Of("a2"), await dialogs.AnswerAsync(AzureUsername(group, "op-3", Named, OtherOrg), token));
        Assert.Equal(PromptAnswer.Of("a3"), await dialogs.AnswerAsync(AzureUsername(group, "op-4", PathLess, null), token));
        Assert.Equal(PromptAnswer.Of("a3"), await dialogs.AnswerAsync(AzureUsername(group, "op-5", PathLess, null), token));

        Assert.Equal(3, presenter.Shown);
    }

    [Fact]
    public async Task The_organizations_password_after_a_named_user_name_prompt_is_asked_once_and_after_a_path_less_one_once_more()
    {
        using var group = new PromptGroup();
        var presenter = new CountingPresenter(user: "myorg");
        var dialogs = new PromptDialogs(presenter);
        var token = TestContext.Current.CancellationToken;

        // With useHttpPath on: both prompts name the organization and carry the account.
        Assert.Equal(PromptAnswer.Of("myorg"), await dialogs.AnswerAsync(AzureUsername(group, "op-1", "Username for 'https://dev.azure.com/myorg/p/_git/r': ", MyOrg), token));
        Assert.Equal(PromptAnswer.Of("pw1"), await dialogs.AnswerAsync(AzurePassword(group, "op-1", "Password for 'https://myorg@dev.azure.com/myorg/p/_git/r': ", MyOrg), token));
        Assert.Equal(1, presenter.Shown);

        // The accepted case (8.3): the path-less user name prompt carries none, its password
        // prompt the account, so that password is asked once more.
        Assert.Equal(PromptAnswer.Of("myorg"), await dialogs.AnswerAsync(AzureUsername(group, "op-2", "Username for 'https://dev.azure.com': ", null), token));
        Assert.Equal(PromptAnswer.Of("pw3"), await dialogs.AnswerAsync(AzurePassword(group, "op-2", "Password for 'https://myorg@dev.azure.com': ", MyOrg), token));
        Assert.Equal(3, presenter.Shown);
    }

    private static readonly AccountKey MyOrg = new(HostingProvider.AzureDevOps, "dev.azure.com", "myorg");
    private static readonly AccountKey OtherOrg = new(HostingProvider.AzureDevOps, "dev.azure.com", "otherorg");

    private static GitPrompt AzureUsername(PromptGroup group, string scope, string text, AccountKey? account) =>
        new(GitPromptKind.Username, text, scope, "Fetch", "repo", Url: text["Username for '".Length..text.LastIndexOf('\'')], Group: group, Account: account);

    private static GitPrompt AzurePassword(PromptGroup group, string scope, string text, AccountKey? account) =>
        new(GitPromptKind.Password, text, scope, "Fetch", "repo", Group: group, Account: account);

    private static GitPrompt Password(PromptGroup group, string scope, string user, AccountKey? account) =>
        new(GitPromptKind.Password, $"Password for 'https://{user}@github.com': ", scope, "Fetch", "repo", Group: group, Account: account);

    private static GitPrompt Username(PromptGroup group, string scope, AccountKey? account) =>
        new(GitPromptKind.Username, "Username for 'https://github.com': ", scope, "Fetch", "repo", Url: "https://github.com", Group: group, Account: account);

    /// <summary>
    /// Answers the n-th dialog with <c>a&lt;n&gt;</c> (or <paramref name="user"/>) and the password
    /// <c>pw&lt;n&gt;</c>, the first with Cancel when asked to.
    /// </summary>
    private sealed class CountingPresenter(bool cancelFirst = false, string? user = null) : IPromptDialogPresenter
    {
        private int _shown;

        public int Shown => Volatile.Read(ref _shown);

        public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _shown);
            return Task.FromResult(cancelFirst && n == 1
                ? new PromptDialogAnswer(null, null, Cancelled: true)
                : new PromptDialogAnswer(prompt.Kind == GitPromptKind.Password ? $"pw{n}" : user ?? $"a{n}", $"pw{n}"));
        }
    }
}
