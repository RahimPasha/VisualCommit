using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using static VisualCommit.App.ViewModels.Operations.OperationTexts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// One repository of the browser "Clone from GitHub" (docs/phase-7a-design.md 9.4): its full name,
/// what is drawn after it in the secondary colour, and the URL a "Choose" answers.
/// </summary>
/// <param name="FullName">"owner/name", the row's text and automation name.</param>
/// <param name="Detail">The flags <c>Private</c>, <c>Fork</c>, <c>Archived</c> (each when true, in that order), then the description, joined with " · "; null when nothing is left.</param>
/// <param name="CloneUrl">GitHub's plain <c>clone_url</c> without any user part (ruling 5).</param>
public sealed record GitHubRepositoryRow(string FullName, string? Detail, string CloneUrl)
{
    /// <summary>The row's tooltip: the full name, then the detail on a second line.</summary>
    public string ToolTip => Detail is null ? FullName : FullName + "\n" + Detail;

    /// <summary>The row of <paramref name="repository"/>.</summary>
    public static GitHubRepositoryRow Of(RepositoryInfo repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var parts = new List<string>();
        if (repository.IsPrivate)
        {
            parts.Add(CloneTexts.Private);
        }

        if (repository.IsFork)
        {
            parts.Add(CloneTexts.Fork);
        }

        if (repository.IsArchived)
        {
            parts.Add(CloneTexts.Archived);
        }

        // A description is one line in the row: its line breaks and runs of spaces become one space.
        var description = string.Join(' ', (repository.Description ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (description.Length > 0)
        {
            parts.Add(description);
        }

        return new GitHubRepositoryRow(
            repository.Repository.FullName,
            parts.Count == 0 ? null : string.Join(CloneTexts.DetailSeparator, parts),
            GitCommand.RemoveCredentials(repository.CloneUrl.AbsoluteUri));
    }
}

/// <summary>
/// The repository browser "Clone from GitHub" (docs/phase-7a-design.md 9.4, D301), over the clone
/// form: the signed-in account's repositories in GitHub's order (most recently updated first), a
/// filter on the full name and the detail, and "Choose", which answers the chosen row's plain clone
/// URL (null when cancelled). A sibling of <see cref="PickDialogViewModel"/>, whose filter it
/// follows. <see cref="Start"/> begins the one load on the dialog's own cancellation, which Cancel,
/// Esc and the window's <c>CancelAll</c> (D234) cancel; the answer is applied on the thread that
/// started it (the UI thread), and one that arrives after the dialog closed changes nothing.
/// </summary>
public sealed partial class CloneFromGitHubDialogViewModel : DialogViewModel<string?>
{
    private readonly Func<CancellationToken, Task<HostingResult<RepositoryList>>> _list;
    private readonly IAppLog _log;
    private readonly Func<string?>? _login;
    private readonly CancellationTokenSource _cancel = new();
    private IReadOnlyList<GitHubRepositoryRow> _all = [];
    private Task? _load;

    /// <param name="list">The account's list (the session's <c>ListRepositoriesAsync</c>).</param>
    /// <param name="log">The app's log: a failure once as a warning, an exception as an error.</param>
    /// <param name="login">The signed-in account's login when the list fails (open point 13), for <see cref="HostingTexts.Describe"/>; null for none.</param>
    public CloneFromGitHubDialogViewModel(Func<CancellationToken, Task<HostingResult<RepositoryList>>> list, IAppLog log, Func<string?>? login = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(log);
        _list = list;
        _log = log;
        _login = login;
    }

    public override string Title => CloneTexts.DialogTitle;

    /// <summary>The rows the filter lets through, in the list's order; rebuilt in place.</summary>
    public ObservableCollection<GitHubRepositoryRow> Rows { get; } = [];

    /// <summary>Every row the list gave, whatever the filter lets through.</summary>
    public IReadOnlyList<GitHubRepositoryRow> AllRows => _all;

    /// <summary>What the filter box holds; enabled while loading, so it filters the rows when they come.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>The selected row, or null.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial GitHubRepositoryRow? Selected { get; set; }

    /// <summary><c>Loading repositories…</c> while loading, <c>No repositories to list</c> for an empty list, else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStatus))]
    public partial string? StatusText { get; private set; } = CloneTexts.Loading;

    /// <summary>The failure in <see cref="HostingTexts.Describe"/>'s words, or <c>The repositories could not be loaded.</c>; else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsError))]
    public partial string? ErrorText { get; private set; }

    /// <summary>The cap's line when more repositories exist than were listed, else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMore))]
    public partial string? MoreText { get; private set; }

    /// <summary>Single sign-on's line when some organisations' repositories are hidden, else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSso))]
    public partial string? SsoText { get; private set; }

    public bool ShowsStatus => StatusText is not null;

    public bool ShowsError => ErrorText is not null;

    public bool ShowsMore => MoreText is not null;

    public bool ShowsSso => SsoText is not null;

    /// <summary>The load <see cref="Start"/> began; a completed task before it.</summary>
    public Task Load => _load ?? Task.CompletedTask;

    protected override string? CancelledResult => null;

    /// <summary>Begins the load (once) and returns it (<see cref="Load"/>).</summary>
    public Task Start() => _load ??= RunAsync();

    protected override void OnCancel()
    {
        _cancel.Cancel();
        base.OnCancel();
    }

    private bool CanConfirm() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (Selected is { } row)
        {
            Complete(row.CloneUrl);
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private async Task RunAsync()
    {
        var token = _cancel.Token;
        try
        {
            var result = await _list(token);
            if (IsClosed)
            {
                return;
            }

            if (result is { Failure: null, Value: { } list })
            {
                Show(list);
            }
            else if (result.Failure is { } failure)
            {
                var request = failure.RequestId is null ? string.Empty : $" (request {failure.RequestId})";
                _log.Warning($"Listing GitHub repositories failed: {failure.Kind}: {failure.Message}{request}");
                ShowFailure(HostingTexts.Describe(failure, _login?.Invoke()));
            }
            else
            {
                _log.Error("Listing GitHub repositories gave neither a list nor a failure.");
                ShowFailure(CloneTexts.Unexpected);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Cancel or Esc: the dialog has closed, and nothing is shown.
        }
        catch (Exception ex)
        {
            _log.Error("Listing GitHub repositories failed.", ex);
            if (!IsClosed)
            {
                ShowFailure(CloneTexts.Unexpected);
            }
        }
    }

    private void Show(RepositoryList list)
    {
        _all = [.. list.Repositories.Select(GitHubRepositoryRow.Of)];
        StatusText = _all.Count == 0 ? CloneTexts.Empty : null;
        MoreText = list.MoreExist ? CloneTexts.MoreExist : null;
        SsoText = list.SomeHiddenBySso ? CloneTexts.SomeHiddenBySso : null;
        ApplyFilter();
    }

    private void ShowFailure(string text)
    {
        _all = [];
        StatusText = null;
        ErrorText = text;
        ApplyFilter();
    }

    /// <summary>
    /// Keeps the rows whose full name or detail holds the filter, ignoring case, in the list's order,
    /// rebuilt in place as <see cref="PickDialogViewModel"/>'s items are; a selected row the filter
    /// hides is no longer selected.
    /// </summary>
    private void ApplyFilter()
    {
        var filter = Filter;
        var shown = _all
            .Where(row => row.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (row.Detail?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        var kept = shown.ToHashSet();
        if (Selected is not null && !kept.Contains(Selected))
        {
            Selected = null;
        }

        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (!kept.Contains(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (i >= Rows.Count || Rows[i] != shown[i])
            {
                Rows.Insert(i, shown[i]);
            }
        }
    }
}
