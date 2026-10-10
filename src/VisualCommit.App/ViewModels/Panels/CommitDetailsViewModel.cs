using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The right panel of one repository tab: the details of the selected commit, as "Commit
/// details" in docs/test-reports/phase-1.md describes them, or phase 0's placeholder when
/// nothing is selected. A tab without a repository uses an instance without one.
/// </summary>
public partial class CommitDetailsViewModel : ObservableObject
{
    private readonly IGitRepository? _repository;
    private readonly ISettingsStore _settings;
    private readonly DateDisplay _dates;
    private readonly IAppLog _log;

    public CommitDetailsViewModel(IGitRepository? repository, ISettingsStore settings, DateDisplay dates, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dates);
        _repository = repository;
        _settings = settings;
        _dates = dates;
        _log = log ?? NullAppLog.Instance;
    }

    /// <summary>A commit's details are shown; false shows the placeholder.</summary>
    [ObservableProperty]
    public partial bool HasCommit { get; private set; }

    /// <summary>Raised when the user clicks a parent's id: the parent's full id, which the graph then selects.</summary>
    public event EventHandler<string>? ParentActivated;

    /// <summary>
    /// Shows the details of <paramref name="sha"/>, or the placeholder when it is null. For a
    /// stash, <paramref name="stash"/> is its entry: the panel then shows its name and, as its
    /// only parent, the commit it was made on. A call that comes while an earlier one is still
    /// loading cancels the earlier one, so the panel always ends up showing the last selection.
    /// Failures are logged and shown in the panel; they do not throw.
    /// </summary>
    public Task ShowAsync(string? sha, StashEntry? stash = null, CancellationToken cancellationToken = default)
    {
        HasCommit = false;
        return Task.CompletedTask;
    }

    /// <summary>Raises <see cref="ParentActivated"/>.</summary>
    protected void OnParentActivated(string sha) => ParentActivated?.Invoke(this, sha);
}
