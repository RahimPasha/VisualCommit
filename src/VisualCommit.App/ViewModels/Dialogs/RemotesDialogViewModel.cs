using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// What the Remotes dialog does to the repository (docs/phase-3-design.md 2.23). Work package
/// WP7's <c>RepositoryOperations</c> implements it with the operations of 2.23; tests give a fake.
/// </summary>
public interface IRemoteActions
{
    /// <summary>The configured remotes (<c>git remote -v</c>), in git's order.</summary>
    Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync();

    /// <summary>
    /// Adds a remote and waits for it; with <paramref name="fetchNow"/> also queues a fetch of it
    /// as an operation of its own, which the dialog does not wait for. Returns null when the
    /// remote was added, otherwise git's message for the form's <c>RemoteError</c>.
    /// </summary>
    Task<string?> AddRemoteAsync(string name, string url, bool fetchNow);

    /// <summary>Renames a remote and waits for it; null, or git's message.</summary>
    Task<string?> RenameRemoteAsync(string name, string newName);

    /// <summary>Changes a remote's URL and waits for it; null, or git's message.</summary>
    Task<string?> SetRemoteUrlAsync(string name, string url);

    /// <summary>
    /// Asks the confirmation of 2.23 (on top of the Remotes dialog, D104) and, when confirmed,
    /// removes the remote after its backup and waits for it. A failure shows its own error dialog.
    /// </summary>
    Task RemoveRemoteAsync(string name);
}

/// <summary>One remote in the Remotes dialog's list (<c>RemoteRow</c>, named by the remote).</summary>
/// <param name="Name">The remote's name.</param>
/// <param name="Url">Its fetch URL as configured, credentials included: shown only in the Change URL form (D103).</param>
/// <param name="ShownUrl">Its fetch URL with credentials hidden: the list and its tooltip (D103).</param>
public sealed record RemoteRow(string Name, string Url, string ShownUrl);

/// <summary>
/// The Remotes dialog (<c>RemotesDialog</c>, docs/phase-3-design.md 2.23, 4f). In list mode it
/// shows a row per remote (its name, its shown URL, "Edit" and "Remove"), "Add remote", and
/// "Close". "Add remote" and "Edit" switch the card to a form: the name (<c>RemoteNameBox</c>),
/// the URL as configured (<c>RemoteUrlBox</c>), "Fetch now" when adding (<c>RemoteFetchOption</c>),
/// the form's message (<c>RemoteError</c>), Cancel (back to the list) and "Save". Save runs the
/// operation and waits for it ("Saving…", the buttons disabled), then goes back to the list, or
/// shows git's message. "Remove" asks its confirmation on top of the dialog and refreshes the list
/// afterwards. Esc in the form goes back to the list; in the list it closes the dialog.
/// </summary>
public sealed partial class RemotesDialogViewModel : DialogViewModel
{
    public const string SavingText = "Saving…";

    private readonly IRemoteActions _actions;
    private readonly bool _ignoreCase;
    private readonly IAppLog _log;
    private RemoteRow? _editing;
    private bool _nameEdited;
    private bool _urlEdited;
    private bool _filling;

    /// <param name="actions">What the dialog does to the repository.</param>
    /// <param name="ignoreCase">Compare names ignoring case; null for this platform's rule (<see cref="RefNameRules.NamesIgnoreCase"/>).</param>
    /// <param name="log">Where a failure to read the remotes is logged.</param>
    public RemotesDialogViewModel(IRemoteActions actions, bool? ignoreCase = null, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions;
        _ignoreCase = ignoreCase ?? RefNameRules.NamesIgnoreCase;
        _log = log ?? NullAppLog.Instance;
        Loading = ReloadAsync();
    }

    public override string Title => "Remotes";

    /// <summary>The remotes, in git's order.</summary>
    public ObservableCollection<RemoteRow> Rows { get; } = [];

    /// <summary>The last reading of the list: complete once the rows are shown.</summary>
    public Task Loading { get; private set; }

    /// <summary>The card shows the form (adding or editing) instead of the list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListShown), nameof(IsAdding), nameof(CancelText))]
    public partial bool IsFormShown { get; private set; }

    public bool IsListShown => !IsFormShown;

    /// <summary>The <c>CancelButton</c>'s label: "Close" in the list, "Cancel" in the form.</summary>
    public string CancelText => IsFormShown ? "Cancel" : "Close";

    /// <summary>The form adds a remote ("Fetch now" is shown).</summary>
    public bool IsAdding => IsFormShown && _editing is null;

    [ObservableProperty]
    public partial string RemoteName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemoteUrl { get; set; } = string.Empty;

    /// <summary>"Fetch now" (adding only), ticked to begin with.</summary>
    [ObservableProperty]
    public partial bool FetchNow { get; set; } = true;

    /// <summary>The form's message: the first rule the name or URL breaks, or git's refusal after Save; empty for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string RemoteError { get; private set; } = string.Empty;

    public bool HasError => RemoteError.Length > 0;

    /// <summary>Save is running: the form's buttons are disabled and Save says "Saving…".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveText), nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(CancelOrCloseCommand), nameof(AddCommand), nameof(EditCommand), nameof(RemoveCommand))]
    public partial bool IsSaving { get; private set; }

    public bool IsIdle => !IsSaving;

    /// <summary>"Save", or "Saving…" while it runs.</summary>
    public string SaveText => IsSaving ? SavingText : "Save";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool CanSave { get; private set; }

    /// <summary>Esc: the form goes back to the list; the list closes the dialog. Nothing while saving.</summary>
    public override void HandleEscape()
    {
        if (IsSaving)
        {
            return;
        }

        if (IsFormShown)
        {
            CancelForm();
            return;
        }

        Cancel();
    }

    partial void OnRemoteNameChanged(string value)
    {
        if (!_filling)
        {
            _nameEdited = true;
            Validate();
        }
    }

    partial void OnRemoteUrlChanged(string value)
    {
        if (!_filling)
        {
            _urlEdited = true;
            Validate();
        }
    }

    /// <summary>"Add remote": an empty form, "Fetch now" ticked.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Add() => ShowForm(null);

    /// <summary>A row's "Edit": the form with its name and its URL as configured.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Edit(RemoteRow row) => ShowForm(row);

    /// <summary>
    /// A row's "Remove": the confirmation on top, the removal, then the list again. The button
    /// stays enabled while it runs (the confirmation covers the dialog meanwhile), so it keeps the
    /// focus, which comes back to it when the confirmation closes (D104).
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle), AllowConcurrentExecutions = true)]
    private async Task Remove(RemoteRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            await _actions.RemoveRemoteAsync(row.Name);
        }
        catch (Exception ex)
        {
            _log.Error($"Removing the remote {row.Name} failed.", ex);
        }

        await ReloadAsync();
    }

    /// <summary>
    /// The card's <c>CancelButton</c>: in the form "Cancel", back to the list with nothing
    /// changed; in the list "Close", which closes the dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void CancelOrClose()
    {
        if (IsFormShown)
        {
            CancelForm();
        }
        else
        {
            Cancel();
        }
    }

    private void CancelForm()
    {
        IsFormShown = false;
        _editing = null;
        RemoteError = string.Empty;
    }

    /// <summary>
    /// "Save": adds the remote, or renames it and changes its URL, and waits. When the rename
    /// succeeds and the URL change fails, the form goes on editing the remote under its new name
    /// (the list shows it too), so that Save again only changes the URL.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveNow))]
    private async Task Save()
    {
        var (editing, name, url) = (_editing, RemoteName, RemoteUrl);
        IsSaving = true;
        string? error;
        var renamed = false;
        try
        {
            if (editing is null)
            {
                error = await _actions.AddRemoteAsync(name, url, FetchNow);
            }
            else
            {
                error = null;
                if (!string.Equals(name, editing.Name, StringComparison.Ordinal))
                {
                    error = await _actions.RenameRemoteAsync(editing.Name, name);
                    if (error is null)
                    {
                        renamed = true;
                        editing = editing with { Name = name };
                        _editing = editing;
                    }
                }

                if (error is null && !string.Equals(url, editing.Url, StringComparison.Ordinal))
                {
                    error = await _actions.SetRemoteUrlAsync(name, url);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Saving the remote {name} failed.", ex);
            error = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }

        if (error is not null)
        {
            if (renamed)
            {
                await ReloadAsync();
            }

            RemoteError = error;
            return;
        }

        IsFormShown = false;
        _editing = null;
        RemoteError = string.Empty;
        await ReloadAsync();
    }

    private bool CanSaveNow() => CanSave && !IsSaving;

    private void ShowForm(RemoteRow? row)
    {
        _editing = row;
        _filling = true;
        RemoteName = row?.Name ?? string.Empty;
        RemoteUrl = row?.Url ?? string.Empty;
        FetchNow = true;
        _filling = false;
        (_nameEdited, _urlEdited) = (false, false);
        IsFormShown = true;
        OnPropertyChanged(nameof(IsAdding));
        Validate();
    }

    private void Validate()
    {
        var others = Rows.Select(row => row.Name).Where(other => _editing is null || !string.Equals(other, _editing.Name, StringComparison.Ordinal));
        var nameError = DialogText.RemoteNameError(RemoteName, others, _ignoreCase);
        var urlError = RemoteUrl.Length == 0 ? DialogText.EnterUrl : null;
        var shown = (nameError, urlError) switch
        {
            ({ } error, _) when error != DialogText.EnterName || _nameEdited => error,
            (_, { } error) when _urlEdited => error,
            _ => null,
        };
        RemoteError = shown ?? string.Empty;
        var changed = _editing is null
            || !string.Equals(RemoteName, _editing.Name, StringComparison.Ordinal)
            || !string.Equals(RemoteUrl, _editing.Url, StringComparison.Ordinal);
        CanSave = nameError is null && urlError is null && changed;
    }

    private async Task ReloadAsync()
    {
        try
        {
            var remotes = await _actions.ReadRemotesAsync();
            var rows = remotes.Select(remote => new RemoteRow(remote.Name, remote.FetchUrl, remote.ShownUrl)).ToList();

            // Updated in place: a row that did not change keeps its view, and with it the focus
            // its button may have (a cancelled Remove gives the focus back to it, D104).
            for (var i = Rows.Count - 1; i >= 0; i--)
            {
                if (!rows.Contains(Rows[i]))
                {
                    Rows.RemoveAt(i);
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (i >= Rows.Count || Rows[i] != rows[i])
                {
                    Rows.Insert(i, rows[i]);
                }
            }

            while (Rows.Count > rows.Count)
            {
                Rows.RemoveAt(Rows.Count - 1);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Reading the remotes failed.", ex);
        }
    }

    /// <summary>Reads the remotes again and shows them; <see cref="Loading"/> completes then.</summary>
    public Task RefreshAsync()
    {
        Loading = ReloadAsync();
        return Loading;
    }
}
