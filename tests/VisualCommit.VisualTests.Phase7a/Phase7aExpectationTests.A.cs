using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Hosting;
using VisualCommit.Hosting.Tokens;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part A's expectations against the App (docs/test-reports/phase-7a.md, part A; design 8.7): the
/// texts of <c>AccountTexts</c> and <c>HostingTexts</c> and the link's address equal
/// <see cref="PartA"/>'s, which were written from the report; the picture names are unique and of
/// design 0.3's form; part A's tokens are what the conventions say.
/// </summary>
public sealed partial class Phase7aExpectationTests
{
    [Fact]
    public void Part_A_s_section_texts_are_the_app_s()
    {
        Assert.Equal(PartA.AccountsButton, HostingTexts.AccountsSection);
        Assert.Equal(PartA.GitHubLabel, HostingTexts.GitHub);
        Assert.Equal(PartA.GitLabel, Texts.GitLabel);
        Assert.Equal(PartA.Intro, Texts.AccountsIntro);
        Assert.Equal(PartA.Off, Texts.AccountsOff);
        Assert.Equal(PartA.SignedOut, Texts.AccountsSignedOut);
        Assert.Equal(PartA.SignInButton, Texts.AccountsSignInButton);
        Assert.Equal(PartA.SignOutButton, Texts.AccountsSignOutButton);
        Assert.Equal(PartA.Busy, Texts.AccountsBusy);
        Assert.Equal(PartA.InKeychain, Texts.TokenInKeychain);
        Assert.Equal(PartA.NoKeychain, Texts.NoKeychain);
        Assert.Equal(PartA.SaveFailedLocked, Texts.SaveFailed(TokenStoreReasons.Locked));
        Assert.Equal(PartA.Refused, Texts.NeedsSignInRefused);
        Assert.Equal(PartA.TokenMissing, Texts.NeedsSignInTokenMissing);
        Assert.Equal(PartA.ReadFailedLocked, Texts.NeedsSignInReadFailed(TokenStoreReasons.Locked));
        Assert.Equal(PartA.SessionEnded, Texts.NeedsSignInSessionEnded);
        Assert.Equal(PartA.SignOutNoteLocked, Texts.AccountsSignOutNote(TokenStoreReasons.Locked));
        Assert.Equal(PartA.UseForGit, Texts.AccountUseForGit);
        Assert.Equal(PartA.UseForGitNote, Texts.AccountUseForGitNote);
        Assert.Equal(PartA.UseForGitOldGit230, Texts.AccountUseForGitOldGit("2.30.0"));
        Assert.Equal(PartA.SignOutTitle, Texts.SignOutTitle);
        Assert.Equal(PartA.SignOutQuestionOcto, Texts.SignOutQuestion(OctoLogin, gitUsesTheAccount: true));
        Assert.Equal(PartA.SignOutConfirm, Texts.SignOutConfirm);
        Assert.Equal(PartA.Locked, TokenStoreReasons.Locked);
        Assert.Equal(PartA.OpenSettingsButton, OperationTexts.OpenSettingsButton);
        Assert.Equal(PartA.GitRefusedOcto, OperationTexts.GitCredentialsRefused(OctoLogin));
    }

    [Fact]
    public void Part_A_s_dialog_texts_and_link_are_the_app_s()
    {
        Assert.Equal(PartA.DialogTitle, HostingTexts.AddAccountTitle);
        Assert.Equal(PartA.Help, Texts.SignInHelp);
        Assert.Equal(PartA.TokenLink, Texts.SignInTokenLink);
        Assert.Equal(PartA.TokenLinkTip, Texts.SignInTokenLinkTip);
        Assert.Equal(PartA.TokenPage, Texts.TokenPage);
        Assert.Equal(
            "http://127.0.0.1:47395/settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write",
            new Uri(new Uri(WebBase()), Texts.TokenPage).AbsoluteUri);
        Assert.Equal(PartA.TokenLabel, Texts.TokenLabel);
        Assert.Equal(PartA.Checking, Texts.SignInChecking);
        Assert.Equal(PartA.NotAToken, Texts.SignInNotAToken);
        Assert.Equal(PartA.BadCredentials, Texts.SignInFailed(new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials")));
        Assert.Equal(PartA.SaveFailedDialogOcto, Texts.SignInSaveFailed(OctoLogin, TokenStoreReasons.Locked));
        Assert.Equal(PartA.Cancel, Texts.Cancel);
        Assert.Equal(PartA.SignIn, Texts.SignIn);
        Assert.Equal(PartA.Close, Texts.Close);
        Assert.Equal(PartA.Mask, PromptDialogViewModel.MaskCharacter);
    }

    [Fact]
    public void Part_A_s_pictures_are_unique_and_of_the_report_s_form_and_its_tokens_its_own()
    {
        var pattern = new Regex(@"^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        Assert.Equal(24, PartA.Pictures.Count);
        Assert.Equal(PartA.Pictures.Count, PartA.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartA.Pictures, name => Assert.Matches(pattern, name));
        Assert.All(PartA.Pictures, name => Assert.StartsWith("a", name, StringComparison.Ordinal));
        Assert.All(PartA.RealWindowPictures, name => Assert.Contains(name, PartA.Pictures));

        Assert.StartsWith("github_pat_FAKE-", PartA.UnknownToken, StringComparison.Ordinal);
        Assert.Equal(OctoToken, PartA.TokenWithSpace.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Single(PartA.TokenWithSpace, character => character == ' ');
        Assert.Equal(5, PartA.Tokens.Select(token => token.Token).Distinct(StringComparer.Ordinal).Count());
    }
}
