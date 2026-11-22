using System.Globalization;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The words a screen reader hears for the commit graph's selected row (D435): a commit's, a
/// stash's and the working-changes row's name, and the hint. Pure: no window, no git. The
/// expected texts are written out here, from the design's examples on the ops scenario, not taken
/// from the class.
/// </summary>
public sealed class GraphRowSpeechTests
{
    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    private static readonly DateTimeOffset MainWorkDate = new(2026, 1, 1, 12, 4, 0, TimeSpan.Zero);

    private static readonly CommitInfo MainWork = Commit("88d6b91e821a43cd39c94aa7892c8095b628caf8", "Main work", MainWorkDate);

    [Fact]
    public void A_commit_without_labels_is_its_subject_author_and_date()
    {
        Assert.Equal("Main work, Test Author, 2026-01-01 12:04", GraphRowSpeech.Name(MainWork, [], Utc.Format));
    }

    [Fact]
    public void Heads_row_reads_its_branch_its_upstream_and_its_tag_in_drawing_order()
    {
        RefLabel[] labels =
        [
            new("main", RefLabelKind.CurrentBranch, "refs/heads/main"),
            new("origin/main", RefLabelKind.RemoteBranch, "refs/remotes/origin/main"),
            new("v1.1", RefLabelKind.Tag, "refs/tags/v1.1"),
        ];

        Assert.Equal(
            "Main work, Test Author, 2026-01-01 12:04, current branch main, remote branch origin/main, tag v1.1",
            GraphRowSpeech.Name(MainWork, labels, Utc.Format));
    }

    [Fact]
    public void Each_kind_of_label_has_its_words()
    {
        string Words(RefLabel label) => GraphRowSpeech.Name(MainWork, [label], Utc.Format)["Main work, Test Author, 2026-01-01 12:04, ".Length..];

        Assert.Equal("current branch main", Words(new("main", RefLabelKind.CurrentBranch, "refs/heads/main")));
        Assert.Equal("branch feature/alpha", Words(new("feature/alpha", RefLabelKind.LocalBranch, "refs/heads/feature/alpha")));
        Assert.Equal("remote branch origin/feature/alpha", Words(new("origin/feature/alpha", RefLabelKind.RemoteBranch, "refs/remotes/origin/feature/alpha")));
        Assert.Equal("tag v1.0", Words(new("v1.0", RefLabelKind.Tag, "refs/tags/v1.0")));
        Assert.Equal("stash@{0}", Words(new("stash@{0}", RefLabelKind.Stash)));
        Assert.Equal("HEAD, detached", Words(new("HEAD", RefLabelKind.DetachedHead)));
    }

    [Fact]
    public void A_detached_heads_row_reads_head_first_then_the_rest_as_given()
    {
        RefLabel[] labels =
        [
            new("HEAD", RefLabelKind.DetachedHead),
            new("dev", RefLabelKind.LocalBranch, "refs/heads/dev"),
            new("v1", RefLabelKind.Tag, "refs/tags/v1"),
        ];

        Assert.Equal("Main work, Test Author, 2026-01-01 12:04, HEAD, detached, branch dev, tag v1", GraphRowSpeech.Name(MainWork, labels, Utc.Format));
    }

    [Fact]
    public void Every_label_is_read_also_those_the_row_folds_into_plus_n()
    {
        // The graph draws as many labels as fit and folds the rest into "+N"; the name reads all.
        var labels = Enumerable.Range(1, 8)
            .Select(n => new RefLabel("feature/branch-" + n.ToString(CultureInfo.InvariantCulture), RefLabelKind.LocalBranch, "refs/heads/feature/branch-" + n.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        var name = GraphRowSpeech.Name(MainWork, labels, Utc.Format);

        Assert.Equal(
            "Main work, Test Author, 2026-01-01 12:04, " + string.Join(", ", Enumerable.Range(1, 8).Select(n => "branch feature/branch-" + n.ToString(CultureInfo.InvariantCulture))),
            name);
    }

    [Fact]
    public void A_label_with_an_open_request_adds_the_requests_words_in_parentheses()
    {
        var login = new RefLabel("feature/login", RefLabelKind.LocalBranch, "refs/heads/feature/login");
        var upstream = new RefLabel("origin/feature/login", RefLabelKind.RemoteBranch, "refs/remotes/origin/feature/login");
        var tag = new RefLabel("v2", RefLabelKind.Tag, "refs/tags/v2");

        var name = GraphRowSpeech.Name(
            MainWork,
            [login, upstream, tag],
            Utc.Format,
            label => label.Kind == RefLabelKind.Tag ? null : "pull request #12: Add the login form");

        Assert.Equal(
            "Main work, Test Author, 2026-01-01 12:04, branch feature/login (pull request #12: Add the login form), "
            + "remote branch origin/feature/login (pull request #12: Add the login form), tag v2",
            name);
    }

    [Fact]
    public void An_empty_request_text_adds_nothing()
    {
        var label = new RefLabel("feature/login", RefLabelKind.LocalBranch, "refs/heads/feature/login");

        Assert.Equal("Main work, Test Author, 2026-01-01 12:04, branch feature/login", GraphRowSpeech.Name(MainWork, [label], Utc.Format, _ => string.Empty));
    }

    [Fact]
    public void A_requests_words_are_githubs_gitlabs_and_azure_devops_and_a_draft_says_so()
    {
        Assert.Equal(
            "pull request #12: Add the login form",
            GraphRowSpeech.Request(false, HostingTexts.Request(HostingProvider.GitHub), HostingTexts.Number(HostingProvider.GitHub, 12), "Add the login form"));
        Assert.Equal(
            "draft pull request #2: Update the readme",
            GraphRowSpeech.Request(true, HostingTexts.Request(HostingProvider.GitHub), HostingTexts.Number(HostingProvider.GitHub, 2), "Update the readme"));
        Assert.Equal(
            "merge request !21: Add the login form",
            GraphRowSpeech.Request(false, HostingTexts.Request(HostingProvider.GitLab), HostingTexts.Number(HostingProvider.GitLab, 21), "Add the login form"));
        Assert.Equal(
            "pull request !21: Add the login form",
            GraphRowSpeech.Request(false, HostingTexts.Request(HostingProvider.AzureDevOps), HostingTexts.Number(HostingProvider.AzureDevOps, 21), "Add the login form"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_or_blank_subject_reads_no_message(string subject)
    {
        var commit = Commit("4ef1f7c0000000000000000000000000000000aa", subject, MainWorkDate);

        Assert.Equal("No message, Test Author, 2026-01-01 12:04", GraphRowSpeech.Name(commit, [], Utc.Format));
    }

    [Fact]
    public void A_stash_row_reads_stash_its_subject_its_date_and_its_label_without_an_author()
    {
        var stash = Commit("0e95aca000000000000000000000000000000001", "On main: Experiment", new DateTimeOffset(2026, 1, 1, 12, 7, 0, TimeSpan.Zero), CommitKind.Stash);

        Assert.Equal(
            "Stash: On main: Experiment, 2026-01-01 12:07, stash@{0}",
            GraphRowSpeech.Name(stash, [new RefLabel("stash@{0}", RefLabelKind.Stash)], Utc.Format));
    }

    [Fact]
    public void A_stash_without_a_message_reads_no_message()
    {
        var stash = Commit("0e95aca000000000000000000000000000000001", " ", new DateTimeOffset(2026, 1, 1, 12, 7, 0, TimeSpan.Zero), CommitKind.Stash);

        Assert.Equal("Stash: No message, 2026-01-01 12:07", GraphRowSpeech.Name(stash, [], Utc.Format));
    }

    [Fact]
    public void The_date_is_the_authors_in_the_date_columns_own_format()
    {
        var authored = new DateTimeOffset(2026, 1, 1, 14, 4, 0, TimeSpan.FromHours(2));
        var commit = MainWork with { AuthorDate = authored, CommitDate = authored.AddDays(3) };

        Assert.Equal("Main work, Test Author, 2026-01-01 12:04", GraphRowSpeech.Name(commit, [], Utc.Format));
        Assert.Equal("Main work, Test Author, someday", GraphRowSpeech.Name(commit, [], date => date == authored ? "someday" : "the commit date"));
    }

    [Fact]
    public void The_working_rows_name_is_its_text_with_the_dot_read_as_a_comma()
    {
        Assert.Equal("Working changes", GraphRowSpeech.WorkingRowName("Working changes"));
        Assert.Equal("Working changes, 1 conflict", GraphRowSpeech.WorkingRowName("Working changes · 1 conflict"));
        Assert.Equal("Working changes, 2 conflicts", GraphRowSpeech.WorkingRowName("Working changes · 2 conflicts"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_working_row_without_text_reads_working_changes(string text)
    {
        Assert.Equal("Working changes", GraphRowSpeech.WorkingRowName(text));
    }

    [Fact]
    public void The_working_rows_default_word_is_the_graphs()
    {
        Assert.Equal(CommitGraphControl.DefaultWorkingRowText, GraphRowSpeech.WorkingChanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void The_hint_of_a_commit_selected_alone_is_its_short_id(int selected)
    {
        Assert.Equal("Commit 88d6b91", GraphRowSpeech.Hint(MainWork, selected, matchesSearch: false));
    }

    [Fact]
    public void The_hint_counts_the_commits_selected_from_two_on()
    {
        Assert.Equal("Commit 88d6b91, 2 commits selected", GraphRowSpeech.Hint(MainWork, 2, matchesSearch: false));
        Assert.Equal("Commit 88d6b91, 100000 commits selected", GraphRowSpeech.Hint(MainWork, 100_000, matchesSearch: false));
    }

    [Fact]
    public void The_hint_says_when_the_search_marks_the_row()
    {
        Assert.Equal("Commit 88d6b91, matches the search", GraphRowSpeech.Hint(MainWork, 1, matchesSearch: true));
        Assert.Equal("Commit 88d6b91, 3 commits selected, matches the search", GraphRowSpeech.Hint(MainWork, 3, matchesSearch: true));
    }

    [Fact]
    public void The_count_has_the_multi_selection_panels_words_and_invariant_digits()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var items = Enumerable.Range(0, 3).Select(n => new MultiSelectionItem(MainWork.Sha, MainWork.ShortSha, "Commit " + n.ToString(CultureInfo.InvariantCulture))).ToList();
            var panel = new MultiSelectionViewModel(items).Title;

            Assert.Equal("Commit 88d6b91" + GraphRowSpeech.Separator + panel, GraphRowSpeech.Hint(MainWork, 3, matchesSearch: false));
            Assert.Equal("Commit 88d6b91, 12345 commits selected", GraphRowSpeech.Hint(MainWork, 12_345, matchesSearch: false));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_stash_rows_hint_is_its_short_id_too()
    {
        var stash = Commit("0e95aca000000000000000000000000000000001", "On main: Experiment", MainWorkDate, CommitKind.Stash);

        Assert.Equal("Commit 0e95aca", GraphRowSpeech.Hint(stash, 1, matchesSearch: false));
    }

    private static CommitInfo Commit(string sha, string subject, DateTimeOffset date, CommitKind kind = CommitKind.Commit) =>
        new(sha, [], "Test Author", "author@example.com", date, date, subject, kind);
}
