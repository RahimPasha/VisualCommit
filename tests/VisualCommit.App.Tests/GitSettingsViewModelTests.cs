using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using Xunit;
using G = VisualCommit.Core.Git.GitConfigScope;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen's Git section without a window (docs/phase-6-design.md 7.4, section 8,
/// 8.13's App tests), over a fake <see cref="IGitConfiguration"/> and repository: each scope's
/// fields from its own read, an <c>includeIf</c> seen from both reads, the placeholders, dirty
/// tracking, 8.4's notices and the "Still" one after a save, the identity's rules and texts, the
/// pull modes with Other, <c>branch.&lt;b&gt;.rebase</c> and its mark, and the merge tools' list.
/// </summary>
public class GitSettingsViewModelTests
{
    internal static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "settings-fake"));
    internal static readonly string GlobalFile = Path.Combine(Root, "data", "harness", "global.gitconfig");
    internal static readonly string XdgFile = Path.Combine(Root, "data", "harness", "xdg", "git", "config");
    internal static readonly string WorkTree = Path.Combine(Root, "web-app");
    internal static readonly string LocalFile = Path.Combine(WorkTree, ".git", "config");
    internal static readonly string Included = Path.Combine(Root, "work.gitconfig");
    internal static readonly string SystemFile = Path.Combine(Root, "etc", "gitconfig");

    [Fact]
    public async Task Each_scopes_fields_come_from_its_own_read()
    {
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("user.name", "Ann Example"), Global("user.email", "ann@example.com"), Global("merge.tool", "meld")),
            InRepository = Read(
                true,
                Global("user.name", "Ann Example"),
                Global("user.email", "ann@example.com"),
                Global("merge.tool", "meld"),
                Local("user.name", "Bo Local"),
                Local("pull.rebase", "true"),
                Local("pull.ff", "true")),
        };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.True(section.ShowsFields);
        Assert.Equal(("Ann Example", "ann@example.com", "meld", PullMode.NotSet), (section.Global.UserName, section.Global.UserEmail, section.Global.MergeToolValue, section.Global.SelectedPullMode));
        Assert.Equal(("Bo Local", string.Empty, (string?)null, PullMode.Rebase), (section.Repository.UserName, section.Repository.UserEmail, section.Repository.MergeToolValue, section.Repository.SelectedPullMode));
        Assert.Equal("Same as global: ann@example.com", section.Repository.EmailPlaceholder);
        Assert.Equal("Same as global (meld)", section.Repository.MergeToolText);
        Assert.Equal(["web-app sets its own: Bo Local."], Texts(section.Global.NameNotices));
        Assert.Equal(["web-app sets its own: Rebase."], Texts(section.Global.PullNotices));
        Assert.Empty(section.GlobalWrites());
        Assert.Empty(section.RepositoryWrites());

        // The section opens on the global scope, also on a repository's tab (D200).
        Assert.False(section.IsRepositoryScope);
        Assert.True(section.CanUseRepositoryScope);
        Assert.Equal($"web-app — {WorkTree}", section.RepositoryScopeToolTip);
        Assert.Equal("Saved in your global git configuration, which git in a terminal uses too.", section.Caption);
        Assert.Equal(GlobalFile, section.CaptionToolTip);
        section.ShowRepositoryCommand.Execute(null);
        Assert.Equal("Saved in this repository's .git/config, which all its worktrees share. These values win over those for all repositories.", section.Caption);
        Assert.Null(section.CaptionToolTip);
    }

    [Fact]
    public async Task Without_a_repository_this_repository_is_disabled_with_its_reason_and_only_the_outside_read_runs()
    {
        var git = new FakeGitConfiguration { Outside = Read(false) };
        var section = await LoadAsync(git, repository: null);

        Assert.False(section.CanUseRepositoryScope);
        Assert.False(section.ShowRepositoryCommand.CanExecute(null));
        Assert.Equal("Open a repository's tab to change its own settings", section.RepositoryScopeToolTip);
        Assert.Equal([null], git.Reads);
        Assert.Empty(git.IdentityReads);
        Assert.Equal(("Not set", "Not set"), (section.Global.NamePlaceholder, section.Global.EmailPlaceholder));
    }

    [Fact]
    public async Task An_includeIf_value_shows_in_the_global_scope_as_the_global_files_with_a_notice_and_a_global_save_is_judged_by_the_outside_read()
    {
        // 0.3's includeIf shape: the outside read leaves it out, the repository read ends with it.
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("user.email", "ann@example.com")),
            InRepository = Read(true, Global("user.email", "ann@example.com"), Global("user.email", "ann@work.example", Included)),
        };
        var repository = new FakeSettingsRepository();
        var section = await LoadAsync(git, repository);

        Assert.Equal("ann@example.com", section.Global.UserEmail);
        Assert.Equal(
            [new GitNotice("In web-app, git uses ann@work.example from work.gitconfig (included).", Included)],
            section.Global.EmailNotices);

        // An edit of the repository scope alone writes no --global.
        section.Repository.UserName = "Bo Local";
        Assert.Empty(section.GlobalWrites());
        Assert.Equal([new GitConfigWrite("user.name", "Bo Local")], section.RepositoryWrites());
        section.Repository.UserName = string.Empty;

        // A global save of the email: the outside read has what was written, so nothing holds the
        // screen open, whatever the includeIf still makes the repository use.
        section.Global.UserEmail = "ann@new.example";
        var writes = section.GlobalWrites();
        Assert.Equal([new GitConfigWrite("user.email", "ann@new.example")], writes);
        await section.WriteGlobalAsync(writes);
        git.Outside = Read(false, Global("user.email", "ann@new.example"));
        git.InRepository = Read(true, Global("user.email", "ann@new.example"), Global("user.email", "ann@work.example", Included));

        Assert.False(await section.ReadBackAsync(writes, []));
        Assert.Equal([writes], git.GlobalWrites);
        Assert.Equal("ann@new.example", section.Global.UserEmail);
        Assert.Empty(section.GlobalWrites());
    }

    [Fact]
    public async Task Placeholders_come_from_the_system_scope_globally_and_from_below_in_the_repository()
    {
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, System("user.name", "Machine Name"), System("pull.rebase", "true")),
            InRepository = Read(true, System("user.name", "Machine Name"), System("pull.rebase", "true"), Global("user.email", "ann@example.com")),
        };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.Equal(("From git's system configuration: Machine Name", "Not set"), (section.Global.NamePlaceholder, section.Global.EmailPlaceholder));
        Assert.Equal([new GitNotice("pull.rebase = true also applies, from git's system configuration.", SystemFile)], section.Global.PullNotices);
        Assert.Equal(("Same as global: Machine Name", "Same as global: ann@example.com"), (section.Repository.NamePlaceholder, section.Repository.EmailPlaceholder));
    }

    [Fact]
    public async Task An_untouched_field_writes_nothing_and_each_change_writes_its_key_in_8_3s_order()
    {
        var git = new FakeGitConfiguration { Outside = Read(false, Global("user.name", "Ann"), Global("pull.ff", "only")), InRepository = Read(true) };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.Empty(section.GlobalWrites());
        section.Global.UserName = "  Ann  ";
        Assert.Empty(section.GlobalWrites());

        section.Global.MergeToolValue = "meld";
        section.Global.UserEmail = " ann@example.com ";
        section.Global.IsRebase = true;
        section.Global.UserName = "   ";
        Assert.Equal(
            [
                new GitConfigWrite("user.name", null),
                new GitConfigWrite("user.email", "ann@example.com"),
                new GitConfigWrite("pull.rebase", "true"),
                new GitConfigWrite("pull.ff", "true"),
                new GitConfigWrite("merge.tool", "meld"),
            ],
            section.GlobalWrites());

        // Picking the mode read again writes no pull key.
        section.Global.IsFastForwardOnly = true;
        Assert.DoesNotContain(section.GlobalWrites(), write => write.Key.StartsWith("pull.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Picking_a_mode_writes_both_keys_and_same_as_global_unsets_both()
    {
        var git = new FakeGitConfiguration { Outside = Read(false), InRepository = Read(true, Local("pull.rebase", "true"), Local("pull.ff", "true")) };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.True(section.Repository.IsRebase);
        section.Repository.IsNotSet = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", null), new GitConfigWrite("pull.ff", null)], section.RepositoryWrites());
        section.Repository.IsMergeCommit = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", "false"), new GitConfigWrite("pull.ff", "false")], section.RepositoryWrites());
        section.Repository.IsFastForwardOnly = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", null), new GitConfigWrite("pull.ff", "only")], section.RepositoryWrites());
        section.Repository.IsRebaseMerges = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", "merges"), new GitConfigWrite("pull.ff", "true")], section.RepositoryWrites());
        section.Repository.IsMerge = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", "false"), new GitConfigWrite("pull.ff", "true")], section.RepositoryWrites());
        Assert.Equal("Same as global", section.Repository.NotSetLabel);
        Assert.Equal("Uses the pull mode for all repositories", section.Repository.NotSetToolTip);
        Assert.Equal(("Not set", "Nothing set: the app merges, and git in a terminal stops when the branches have diverged"), (section.Global.NotSetLabel, section.Global.NotSetToolTip));
    }

    [Theory]
    [InlineData("preserve", null, "Other: pull.rebase = preserve", "Pulls fail while this value is set.")]
    [InlineData("interactive", null, "Other: pull.rebase = interactive", "Git opens an editor for the list; the app rebases without one.")]
    [InlineData("m", null, "Other: pull.rebase = m", "Git 2.36 merges with this value and newer gits rebase keeping merge commits; the app does what its git does.")]
    [InlineData("k", "only", "Other: pull.rebase = k", "Git 2.36 reads this value as false and newer gits refuse it; the app does what its git does.")]
    [InlineData("preserve", "sometimes", "Other: pull.rebase = preserve, pull.ff = sometimes", "Pulls fail while this value is set.")]
    public async Task An_unusual_value_shows_as_other_checked_and_is_never_rewritten_unless_a_mode_is_picked(string rebase, string? fastForward, string other, string note)
    {
        var values = new List<GitConfigValue> { Local("pull.rebase", rebase) };
        if (fastForward is not null)
        {
            values.Add(Local("pull.ff", fastForward));
        }

        var git = new FakeGitConfiguration { Outside = Read(false), InRepository = Read(true, [.. values]) };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.True(section.Repository.ShowsOther);
        Assert.True(section.Repository.IsOther);
        Assert.Null(section.Repository.SelectedPullMode);
        Assert.Equal((other, note), (section.Repository.OtherText, section.Repository.OtherNote));
        Assert.Empty(section.RepositoryWrites());
        Assert.False(section.Global.ShowsOther);

        section.Repository.IsRebase = true;
        Assert.Equal([new GitConfigWrite("pull.rebase", "true"), new GitConfigWrite("pull.ff", "true")], section.RepositoryWrites());
        section.Repository.IsOther = true;
        Assert.Empty(section.RepositoryWrites());
        Assert.Equal($"web-app sets its own: {other}.", Assert.Single(section.Global.PullNotices).Text);
    }

    [Fact]
    public async Task Each_notice_of_8_4_names_its_file_by_what_it_is_with_the_full_path_as_its_tooltip()
    {
        var worktreeFile = Path.Combine(WorkTree, ".git", "config.worktree");
        var localInclude = Path.Combine(WorkTree, ".git", "team.gitconfig");
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("user.name", "Xdg Name", XdgFile), Global("merge.tool", "kdiff3", Included), Command("user.email", "env@example.com")),
            InRepository = Read(
                true,
                Global("user.name", "Xdg Name", XdgFile),
                Global("merge.tool", "kdiff3", Included),
                Local("user.email", "team@example.com", localInclude),
                new GitConfigValue(G.Worktree, worktreeFile, "user.name", "Tree Name"),
                Command("user.email", "env@example.com")),
        };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.Equal([new GitNotice("Git uses Xdg Name from .config/git/config.", XdgFile)], section.Global.NameNotices);
        Assert.Equal(
            [new GitNotice("env@example.com comes from the environment the app was started with and wins over every file."), new GitNotice("web-app sets its own: team@example.com.")],
            section.Global.EmailNotices);
        Assert.Equal([new GitNotice("Git uses kdiff3 from work.gitconfig (included).", Included)], section.Global.MergeToolNotices);
        Assert.Equal([new GitNotice("This worktree has its own value, Tree Name, in config.worktree; the app does not change it.", worktreeFile)], section.Repository.NameNotices);
        Assert.Equal(
            [
                new GitNotice("Git uses team@example.com from team.gitconfig (included by .git/config).", localInclude),
                new GitNotice("env@example.com comes from the environment the app was started with and wins over every file."),
            ],
            section.Repository.EmailNotices);

        // With XDG_CONFIG_HOME set, the XDG file is named after it.
        git.Variables["XDG_CONFIG_HOME"] = Path.Combine(Root, "data", "harness", "xdg");
        var named = await LoadAsync(git, new FakeSettingsRepository());
        Assert.Equal("Git uses Xdg Name from git/config under XDG_CONFIG_HOME.", Assert.Single(named.Global.NameNotices).Text);
    }

    [Fact]
    public async Task A_save_that_an_included_file_still_beats_says_so_and_holds_the_screen_open()
    {
        // 8.4: "Still pull.ff = only in work.gitconfig (included), which git reads later."
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("pull.ff", "only"), Global("pull.ff", "only", Included)),
            InRepository = Read(true, Global("pull.ff", "only"), Global("pull.ff", "only", Included)),
        };
        var section = await LoadAsync(git, new FakeSettingsRepository());
        Assert.True(section.Global.IsFastForwardOnly);
        Assert.Equal([new GitNotice("Git uses pull.ff = only from work.gitconfig (included).", Included)], section.Global.PullNotices);

        section.Global.IsRebase = true;
        var writes = section.GlobalWrites();
        Assert.Equal([new GitConfigWrite("pull.rebase", "true"), new GitConfigWrite("pull.ff", "true")], writes);
        git.Outside = Read(false, Global("pull.rebase", "true"), Global("pull.ff", "true"), Global("pull.ff", "only", Included));

        Assert.True(await section.ReadBackAsync(writes, []));
        Assert.Equal(new GitNotice("Still pull.ff = only in work.gitconfig (included), which git reads later.", Included, IsWarning: true), section.Global.PullNotices[0]);
        Assert.True(section.Global.HasStillNotice);
        Assert.False(section.IsRepositoryScope);

        // An unset that a file of the scope the app does not write still sets.
        section.Global.MergeToolValue = null;
        git.Outside = Read(false, Global("merge.tool", "meld", XdgFile));
        Assert.True(await section.ReadBackAsync([new GitConfigWrite("merge.tool", null)], []));
        Assert.Equal(new GitNotice("Still set to meld in .config/git/config: the app cannot change that file.", XdgFile, IsWarning: true), section.Global.MergeToolNotices[0]);
    }

    [Fact]
    public async Task A_repository_key_that_same_as_global_unsets_is_done_though_the_global_one_still_decides()
    {
        // SD30: the comparison is within the scope written (S19 step 7).
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("pull.ff", "only")),
            InRepository = Read(true, Global("pull.ff", "only"), Local("pull.rebase", "true"), Local("pull.ff", "true")),
        };
        var section = await LoadAsync(git, new FakeSettingsRepository());
        section.Repository.IsNotSet = true;
        var writes = section.RepositoryWrites();
        git.InRepository = Read(true, Global("pull.ff", "only"));

        Assert.False(await section.ReadBackAsync([], writes));
        Assert.False(section.Repository.HasStillNotice);
    }

    [Theory]
    [InlineData("Ann <Example>", "A name cannot contain <, > or a line break.")]
    [InlineData("Ann\nExample", "A name cannot contain <, > or a line break.")]
    [InlineData(" . , ; ", "Git refuses a name made only of punctuation.")]
    [InlineData("\"'\\:", "Git refuses a name made only of punctuation.")]
    [InlineData("Ann Example", null)]
    [InlineData("", null)]
    public void The_name_rules_of_8_6(string name, string? error) => Assert.Equal(error, GitScopeViewModel.NameRule(name));

    [Theory]
    [InlineData("ann.example.com", null)]
    [InlineData("<ann@example.com>", "An email address cannot contain <, > or a line break.")]
    [InlineData("ann@\rexample.com", "An email address cannot contain <, > or a line break.")]
    [InlineData(" ann@example.com\r\n", null)]
    [InlineData("", null)]
    public void The_email_rules_of_8_6(string email, string? error) => Assert.Equal(error, GitScopeViewModel.EmailRule(email));

    [Fact]
    public async Task An_error_shows_once_its_box_was_edited_and_a_warning_never_holds_save()
    {
        var git = new FakeGitConfiguration { Outside = Read(false), InRepository = Read(true) };
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.Null(section.Global.NameError);
        section.Global.UserName = "Ann <Example>";
        section.Global.UserEmail = "ann.example.com";
        Assert.Equal("A name cannot contain <, > or a line break.", section.Global.NameError);
        Assert.Null(section.Global.EmailError);
        Assert.Equal("This does not look like an email address.", section.Global.EmailWarning);
        Assert.Equal("UserNameBox", section.Global.FirstErrorField);
        Assert.True(section.ShowErrors());
        Assert.False(section.IsRepositoryScope);

        section.Global.UserName = "Ann Example";
        Assert.False(section.ShowErrors());
        Assert.Equal("This does not look like an email address.", section.Global.EmailWarning);
        Assert.False(section.HasError);

        // An error of the repository scope alone shows that scope.
        section.Repository.UserEmail = "<bo>";
        Assert.True(section.ShowErrors());
        Assert.True(section.IsRepositoryScope);
    }

    [Theory]
    [InlineData("Test Author <author@example.com>", "Test Author <author@example.com>", null, "Commits here are made as Test Author <author@example.com>.")]
    [InlineData("Ann <a@x>", "Ci <c@x>", null, "Commits here are made as Ann <a@x>, committed as Ci <c@x>.")]
    [InlineData(null, null, "fatal: no email was given and auto-detection is disabled", "Git cannot make commits here: no email is set.")]
    [InlineData(null, null, "fatal: unable to auto-detect email address (got 'ann@pc.(none)')", "Git cannot make commits here: no email is set.")]
    [InlineData(null, null, "fatal: no name was given and auto-detection is disabled", "Git cannot make commits here: no name is set.")]
    [InlineData(null, null, "fatal: empty ident name (for <a@x>) not allowed", "Git cannot make commits here: the name is empty.")]
    [InlineData(null, null, "fatal: name consists only of disallowed characters: .", "Git cannot make commits here: it refuses the name.")]
    [InlineData(null, null, "fatal: something new", "Git cannot make commits here: something new")]
    public void The_identity_in_effect_in_the_apps_words(string? author, string? committer, string? refusal, string text) =>
        Assert.Equal(text, GitScopeViewModel.InEffectText(new GitIdentityRead(author, committer, refusal)));

    [Fact]
    public async Task The_repository_scope_has_the_identity_in_effect_and_8_6s_notices_in_order()
    {
        var git = new FakeGitConfiguration
        {
            Outside = Read(false),
            InRepository = Read(true, Global("user.useConfigOnly", "true"), Global("author.name", "Pen Name"), Local("committer.email", "ci@example.com")),
            Identity = new GitIdentityRead(null, null, "fatal: no email was given and auto-detection is disabled"),
        };
        git.Variables["GIT_AUTHOR_EMAIL"] = "a@x";
        git.Variables["GIT_COMMITTER_NAME"] = "Ci";
        git.Variables["EMAIL"] = "e@x";
        var section = await LoadAsync(git, new FakeSettingsRepository());

        Assert.Null(section.Global.IdentityInEffect);
        Assert.Equal("Git cannot make commits here: no email is set.", section.Repository.IdentityInEffect);
        Assert.Equal(
            [
                "GIT_AUTHOR_EMAIL is set in the environment the app was started with, so git uses it for authors.",
                "GIT_COMMITTER_NAME is set in the environment the app was started with, so git uses it for committers.",
                "EMAIL is set in the environment the app was started with, so git uses it when no email is set.",
                "author.name = Pen Name (your global configuration) is used for authors instead.",
                "committer.email = ci@example.com (.git/config) is used for committers instead.",
                "user.useConfigOnly is set, so git does not guess a missing name or email.",
            ],
            Texts(section.Repository.IdentityNotices));
        Assert.Equal([WorkTree], git.IdentityReads);
    }

    [Theory]
    [InlineData(PullFastForwardSetting.Only, PullRebaseSetting.None, "pull.rebase", null, "only", "Pull into main: Fast-forward only (pull.ff = only in your global configuration).")]
    [InlineData(PullFastForwardSetting.Always, PullRebaseSetting.Rebase, "pull.rebase", "true", "true", "Pull into main: Rebase (pull.rebase = true in .git/config).")]
    [InlineData(PullFastForwardSetting.Unset, PullRebaseSetting.Merges, "branch.main.rebase", "merges", null, "Pull into main: Rebase, keeping merge commits (branch.main.rebase = merges in .git/config).")]
    [InlineData(PullFastForwardSetting.Never, PullRebaseSetting.None, "pull.rebase", null, "false", "Pull into main: Merge, always with a merge commit (pull.ff = false in .git/config).")]
    [InlineData(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "false", null, "Pull into main: Merge (fast-forward when possible) (pull.rebase = false in .git/config).")]
    [InlineData(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null, "Pull into main: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.")]
    public async Task The_pull_in_effect_is_pulls_own_reading_with_the_deciding_keys_file(
        PullFastForwardSetting fastForward, PullRebaseSetting rebase, string rebaseKey, string? rebaseValue, string? fastForwardValue, string text)
    {
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("pull.ff", "only")),
            InRepository = Read(
                true,
                [Global("pull.ff", "only"), .. fastForwardValue is null or "only" ? Array.Empty<GitConfigValue>() : [Local("pull.ff", fastForwardValue)], .. rebaseValue is null ? Array.Empty<GitConfigValue>() : [Local(rebaseKey, rebaseValue)]]),
        };
        var repository = new FakeSettingsRepository { Pull = new PullConfiguration(fastForward, rebase, rebaseKey, rebaseValue, fastForwardValue, null, null) };
        var section = await LoadAsync(git, repository);

        Assert.Equal(text, section.Repository.PullInEffect?.Text);
        Assert.Null(section.Global.PullInEffect);
        Assert.Equal(["main"], repository.PullReads);
    }

    [Fact]
    public async Task A_value_pull_refuses_and_a_detached_head_say_so_or_nothing()
    {
        var git = new FakeGitConfiguration { Outside = Read(false), InRepository = Read(true, Local("pull.rebase", "preserve")) };
        var refused = new FakeSettingsRepository
        {
            Pull = new PullConfiguration(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", "preserve", null, "pull.rebase is set to preserve, which this app does not understand.", "pull.rebase"),
        };
        Assert.Equal(
            "Pull into main fails: pull.rebase is set to preserve, which this app does not understand.",
            (await LoadAsync(git, refused)).Repository.PullInEffect?.Text);

        var detached = new FakeSettingsRepository { BranchName = null };
        Assert.Null((await LoadAsync(git, detached)).Repository.PullInEffect);
        Assert.Empty(detached.PullReads);
    }

    [Fact]
    public async Task A_branchs_own_rebase_setting_is_noticed_and_marked_for_removal_last()
    {
        var git = new FakeGitConfiguration { Outside = Read(false), InRepository = Read(true, Local("branch.main.rebase", "false")) };
        var section = await LoadAsync(git, new FakeSettingsRepository());
        var scope = section.Repository;

        Assert.Equal("main has its own pull setting, branch.main.rebase = false, which git uses instead of the mode above.", scope.BranchRebaseNoticeText);
        Assert.True(scope.ShowsBranchRebaseButton);
        Assert.Equal("Use the repository's setting", scope.BranchRebaseButtonText);
        Assert.Empty(section.RepositoryWrites());

        scope.ToggleBranchRebaseCommand.Execute(null);
        Assert.Equal(("Save removes branch.main.rebase, so main pulls as set above.", "Keep main's setting"), (scope.BranchRebaseNoticeText, scope.BranchRebaseButtonText));
        scope.IsMerge = true;
        Assert.Equal(
            [new GitConfigWrite("pull.rebase", "false"), new GitConfigWrite("pull.ff", "true"), new GitConfigWrite("branch.main.rebase", null)],
            section.RepositoryWrites());
        scope.ToggleBranchRebaseCommand.Execute(null);
        Assert.DoesNotContain(section.RepositoryWrites(), write => write.Key == "branch.main.rebase");

        // In another file: named, without a button; a branch name compares exactly.
        git.InRepository = Read(true, Global("branch.main.rebase", "true"), Local("branch.Main.rebase", "false"));
        var global = await LoadAsync(git, new FakeSettingsRepository());
        Assert.Equal("main has its own pull setting, branch.main.rebase = true (your global configuration), which git uses instead of the mode above.", global.Repository.BranchRebaseNoticeText);
        Assert.False(global.Repository.ShowsBranchRebaseButton);
    }

    [Fact]
    public async Task The_merge_tools_list_leaves_out_terminal_tools_adds_ones_own_and_a_path_found_one_and_ends_with_the_current_value()
    {
        var kdiff3 = Path.Combine(Root, "tools", "kdiff3.exe");
        var git = new FakeGitConfiguration
        {
            Outside = Read(false, Global("mergetool.slow.cmd", "true"), Global("mergetool.araxis.path", kdiff3), Global("merge.tool", "vimdiff")),
            InRepository = Read(true, Global("mergetool.slow.cmd", "true"), Global("mergetool.araxis.path", kdiff3), Global("merge.tool", "vimdiff")),
        };
        var help = new MergeToolHelp(
            [new MergeTool("meld", "Use Meld (requires a graphical session)"), new MergeTool("vimdiff", "Use Vim"), new MergeTool("kdiff3", null)],
            [new MergeTool("araxis", null), new MergeTool("bc", null), new MergeTool("emerge", null)],
            Failed: false);
        var pick = new FakePick();
        var section = await LoadAsync(git, new FakeSettingsRepository(), help: help, pick: pick, fileExists: path => path == kdiff3);

        Assert.True(section.Global.ChooseMergeToolCommand.CanExecute(null));
        pick.Answer = "slow";
        await section.Global.ChooseMergeToolCommand.ExecuteAsync(null);

        var dialog = Assert.Single(pick.Shown);
        Assert.Equal(("Choose a merge tool", "Choose", "vimdiff"), (dialog.Title, dialog.ConfirmText, dialog.Selected));
        Assert.Equal(
            [
                new PickOption("Not set", "Merge tool… stays disabled"),
                new PickOption("meld", "Use Meld"),
                new PickOption("kdiff3", "found on this computer"),
                new PickOption("araxis", "found on this computer"),
                new PickOption("slow", "your own tool (mergetool.slow.cmd)"),
                new PickOption("bc", "not found on this computer"),
                new PickOption("vimdiff", "runs in a terminal; the app cannot start it"),
            ],
            dialog.Options);
        Assert.Equal("slow", section.Global.MergeToolValue);
        Assert.Null(section.Global.MergeToolStatus);
        Assert.Equal([new GitConfigWrite("merge.tool", "slow")], section.GlobalWrites());

        // A tool git did not find, chosen: the status says so; one whose path exists does not.
        section.Global.MergeToolValue = "bc";
        Assert.Equal("git did not find bc on this computer: Merge tool… fails until it is installed.", section.Global.MergeToolStatus);
        section.Global.MergeToolValue = "araxis";
        Assert.Null(section.Global.MergeToolStatus);

        // The repository scope's Not set names what it falls back to.
        section.ShowRepositoryCommand.Execute(null);
        pick.Answer = null;
        await section.Repository.ChooseMergeToolCommand.ExecuteAsync(null);
        Assert.Equal(new PickOption("Not set", "same as global (vimdiff)"), pick.Shown[^1].Options[0]);
        Assert.Equal("Not set", pick.Shown[^1].Selected);
    }

    [Fact]
    public async Task While_the_list_is_looked_for_choose_is_disabled_and_after_a_failure_it_offers_what_the_read_has()
    {
        var held = new VisualCommit.Testing.Headless.HeldMergeTools();
        var git = new FakeGitConfiguration { Outside = Read(false, Global("mergetool.slow.cmd", "true"), Global("merge.tool", "mystery")), InRepository = Read(true) };
        var pick = new FakePick();
        var section = new GitSettingsViewModel(git, new FakeSettingsRepository(), held.Catalogues.MergeTools, pick.ShowAsync);
        await section.LoadAsync();

        // The fields are there; the list is not (8.10).
        Assert.False(section.IsReading);
        Assert.True(section.IsLookingForMergeTools);
        Assert.False(section.MergeToolsLoaded.IsCompleted);
        Assert.False(section.Global.ChooseMergeToolCommand.CanExecute(null));
        Assert.Equal("Looking for the merge tools git knows…", section.Global.MergeToolChooseToolTip);
        Assert.Equal("Looking for the merge tools git knows…", section.Global.MergeToolStatus);
        Assert.Equal("mystery", section.Global.MergeToolText);

        held.Fail();
        await section.MergeToolsLoaded;
        Assert.True(section.Global.ChooseMergeToolCommand.CanExecute(null));
        Assert.Null(section.Global.MergeToolChooseToolTip);
        Assert.Equal("Git could not list its merge tools.", section.Global.MergeToolStatus);
        await section.Global.ChooseMergeToolCommand.ExecuteAsync(null);
        Assert.Equal(["Not set", "slow", "mystery"], pick.Shown[^1].Options.Select(option => option.Text));
        Assert.Equal("git does not know this tool", pick.Shown[^1].Options[^1].Detail);

        // A list that is there at once (a kept one) enables Choose… at once.
        var atOnce = await LoadAsync(git, new FakeSettingsRepository());
        Assert.True(atOnce.Global.ChooseMergeToolCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_read_shows_gits_first_line_and_no_usable_git_its_sentence()
    {
        var broken = new FakeGitConfiguration
        {
            ReadFailure = new GitException("git config --show-scope --show-origin -z --list", 128, "warning: x\nfatal: bad config line 1 in file C:/Users/me/.gitconfig\n"),
        };
        var read = await LoadAsync(broken, new FakeSettingsRepository());
        Assert.Equal("Git's configuration could not be read: fatal: bad config line 1 in file C:/Users/me/.gitconfig", read.ReadError);
        Assert.False(read.ShowsFields);
        Assert.Empty(read.GlobalWrites());

        var none = new FakeGitConfiguration { ReadFailure = new GitUnavailableException("Git was not found on this computer.") };
        var unavailable = await LoadAsync(none, null);
        Assert.Equal("Git was not found on this computer. Git's own settings need it.", unavailable.Unavailable);
        Assert.False(unavailable.ShowsFields);
    }

    internal static async Task<GitSettingsViewModel> LoadAsync(
        FakeGitConfiguration git,
        FakeSettingsRepository? repository,
        MergeToolHelp? help = null,
        FakePick? pick = null,
        Func<string, bool>? fileExists = null)
    {
        var tools = help ?? VisualCommit.Core.ToolCatalogues.FixedMergeTools;
        var section = new GitSettingsViewModel(git, repository, _ => Task.FromResult(tools), (pick ?? new FakePick()).ShowAsync, fileExists: fileExists ?? (_ => false));
        await section.LoadAsync();
        await section.MergeToolsLoaded;
        return section;
    }

    internal static GitConfigRead Read(bool inRepository, params GitConfigValue[] values) => new(values, inRepository);

    internal static GitConfigValue Global(string key, string? value, string? origin = null) => new(G.Global, origin ?? GlobalFile, key, value);

    internal static GitConfigValue Local(string key, string? value, string? origin = null) => new(G.Local, origin ?? LocalFile, key, value);

    internal static GitConfigValue System(string key, string? value) => new(G.System, SystemFile, key, value);

    internal static GitConfigValue Command(string key, string? value) => new(G.Command, null, key, value);

    private static List<string> Texts(IEnumerable<GitNotice> notices) => [.. notices.Select(notice => notice.Text)];

    /// <summary>Git's configuration as the screen reads and writes it, from values the test sets; every call recorded.</summary>
    internal sealed class FakeGitConfiguration : IGitConfiguration
    {
        public GitConfigRead Outside { get; set; } = Read(false);

        public GitConfigRead InRepository { get; set; } = Read(true);

        public GitIdentityRead Identity { get; set; } = new("Test Author <author@example.com>", "Test Author <author@example.com>", null);

        public Exception? ReadFailure { get; set; }

        public Exception? WriteFailure { get; set; }

        public Dictionary<string, string?> Variables { get; } = new(StringComparer.Ordinal);

        public List<string?> Reads { get; } = [];

        public List<string> IdentityReads { get; } = [];

        public List<IReadOnlyList<GitConfigWrite>> GlobalWrites { get; } = [];

        public string GlobalFile => GitSettingsViewModelTests.GlobalFile;

        public string XdgFile => GitSettingsViewModelTests.XdgFile;

        public Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default)
        {
            Reads.Add(workingTree);
            return ReadFailure is { } failure ? Task.FromException<GitConfigRead>(failure) : Task.FromResult(workingTree is null ? Outside : InRepository);
        }

        public Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default)
        {
            IdentityReads.Add(workingTree);
            return Task.FromResult(Identity);
        }

        public string? Variable(string name) => Variables.TryGetValue(name, out var value) ? value : null;

        public Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default)
        {
            GlobalWrites.Add(writes);
            return WriteFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }

        public Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default) => Task.FromResult(VisualCommit.Core.ToolCatalogues.FixedMergeTools);

        public void Dispose()
        {
        }
    }

    /// <summary>"This repository" without git: its name, folders, branch, Pull's reading and a save the test answers.</summary>
    internal sealed class FakeSettingsRepository : ISettingsRepository
    {
        public string Name => "web-app";

        public string Path => WorkTree;

        public string ConfigFile => LocalFile;

        public string? BranchName { get; set; } = "main";

        public string? Branch => BranchName;

        public PullConfiguration Pull { get; set; } = new(PullFastForwardSetting.Unset, PullRebaseSetting.None, "pull.rebase", null, null, null, null);

        public List<string> PullReads { get; } = [];

        public List<IReadOnlyList<GitConfigWrite>> Saves { get; } = [];

        public List<CancellationToken> SaveTokens { get; } = [];

        /// <summary>What a save answers.</summary>
        public OperationResult SaveResult { get; set; } = new(OperationOutcome.Succeeded, "Saved the repository's settings", null);

        /// <summary>A save waits until <see cref="ReleaseWaitingSave"/>, or until its token is cancelled (then it answers Cancelled).</summary>
        public bool HoldSaves { get; set; }

        private TaskCompletionSource<OperationResult>? _held;

        public OperationSnapshot? Running { get; set; }

        public event EventHandler? QueueChanged;

        public Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default)
        {
            PullReads.Add(branch);
            return Task.FromResult(Pull);
        }

        public async Task<OperationResult> SaveAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellation)
        {
            Saves.Add(writes);
            SaveTokens.Add(cancellation);
            if (!HoldSaves)
            {
                return SaveResult;
            }

            var held = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _held = held;
            using (cancellation.Register(() => held.TrySetResult(new OperationResult(OperationOutcome.Cancelled, "Save repository settings cancelled", null))))
            {
                return await held.Task;
            }
        }

        /// <summary>A held save answers <see cref="SaveResult"/>.</summary>
        public void ReleaseWaitingSave() => _held?.TrySetResult(SaveResult);

        public void RaiseQueueChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The Pick dialogs the section opened, answered with <see cref="Answer"/> (null: cancelled).</summary>
    internal sealed class FakePick
    {
        public List<PickDialogViewModel> Shown { get; } = [];

        public string? Answer { get; set; }

        public Task<string?> ShowAsync(DialogViewModel<string?> dialog)
        {
            Shown.Add((PickDialogViewModel)dialog);
            return Task.FromResult(Answer);
        }
    }
}
