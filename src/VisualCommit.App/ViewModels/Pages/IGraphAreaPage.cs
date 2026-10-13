using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Pages;

/// <summary>
/// A page of the graph area (docs/phase-5-design.md 11.1, D176): the file history, the blame or
/// the reflog, shown in place of the graph (<see cref="RepositoryViewModel.Page"/>). A page holds
/// no work, so leaving it never asks; at most one is open, and it and the interactive list
/// exclude each other. A diff can open over it, and closing the diff gives the page the focus back.
/// </summary>
public interface IGraphAreaPage : IDisposable
{
    /// <summary>The page's title, which names its pane in UI Automation: <c>History of Parser.cs</c>.</summary>
    string Title { get; }

    /// <summary>Raised when the page wants the keyboard focus: its view focuses its list or text.</summary>
    event EventHandler? FocusRequested;

    /// <summary>Asks the host to close the page (its close button, Esc): the graph shows again and takes the focus.</summary>
    void Close();

    /// <summary>Gives the page the keyboard focus (<see cref="FocusRequested"/>): after a diff over it closes.</summary>
    void RequestFocus();

    /// <summary>
    /// The repository's refs were read again (every refresh, also when no ref target moved): a page
    /// that shows what changes reads again (the reflog, 5.2).
    /// </summary>
    void OnRefsRead(RepoRefs refs);
}

/// <summary>
/// What a page of the graph area gets from its repository (11.1, 11.3): reads outside the queue
/// (D47), the settings, dates and log, the menus' context and actions, and the right panel and the
/// graph behind it. <see cref="RepositoryViewModel"/> implements it; tests give a recording fake.
/// </summary>
public interface IGraphAreaPageHost
{
    /// <summary>The repository's git calls, for the page's reads.</summary>
    IGitRepository Repository { get; }

    /// <summary>Cancelled when the tab closes: every read of a page is cancelled with it.</summary>
    CancellationToken Lifetime { get; }

    ISettingsStore Settings { get; }

    DateDisplay Dates { get; }

    IAppLog Log { get; }

    /// <summary>The actions the page's menus start (<see cref="RepositoryMenus"/>' page builders).</summary>
    IRepositoryActions Actions { get; }

    /// <summary>The repository's state as the menus see it, read when a menu opens.</summary>
    RepositoryMenuContext MenuContext { get; }

    /// <summary>
    /// Whether the graph shows the commit <paramref name="sha"/>: "Show in graph" is disabled
    /// otherwise ("This commit is not in the graph", 4.3). For the zero id of a blame's line not
    /// committed yet, whether the working-changes row shows.
    /// </summary>
    bool HasCommit(string sha);

    /// <summary>
    /// Shows the commit in the right panel and selects it behind the page when the graph has it
    /// (11.3, <see cref="RepositoryViewModel.ShowCommit"/>); the zero id of a line not committed
    /// yet selects the working-changes row. For the user's own click or key on a row only.
    /// </summary>
    void ShowCommit(string sha);

    /// <summary>
    /// "Show in graph" (11.3): closes the page, selects the commit and reveals it. The zero id of a
    /// blame's line not committed yet selects the working-changes row (4.2).
    /// </summary>
    void ShowInGraph(string sha);

    /// <summary>Closes <paramref name="page"/> when it is the one shown; another page is left alone.</summary>
    void Close(IGraphAreaPage page);
}

/// <summary>The file history page's host (4.1): it also opens the blame of a row's version.</summary>
public interface IFileHistoryHost : IGraphAreaPageHost
{
    /// <summary>"Blame" and "Blame at this commit": the blame page in place of the history.</summary>
    void OpenBlame(BlameRequest request);
}

/// <summary>The blame page's host (4.2): it also opens the file's history.</summary>
public interface IBlameHost : IGraphAreaPageHost
{
    /// <summary>"File history": the history page in place of the blame.</summary>
    void OpenFileHistory(FileHistoryRequest request);
}

/// <summary>What a file history page shows (4.1, 4.4).</summary>
/// <param name="Path">The file's path at <paramref name="Start"/>, repository-relative with <c>/</c>.</param>
/// <param name="Start">Where the history starts: <c>HEAD</c> or a full commit id, resolved to a full id when the page opens, which then stays fixed.</param>
public sealed record FileHistoryRequest(string Path, string Start);

/// <summary>What a blame page shows (4.2, 4.4).</summary>
/// <param name="Path">The file's path at <paramref name="Commit"/>, repository-relative with <c>/</c>.</param>
/// <param name="Commit">The commit whose version is blamed (a full id), or null for the working tree's.</param>
/// <param name="Caption">The header's secondary text when it is not the default (<c>at {short} {subject}</c> or <c>in the working tree</c>): <c>at HEAD</c>, <c>before a1b2c3d</c>.</param>
public sealed record BlameRequest(string Path, string? Commit, string? Caption = null);
