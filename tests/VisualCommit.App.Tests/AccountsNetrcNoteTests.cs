using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The Accounts section's netrc note (docs/phase-8a-design.md 5.3; ruling 8, D424): it shows while an
/// account is signed in with "Use for git over HTTPS" saved on, the git found is below 2.46, and the
/// home folder git is given holds a <c>.netrc</c> or <c>_netrc</c> file, asked off the UI thread
/// as one of the screen's reads; Dismiss hides it for the session and writes nothing.
/// </summary>
public class AccountsNetrcNoteTests
{
    private static readonly GitVersion Older = new(2, 43, 0);

    [Fact]
    public async Task The_note_shows_with_an_account_used_for_git_an_older_git_and_a_netrc_file()
    {
        var asked = 0;
        var section = Section(Older, () =>
        {
            asked++;
            return true;
        });
        Assert.False(section.ShowsNetrcNote);

        await section.ReadNetrcAsync();

        Assert.True(section.ShowsNetrcNote);
        Assert.Equal(1, asked);
        Assert.Equal(
            "Your home folder has a .netrc or _netrc file. With Git 2.43.0, an entry in that file for a host can answer git's requests to it before your signed-in account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every version of Git.",
            section.NetrcNote);
    }

    public static TheoryData<string> WithoutTheNote => ["no file", "git 2.46", "git 2.47", "no git", "not used for git", "needs sign-in", "signed out", "no question"];

    [Theory]
    [MemberData(nameof(WithoutTheNote))]
    public async Task Without_any_one_of_its_conditions_there_is_no_note(string missing)
    {
        var stub = Stub();
        var version = missing switch
        {
            "git 2.46" => new GitVersion(2, 46, 0),
            "git 2.47" => new GitVersion(2, 47, 1),
            "no git" => (GitVersion?)null,
            _ => Older,
        };
        switch (missing)
        {
            case "not used for git":
                stub.Account = stub.Account! with { Account = stub.Account.Account with { UseForGit = false } };
                break;
            case "needs sign-in":
                stub.Account = stub.Account! with { State = AccountState.NeedsSignIn };
                break;
            case "signed out":
                stub.Account = null;
                break;
        }

        var section = new AccountsSettingsViewModel(
            stub,
            null,
            new DialogHostViewModel(),
            new RecordingLog(),
            version,
            missing == "no question" ? null : () => missing != "no file");
        await section.ReadNetrcAsync();

        Assert.False(section.ShowsNetrcNote);
    }

    [Fact]
    public async Task Dismiss_hides_the_note_and_tells_the_window()
    {
        var told = 0;
        var section = Section(Older, () => true, () => told++);
        await section.ReadNetrcAsync();
        var changed = new List<string?>();
        section.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        section.DismissNetrcNoteCommand.Execute(null);

        Assert.False(section.ShowsNetrcNote);
        Assert.Equal(1, told);
        Assert.Contains(nameof(AccountsSettingsViewModel.ShowsNetrcNote), changed);
    }

    [Fact]
    public async Task A_question_that_fails_shows_no_note_and_is_logged()
    {
        var log = new RecordingLog();
        var section = new AccountsSettingsViewModel(Stub(), null, new DialogHostViewModel(), log, Older, () => throw new IOException("The home share does not answer."));

        await section.ReadNetrcAsync();

        Assert.False(section.ShowsNetrcNote);
        Assert.Contains(log.Entries, entry => entry.Message == "Whether the home folder holds a netrc file could not be read." && entry.Exception is IOException);
    }

    private static HostingSessionStub Stub() => new(new Uri("https://github.example.com/"), "octo", "github_pat_FAKE-octo-1");

    private static AccountsSettingsViewModel Section(GitVersion? version, Func<bool>? homeHasNetrc, Action? dismissed = null) =>
        new(Stub(), null, new DialogHostViewModel(), new RecordingLog(), version, homeHasNetrc, dismissed);
}
