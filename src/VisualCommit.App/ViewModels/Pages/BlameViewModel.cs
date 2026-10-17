using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Pages;

/// <summary>What the blame page's body shows (docs/phase-5-design.md 4.2).</summary>
public enum BlameBody
{
    /// <summary>Git has not ended: <c>Loading…</c>.</summary>
    Loading,

    /// <summary>The file's lines with the gutter.</summary>
    Text,

    /// <summary>A message in the text's place: a binary or empty file, or git's failure.</summary>
    Message,
}

/// <summary>
/// The blame page (docs/phase-5-design.md 4.2, D158, D175): a file's lines, each with the commit
/// that last changed it, at a commit or in the working tree, grouped in runs of consecutive lines
/// of one commit for the gutter (BD12). A click on a run shows its commit in the right panel and
/// selects it behind the page (11.3); a double-click shows it in the graph; a run of lines not
/// committed yet stands for the working-changes row (4.3).
/// </summary>
public sealed partial class BlameViewModel : GraphAreaPageViewModel
{
    /// <summary>From this page width the gutter is <see cref="WideGutterWidth"/> wide and shows the author; below it <see cref="NarrowGutterWidth"/> (4.2).</summary>
    public const double WideGutterFromWidth = 700;

    /// <summary>The gutter's width on a page at least <see cref="WideGutterFromWidth"/> wide.</summary>
    public const double WideGutterWidth = 200;

    /// <summary>The gutter's width on a narrower page (the author moves into the tooltip).</summary>
    public const double NarrowGutterWidth = 130;

    /// <summary>Phase 2's very-large limit (D65): a file with more lines, or more bytes, shows without syntax colours.</summary>
    public const int VeryLargeLines = 10_000;

    /// <inheritdoc cref="VeryLargeLines"/>
    public const int VeryLargeBytes = 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IBlameHost _host;

    public BlameViewModel(IBlameHost host, BlameRequest request)
        : base(host)
    {
        ArgumentNullException.ThrowIfNull(request);
        _host = host;
        Request = request;
        FileName = WorkingChangesViewModel.FileName(request.Path);
        Title = $"Blame of {FileName}";
        Caption = request.Caption ?? (request.Commit is null ? InWorkingTree : string.Empty);
    }

    /// <summary>The header's text when the working tree's version is blamed.</summary>
    public const string InWorkingTree = "in the working tree";

    /// <summary>What the page shows: the path and the version.</summary>
    public BlameRequest Request { get; }

    /// <summary>The file's name, the header's <c>Blame of {name}</c>.</summary>
    public string FileName { get; }

    /// <summary>The blamed commit as a full id once read; null for the working tree.</summary>
    public string? Commit { get; private set; }

    /// <summary>The header's secondary text: <c>at {short} {subject}</c>, <c>in the working tree</c>, or the request's (<c>at HEAD</c>, <c>before a1b2c3d</c>).</summary>
    [ObservableProperty]
    public partial string Caption { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading), nameof(ShowsText), nameof(ShowsMessage))]
    public partial BlameBody Body { get; private set; } = BlameBody.Loading;

    public bool ShowsLoading => Body == BlameBody.Loading;

    public bool ShowsText => Body == BlameBody.Text;

    public bool ShowsMessage => Body == BlameBody.Message;

    /// <summary>What the text's place says instead of the lines: a binary or empty file, or git's message.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>The message is git's failure: drawn in the error style.</summary>
    [ObservableProperty]
    public partial bool IsError { get; private set; }

    /// <summary>The file's lines, each with its commit, as git gave them (one character per byte, D66). Set with <see cref="Text"/>, <see cref="Runs"/> and <see cref="Highlighting"/> before <see cref="TextShown"/>.</summary>
    public IReadOnlyList<BlameLine> Lines { get; private set; } = [];

    /// <summary>
    /// The lines as the page shows them, joined by line feeds: <c>BlameText</c>'s document and its
    /// UI Automation value. Decoded as UTF-8 when every line is valid UTF-8, otherwise shown as
    /// the bytes are, as the diff decides (phase 2); decoded once per read.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The runs of consecutive lines of one commit, top to bottom (BD12).</summary>
    public IReadOnlyList<BlameRun> Runs { get; private set; } = [];

    /// <summary>Syntax colours: off for a very large file (D65).</summary>
    public bool Highlighting { get; private set; } = true;

    /// <summary>
    /// The read's lines, text, runs and highlighting are all set: the view fills the text and the
    /// gutter once, for all of them together.
    /// </summary>
    public event EventHandler? TextShown;

    /// <summary>The run whose commit was picked last (its lines have the selection background in the gutter), or null.</summary>
    [ObservableProperty]
    public partial BlameRun? SelectedRun { get; private set; }

    /// <summary>The page is at least <see cref="WideGutterFromWidth"/> wide: the gutter is <see cref="WideGutterWidth"/> wide and shows the author. The view sets it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GutterWidth))]
    public partial bool IsWide { get; set; }

    /// <summary>The gutter's width (4.2).</summary>
    public double GutterWidth => IsWide ? WideGutterWidth : NarrowGutterWidth;

    /// <summary>
    /// Reads the blame (4.2): git prints it only when it ends, so the page says <c>Loading…</c>
    /// until then; a commit's caption takes the commit's subject, read beside it. Closing the page
    /// cancels both.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var details = Request.Commit is { } revision ? Host.Repository.ReadCommitDetailsAsync(revision, Lifetime) : null;
            var blame = await Host.Repository.ReadBlameAsync(Request.Commit, Request.Path, Lifetime);
            if (details is not null)
            {
                var commit = await details;
                Commit = commit.Sha;
                if (Request.Caption is null)
                {
                    Caption = $"at {Short(commit.Sha)} {commit.Subject}".TrimEnd();
                }
            }

            if (!IsClosed)
            {
                Show(blame);
            }
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested)
        {
        }
        catch (BlameIgnoreRevsFileMissingException ex)
        {
            ShowMessage($"Blame stopped: the git setting blame.ignoreRevsFile names {ex.File}, which does not exist.", isError: true);
        }
        catch (Exception ex)
        {
            Host.Log.Error($"The blame of {Request.Path} could not be read.", ex);
            ShowMessage(ex is GitException { StandardError: { } error } && !string.IsNullOrWhiteSpace(error) ? error.Trim() : ex.Message, isError: true);
        }
    }

    /// <summary>
    /// A click on a run, in the gutter or on its text (4.2, 11.3): its lines take the selection
    /// background, and the right panel shows its commit, selected behind the page when the graph
    /// has it; a run not committed yet selects the working-changes row.
    /// </summary>
    public void Pick(BlameRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (IsClosed)
        {
            return;
        }

        SelectedRun = run;
        Host.ShowCommit(run.Commit.Sha);
    }

    /// <summary>A double-click on a run and its "Show in graph": the page closes and the graph shows the commit; nothing for a commit the graph does not have (4.3).</summary>
    public void ShowInGraph(BlameRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!IsClosed && Host.HasCommit(run.Commit.Sha))
        {
            Host.ShowInGraph(run.Commit.Sha);
        }
    }

    /// <summary>A run's menu (<see cref="RepositoryMenus.ForBlameRun"/>), with whether the graph has its commit now.</summary>
    public MenuModel MenuFor(BlameRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return RepositoryMenus.ForBlameRun(Host.MenuContext, run.Commit, Host.HasCommit(run.Commit.Sha), Host.Actions);
    }

    /// <summary>
    /// A run's tooltip (BD11): the full SHA, <c>{author} &lt;{email}&gt;</c>, the author date as
    /// <c>yyyy-MM-dd HH:mm</c>, the subject, and <c>as {path}</c> when the line came from another
    /// path; a run not committed yet says <c>Not committed yet</c> alone (git gives it the wall clock).
    /// </summary>
    public string ToolTipOf(BlameRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var commit = run.Commit;
        if (commit.IsUncommitted)
        {
            return OperationTexts.MenuTexts.NotCommittedYet;
        }

        var lines = new List<string> { commit.Sha, $"{commit.AuthorName} <{commit.AuthorEmail}>", Host.Dates.Format(commit.AuthorTime), commit.Summary };
        if (!string.Equals(commit.Path, Request.Path, StringComparison.Ordinal))
        {
            lines.Add($"as {commit.Path}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>The date a run's first line shows in the gutter, <c>yyyy-MM-dd</c> in the dates' zone (4.2); none for lines not committed yet (BD11).</summary>
    public string DateOf(BlameRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.Commit.IsUncommitted
            ? string.Empty
            : TimeZoneInfo.ConvertTime(run.Commit.AuthorTime, Host.Dates.TimeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>The header's <c>File history</c> (<c>BlameHistory</c>): the file's history from the same commit (HEAD for the working tree's version), in place of this page.</summary>
    [RelayCommand]
    private void ShowFileHistory() =>
        _host.OpenFileHistory(new FileHistoryRequest(Request.Path, Commit ?? Request.Commit ?? OperationTexts.MenuTexts.HeadSource));

    private void Show(BlameResult blame)
    {
        if (blame.IsSubmodule)
        {
            ShowMessage($"{FileName} is a submodule: it has no lines to blame", isError: false);
            return;
        }

        if (blame.IsBinary)
        {
            ShowMessage($"{FileName} is a binary file: it has no lines to blame", isError: false);
            return;
        }

        if (blame.IsEmpty)
        {
            ShowMessage($"{FileName} is empty", isError: false);
            return;
        }

        Highlighting = blame.Lines.Count <= VeryLargeLines && blame.Lines.Sum(line => (long)line.Text.Length + 1) <= VeryLargeBytes;
        Lines = blame.Lines;
        Text = string.Join('\n', Decode(blame.Lines));
        Runs = RunsOf(blame.Lines);
        TextShown?.Invoke(this, EventArgs.Empty);
        Body = BlameBody.Text;
        RequestFocus();
    }

    private void ShowMessage(string message, bool isError)
    {
        if (IsClosed)
        {
            return;
        }

        Message = message;
        IsError = isError;
        Body = BlameBody.Message;
    }

    /// <summary>The lines' texts for display: UTF-8 when every line is valid UTF-8, otherwise each byte as a character.</summary>
    private static IEnumerable<string> Decode(IReadOnlyList<BlameLine> lines)
    {
        var utf8 = lines.All(line => IsValidUtf8(line.Text));
        return lines.Select(line => utf8 ? DiffParser.DecodeUtf8(line.Text) : line.Text);
    }

    private static bool IsValidUtf8(string latin1)
    {
        if (latin1.All(c => c <= 0x7F))
        {
            return true;
        }

        try
        {
            _ = StrictUtf8.GetCharCount(Encoding.Latin1.GetBytes(latin1));
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Groups consecutive lines of one commit (git may give them in several porcelain groups, 0.1).</summary>
    public static IReadOnlyList<BlameRun> RunsOf(IReadOnlyList<BlameLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var runs = new List<BlameRun>();
        var first = 0;
        for (var line = 1; line <= lines.Count; line++)
        {
            if (line == lines.Count || !string.Equals(lines[line].Commit.Sha, lines[first].Commit.Sha, StringComparison.Ordinal))
            {
                runs.Add(new BlameRun(runs.Count, first + 1, line, lines[first].Commit));
                first = line;
            }
        }

        return runs;
    }
}

/// <summary>A run of the blame's gutter (4.2, BD12): consecutive lines last changed by one commit.</summary>
/// <param name="Index">Its place among the runs, from 0: run 0 and every even run have the panel's background, the odd ones the window's.</param>
/// <param name="FirstLine">Its first line, from 1.</param>
/// <param name="LastLine">Its last line.</param>
/// <param name="Commit">The commit that last changed its lines (the zero id for lines not committed yet).</param>
public sealed record BlameRun(int Index, int FirstLine, int LastLine, BlameCommit Commit)
{
    /// <summary>Its lines have the panel's background (run 0, 2, …); the others the window's.</summary>
    public bool IsEven => Index % 2 == 0;

    /// <summary>Its commit's first 7 characters, as the gutter shows them.</summary>
    public string ShortSha => Commit.Sha[..Math.Min(7, Commit.Sha.Length)];
}
