using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.PageFakes;

namespace VisualCommit.App.Tests;

/// <summary>
/// The blame page's view model without a UI (docs/phase-5-design.md 4.2, 4.3, 11.3; report part
/// B's decisions BD11, BD12): the runs, the caption, the gutter's dates and the tooltips (lines not
/// committed yet without git's wall clock), a click and "Show in graph", "File history", and the
/// binary, empty and failed files.
/// </summary>
public class BlameViewModelTests
{
    private static readonly BlameCommit Parser = BlameCommitOf(AddParserSha, "Mira Kovac", 1, "src/Parser.cs");
    private static readonly BlameCommit Renamed = BlameCommitOf(RenameSha, "Mira Kovac", 6, "src/Reader.cs", AddTracingSha, "src/Parser.cs");
    private static readonly BlameCommit Uncommitted = new(Zero, "Not Committed Yet", "not.committed.yet", DateTimeOffset.Now, "Version of src/Reader.cs from src/Reader.cs", "src/Reader.cs", HeadSha, "src/Reader.cs", false);

    [Fact]
    public async Task The_working_trees_blame_groups_its_lines_in_runs_and_its_lines_not_committed_yet_show_no_date()
    {
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult(
            [
                new("namespace History;", Parser),
                new(string.Empty, Parser),
                new("/// <summary>Reads the lines of a text.</summary>", Uncommitted),
                new("public sealed class Reader", Renamed),
                new("{", Parser),
            ]),
        };
        var host = new RecordingPageHost(repository);
        using var page = new BlameViewModel(host, new BlameRequest("src/Reader.cs", null));
        var focus = 0;
        page.FocusRequested += (_, _) => focus++;

        await page.LoadAsync();

        Assert.Equal(("Blame of Reader.cs", "in the working tree", true), (page.Title, page.Caption, page.ShowsText));
        Assert.Equal("namespace History;\n\n/// <summary>Reads the lines of a text.</summary>\npublic sealed class Reader\n{", page.Text);
        Assert.Equal([(0, 1, 2, true), (1, 3, 3, false), (2, 4, 4, true), (3, 5, 5, false)], page.Runs.Select(run => (run.Index, run.FirstLine, run.LastLine, run.IsEven)));
        Assert.False(repository.Reads.ContainsKey("details"));
        Assert.Equal(1, focus);

        // BD11, BD12: the date of a run, none for lines not committed yet; the tooltips.
        Assert.Equal(["2026-01-01", string.Empty, "2026-01-01", "2026-01-01"], page.Runs.Select(page.DateOf));
        Assert.Equal("1d5a7cf999727e7f724054348c81866a62025184\nMira Kovac <mira@example.com>\n2026-01-01 12:01\nAdd the parser\nas src/Parser.cs", page.ToolTipOf(page.Runs[0]));
        Assert.Equal("Not committed yet", page.ToolTipOf(page.Runs[1]));
        Assert.Equal("0575c7f6631b3ba6c9d623f53b28489405230cfd\nMira Kovac <mira@example.com>\n2026-01-01 12:06\nRename the parser to reader", page.ToolTipOf(page.Runs[2]));
    }

    [Fact]
    public async Task A_click_picks_a_run_and_shows_its_commit_and_Show_in_graph_needs_the_graph_to_have_it()
    {
        var repository = new PageFakeRepository { Blame = (_, _) => new BlameResult([new("a", Parser), new("b", Uncommitted)]) };
        var host = new RecordingPageHost(repository);
        host.InGraph.Add(AddParserSha);
        using var page = new BlameViewModel(host, new BlameRequest("src/Reader.cs", null));
        await page.LoadAsync();

        page.Pick(page.Runs[0]);
        page.Pick(page.Runs[1]);

        // The zero id stands for the working-changes row (4.3): the host selects it.
        Assert.Equal([AddParserSha, Zero], host.Shown);
        Assert.Same(page.Runs[1], page.SelectedRun);

        page.ShowInGraph(page.Runs[1]);
        page.ShowInGraph(page.Runs[0]);
        Assert.Equal([AddParserSha], host.ShownInGraph);
        Assert.Equal(["Not committed yet", "Show in graph (disabled: \"This commit is not in the graph\")"], RepositoryMenusTests.Lines(page.MenuFor(page.Runs[1])));
    }

    [Fact]
    public async Task A_commits_blame_is_captioned_with_its_subject_and_its_file_history_starts_there()
    {
        var repository = new PageFakeRepository { Blame = (_, _) => new BlameResult([new("x", Parser)]) };
        var host = new RecordingPageHost(repository);
        using var page = new BlameViewModel(host, new BlameRequest("src/Parser.cs", AddTracingSha));

        await page.LoadAsync();

        Assert.Equal(("at 14a5c6c Add tracing", AddTracingSha), (page.Caption, page.Commit));
        page.ShowFileHistoryCommand.Execute(null);
        Assert.Equal([new FileHistoryRequest("src/Parser.cs", AddTracingSha)], host.Histories);

        // A caption the request gives stays (4.4: a deleted file's "before {short}").
        using var before = new BlameViewModel(host, new BlameRequest("docs/notes.md", ParentSha, "before c0bc2dd"));
        await before.LoadAsync();
        Assert.Equal("before c0bc2dd", before.Caption);

        // The working tree's version: the history from HEAD.
        using var working = new BlameViewModel(host, new BlameRequest("src/Reader.cs", null));
        await working.LoadAsync();
        working.ShowFileHistoryCommand.Execute(null);
        Assert.Equal(new FileHistoryRequest("src/Reader.cs", "HEAD"), host.Histories[^1]);
    }

    [Fact]
    public async Task A_binary_an_empty_and_a_failed_blame_say_so_in_the_texts_place()
    {
        var repository = new PageFakeRepository { Blame = (_, _) => new BlameResult([new("PNG\0data", Parser)]) };
        var host = new RecordingPageHost(repository);

        using var binary = new BlameViewModel(host, new BlameRequest("assets/logo.png", HeadSha));
        await binary.LoadAsync();
        Assert.Equal(("logo.png is a binary file: it has no lines to blame", false, true), (binary.Message, binary.IsError, binary.ShowsMessage));

        repository.Blame = (_, _) => new BlameResult([]);
        using var empty = new BlameViewModel(host, new BlameRequest("empty.txt", null));
        await empty.LoadAsync();
        Assert.Equal("empty.txt is empty", empty.Message);

        // A submodule (15.9): git cannot blame it, and the page says so as for a binary file.
        repository.Blame = (_, _) => new BlameResult([]) { IsSubmodule = true };
        using var submodule = new BlameViewModel(host, new BlameRequest("libs/sub", HeadSha));
        await submodule.LoadAsync();
        Assert.Equal(("sub is a submodule: it has no lines to blame", false, true), (submodule.Message, submodule.IsError, submodule.ShowsMessage));

        repository.Blame = (_, _) => throw new BlameIgnoreRevsFileMissingException(".git-blame-ignore-revs", "fatal: could not open object name list: .git-blame-ignore-revs\n");
        using var ignoreRevs = new BlameViewModel(host, new BlameRequest("a.txt", null));
        await ignoreRevs.LoadAsync();
        Assert.Equal(("Blame stopped: the git setting blame.ignoreRevsFile names .git-blame-ignore-revs, which does not exist.", true), (ignoreRevs.Message, ignoreRevs.IsError));

        repository.Blame = (_, _) => throw new GitException("git blame", 128, "fatal: no such path 'gone.txt' in HEAD\n");
        using var failed = new BlameViewModel(host, new BlameRequest("gone.txt", null));
        await failed.LoadAsync();
        Assert.Equal(("fatal: no such path 'gone.txt' in HEAD", true), (failed.Message, failed.IsError));
    }

    [Fact]
    public async Task A_very_large_file_shows_without_syntax_colours_and_utf8_lines_are_decoded()
    {
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult([.. Enumerable.Range(0, BlameViewModel.VeryLargeLines + 1).Select(_ => new BlameLine("x", Parser))]),
        };
        var host = new RecordingPageHost(repository);
        using var large = new BlameViewModel(host, new BlameRequest("big.txt", null));
        await large.LoadAsync();
        Assert.False(large.Highlighting);
        Assert.Single(large.Runs);

        // "é" as git gives it, one character per byte (D66).
        repository.Blame = (_, _) => new BlameResult([new("cafÃ©", Parser)]);
        using var utf8 = new BlameViewModel(host, new BlameRequest("cafe.txt", null));
        await utf8.LoadAsync();
        Assert.True(utf8.Highlighting);
        Assert.Equal("café", utf8.Text);
    }

    [Fact]
    public void The_gutter_is_200_wide_from_a_page_700_wide_and_130_below()
    {
        using var page = new BlameViewModel(new RecordingPageHost(new PageFakeRepository()), new BlameRequest("a.txt", null));

        Assert.Equal(130, page.GutterWidth);
        page.IsWide = true;
        Assert.Equal(200, page.GutterWidth);
        Assert.Equal(700, BlameViewModel.WideGutterFromWidth);
    }
}
