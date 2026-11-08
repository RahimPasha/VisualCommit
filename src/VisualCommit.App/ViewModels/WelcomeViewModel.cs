using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The welcome page of a tab without a repository (D42): Open, Clone and Init, the clone form
/// with its progress, the workspaces (docs/phase-6-design.md 13.1, D188) and the recent
/// repositories. Since phase 7a the form's "From GitHub…" opens the repository browser while an
/// account is signed in (docs/phase-7a-design.md 9.3, D261, D302); since phase 7b the button names
/// the one signed-in provider, or "From an account…" with two or three (docs/phase-7b-design.md 11.3,
/// D354).
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject, IDisposable
{
    private readonly RepoTabViewModel _tab;
    private readonly TabServices _services;
    private CancellationTokenSource? _clone;
    private Task? _cloneTask;
    private bool _nameTyped;
    private bool _settingName;

    public WelcomeViewModel(RepoTabViewModel tab, TabServices services)
    {
        _tab = tab;
        _services = services;
        Recent.CollectionChanged += OnRecentChanged;
        Workspaces.CollectionChanged += OnWorkspacesChanged;
        _services.Workspaces.Changed += OnWorkspaceStoreChanged;
        _services.Hosting.AccountChanged += OnAccountChanged;
    }

    /// <summary>The recent repositories, newest first. Shared by every tab.</summary>
    public ObservableCollection<RecentRepositoryItem> Recent => _services.Recent.Items;

    public bool HasRecent => Recent.Count > 0;

    /// <summary>The workspaces, by name (13.1). Shared by every tab, as <see cref="Recent"/> is.</summary>
    public ObservableCollection<WorkspaceItem> Workspaces => _services.WorkspaceItems;

    /// <summary>The list shows; <c>No workspaces</c> shows while it is empty.</summary>
    public bool HasWorkspaces => Workspaces.Count > 0;

    /// <summary>What the page says about <c>workspaces.json</c> (1.3): a file set aside, one that could not be read, a newer version's, a failed save; or null.</summary>
    public string? WorkspacesNotice => _services.Workspaces.Notice;

    public bool HasWorkspacesNotice => !string.IsNullOrEmpty(WorkspacesNotice);

    /// <summary>The notice is a failed save's, in the warning tone (13.1); the others are secondary.</summary>
    public bool IsWorkspacesNoticeWarning => HasWorkspacesNotice && _services.Workspaces.SaveFailed;

    /// <summary>Workspaces can be created, renamed and deleted (the store is not read-only, 1.5).</summary>
    public bool CanEditWorkspaces => WorkspacesEditReason is null;

    /// <summary>Why they cannot: the store's read-only reason, or null.</summary>
    public string? WorkspacesEditReason => _services.WorkspaceCommands.EditReason;

    /// <summary><c>NewWorkspaceButton</c>'s tooltip and help text: what it does, or the read-only reason while disabled.</summary>
    public string NewWorkspaceToolTip => WorkspacesEditReason ?? WorkspaceTexts.NewWorkspaceToolTip;

    /// <summary>A workspace item's menu (13.1): <c>Open</c>, <c>Rename workspace…</c>, <c>Delete workspace…</c>.</summary>
    public MenuModel MenuFor(WorkspaceItem item) => WorkspaceMenus.ForWelcomeItem(item, _services.WorkspaceCommands, _tab);

    /// <summary>Why the last Open, Init or Clone failed, in git's words where git said it; or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>The clone form shows instead of the buttons and the recent list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsButtons))]
    public partial bool IsCloneFormOpen { get; private set; }

    public bool ShowsButtons => !IsCloneFormOpen;

    [ObservableProperty]
    public partial string CloneUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CloneParent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CloneName { get; set; } = string.Empty;

    /// <summary>A clone is running: the fields are disabled and the progress shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditClone))]
    [NotifyCanExecuteChangedFor(nameof(StartCloneCommand), nameof(CloseCloneFormCommand), nameof(BrowseCommand))]
    public partial bool IsCloning { get; private set; }

    public bool CanEditClone => !IsCloning;

    /// <summary>
    /// The browse button shows at the URL row's end only while an account of any provider is signed
    /// in (D261, D354); "needs sign-in" counts as none (D302). Collapsed, it takes no room, so the form
    /// is laid out as before.
    /// </summary>
    public bool ShowsBrowseButton => SignedInProviders().Count > 0;

    /// <summary>
    /// The button's text and automation name (11.3, D354): <see cref="OperationTexts.CloneTexts.FromButtonOf"/>
    /// of the one signed-in provider (7a's <c>From GitHub…</c> for GitHub), <c>From an account…</c> with
    /// two or three; 7a's text while none is (the button is collapsed then).
    /// </summary>
    public string BrowseButtonText => SignedInProviders() switch
    {
        [var one] => OperationTexts.CloneTexts.FromButtonOf(one),
        { Count: > 1 } => OperationTexts.CloneTexts.FromAnAccount,
        _ => OperationTexts.CloneTexts.FromGitHubButton,
    };

    /// <summary>The current stage's percentage, 0 to 100.</summary>
    [ObservableProperty]
    public partial double CloneProgressValue { get; private set; }

    /// <summary>The stage and percentage as git reports them, such as "Receiving objects 45%".</summary>
    [ObservableProperty]
    public partial string CloneProgressText { get; private set; } = string.Empty;

    /// <summary>
    /// The clone that is running, or null: the app waits for it to clean up when it closes, and the
    /// tab is not empty meanwhile. From Clone's press on: while git's configuration is read before git
    /// starts (9.3) a completed task stands in, so that nothing opens in the tab then and the close
    /// has nothing to wait for (the tab's <see cref="Dispose"/> cancels the read).
    /// </summary>
    public Task? RunningClone => _cloneTask is { IsCompleted: false } running ? running : IsCloning ? Task.CompletedTask : null;

    /// <summary>Shows a failure under the buttons.</summary>
    public void ShowError(string message) => ErrorText = message;

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _services.Folders.PickFolderAsync(OpenFolderTitle);
        if (path is null)
        {
            return;
        }

        // As Open repository… and its key (5.10, D215: the button's tooltip names that key): the tab
        // that already shows the folder is activated, and nothing opens it again.
        if (_services.ActivateTabShowing?.Invoke(path) == true)
        {
            return;
        }

        ErrorText = null;
        await _tab.OpenAsync(path);
    }

    /// <summary>The folder dialog's title of Open (D42), which the palette's and the key's Open ask with too (docs/phase-6-design.md 5.10).</summary>
    public const string OpenFolderTitle = "Open a repository";

    /// <summary>The folder dialog's title of Init (D42), which the palette's and the key's Init ask with too (docs/phase-6-design.md 5.10).</summary>
    public const string InitFolderTitle = "Choose a folder for the new repository";

    [RelayCommand]
    private async Task InitAsync()
    {
        var path = await _services.Folders.PickFolderAsync(InitFolderTitle);
        if (path is null)
        {
            return;
        }

        await InitAtAsync(path);
    }

    /// <summary>
    /// Makes a repository in <paramref name="path"/> and shows it in this page's tab: Init after
    /// its folder dialog, which the palette's and the key's Init call with their own dialog's answer
    /// (docs/phase-6-design.md 5.10). A failure shows under the buttons.
    /// </summary>
    public async Task InitAtAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ErrorText = null;
        try
        {
            var repository = await _services.Repositories.InitAsync(path);
            await _tab.ShowAsync(repository);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _services.Log.Error($"Initialising a repository in {path} failed.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentRepositoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        ErrorText = null;
        await _tab.OpenAsync(item.Path);
    }

    /// <summary><c>New workspace…</c> (1.6): the name dialog, then the workspace's dashboard takes this page's tab (2.2).</summary>
    [RelayCommand(CanExecute = nameof(CanEditWorkspaces))]
    private Task NewWorkspaceAsync() => _services.WorkspaceCommands.CreateAsync(_tab);

    /// <summary>A click, Enter or Space on a workspace item opens it from this page's tab (D188, 2.2).</summary>
    [RelayCommand]
    private void OpenWorkspace(WorkspaceItem? item)
    {
        if (item is not null)
        {
            _services.WorkspaceCommands.Open(item.Id, _tab);
        }
    }

    [RelayCommand]
    private void OpenCloneForm()
    {
        ErrorText = null;
        IsCloneFormOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditClone))]
    private void CloseCloneForm()
    {
        ErrorText = null;
        IsCloneFormOpen = false;
    }

    [RelayCommand]
    private async Task BrowseCloneParentAsync()
    {
        var path = await _services.Folders.PickFolderAsync("Choose the folder to clone into");
        if (path is not null)
        {
            CloneParent = path;
        }
    }

    /// <summary>
    /// The browse button and the palette's <c>Clone from …</c> (7a design 9.3; docs/phase-7b-design.md
    /// 11.3, 11.5, D354): the repository browser over the form on <paramref name="provider"/>, or the
    /// first signed-in provider in D353's order for null (the button), with a switch between the
    /// signed-in providers; nothing when none is signed in or <paramref name="provider"/> is not. Its
    /// first load starts before it shows (as <c>WorkspaceCommands</c>' search does); a chosen
    /// repository's clone URL fills the URL box (and so the folder name, unless the user typed one),
    /// unless a clone started meanwhile. The user then presses Clone as for a typed URL. The palette
    /// runs it through <see cref="BrowseCommand"/> with its provider, as 7a's ran the button's command,
    /// so the button is drawn disabled under every browser over the form (open point 21's ruling).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditClone))]
    public async Task BrowseAsync(HostingProvider? provider)
    {
        var hosting = _services.Hosting;
        var providers = SignedInProviders();
        if (providers.Count == 0 || (provider is { } asked && !providers.Contains(asked)))
        {
            return;
        }

        var dialog = new CloneFromGitHubDialogViewModel(
            providers,
            provider ?? providers[0],
            hosting.ListRepositoriesAsync,
            _services.Log,
            each => hosting.AccountOf(each)?.Account.Login);
        _ = dialog.Start();
        var url = await _services.DialogHost.ShowAsync(dialog);
        if (url is not null && !IsCloning)
        {
            ErrorText = null;
            CloneUrl = url;
        }
    }

    /// <summary>The providers whose accounts are signed in, in D353's order; one needing sign-in is none (D302).</summary>
    private List<HostingProvider> SignedInProviders()
    {
        var hosting = _services.Hosting;
        return [.. HostingProviders.InAccountsOrder.Where(provider => hosting.AccountOf(provider) is { State: AccountState.SignedIn })];
    }

    [RelayCommand(CanExecute = nameof(CanEditClone))]
    private async Task StartCloneAsync()
    {
        var url = CloneUrl.Trim();
        var parent = CloneParent.Trim();
        var name = CloneName.Trim();
        if (url.Length == 0 || parent.Length == 0 || name.Length == 0)
        {
            ErrorText = "Fill in the repository URL, the parent folder and the folder name.";
            return;
        }

        ErrorText = null;
        IsCloning = true;
        CloneProgressValue = 0;
        CloneProgressText = "Starting";
        _clone = new CancellationTokenSource();
        var progress = new Progress<CloneProgress>(report =>
        {
            if (report.Percent is { } percent)
            {
                CloneProgressValue = percent;
                CloneProgressText = $"{report.Stage} {percent}%";
            }
            else
            {
                CloneProgressText = report.Stage;
            }
        });

        // Git's and ssh's prompts for the clone come to the app's dialogs (phase 3 design 3.3); a
        // clone runs outside any operation queue, so it opens its own prompt scope. A GitHub URL is
        // bound to the signed-in account after the user's insteadOf rules (9.3, D331).
        PromptScope? prompts = null;
        try
        {
            var bindings = await BindingsForCloneAsync(url, _clone.Token);
            prompts = _services.Prompts.Begin("Clone", name, PromptMode.Ask, bindings: bindings);
            var cloning = _services.Repositories.CloneAsync(url, System.IO.Path.Combine(parent, name), progress, _clone.Token, prompts.Environment);
            _cloneTask = cloning;
            var repository = await cloning;
            IsCloning = false;
            IsCloneFormOpen = false;
            await _tab.ShowAsync(repository);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Clone cancelled.";
        }
        catch (GitException) when (prompts?.UserCancelled == true)
        {
            // The user cancelled a prompt, which made git fail: that is a cancel, not an error.
            ErrorText = "Clone cancelled.";
        }
        catch (Exception ex)
        {
            if (ex is not GitException)
            {
                _services.Log.Error($"Cloning {GitCommand.HideCredentials(url)} failed.", ex);
            }

            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
        finally
        {
            prompts?.Dispose();
            IsCloning = false;
            _clone.Dispose();
            _clone = null;
        }
    }

    [RelayCommand]
    private void CancelClone() => _clone?.Cancel();

    /// <summary>
    /// The bindings of the clone of <paramref name="url"/> (9.3, D331): none unless the account binds
    /// the URL as typed; else the user's <c>insteadOf</c> rules are read once with the outside read in
    /// the data folder (local and worktree entries dropped: <c>git clone</c> applies none of a
    /// repository around its folder) and the URL git will use is bound. A rule that rewrites it to
    /// another user leaves it unbound (D82's order). No configuration service, or a failed read, binds
    /// nothing and is logged once. Signed out nothing is read, so no earlier count of git calls moves.
    /// <para>
    /// The outside read sees no file of a conditional include, while <c>git clone</c> applies those of
    /// the new folder and of its URL (fixed at phase 7b's review before the gate): when the read lists
    /// any, <see cref="WhyIncludesUnbindAsync"/> reads their files, and a reason it gives binds nothing
    /// (D82's order) with one Debug line. Without such a directive no further git call is made.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<CredentialBinding>> BindingsForCloneAsync(string url, CancellationToken cancellationToken)
    {
        var hosting = _services.Hosting;
        if (hosting.BindingsForClone(url).Count == 0)
        {
            return [];
        }

        IGitConfiguration settings;
        List<GitConfigValue> outside;
        string? effective;
        try
        {
            settings = _services.GitSettings ?? throw new InvalidOperationException("No git configuration service.");
            var read = await settings.ReadAsync(null, cancellationToken);
            outside = [.. read.Values.Where(value => value.Scope is not (GitConfigScope.Local or GitConfigScope.Worktree))];
            effective = CloneUrlAfterRules(outside, url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _services.Log.Warning($"Cloning {GitCommand.HideCredentials(url)}: git's configuration could not be read; the clone goes without the account's token.", ex);
            return [];
        }

        if (effective is null)
        {
            return [];
        }

        if (await WhyIncludesUnbindAsync(settings, outside, url, effective, cancellationToken) is { } why)
        {
            _services.Log.Debug($"Cloning: {why}; the clone goes without the account's token (D82's order).");
            return [];
        }

        return hosting.BindingsForClone(effective);
    }

    /// <summary>
    /// Why a conditional include of <paramref name="outside"/> (<see cref="GitConfigIncludes.IsConditional"/>,
    /// whatever its condition) keeps the clone of <paramref name="url"/> unbound; null when none does, or
    /// there is none (then no git call is made). Each directive's file is read
    /// (<see cref="IGitConfiguration.ReadIncludedAsync"/>): one git cannot read, one that holds a
    /// conditional directive of its own (its condition is the new repository's to judge), or one whose
    /// <c>url.</c> rules, read where git reads them, right after the directive, give the URL another
    /// result than the outside rules alone, is a reason. Each include is judged with the outside rules
    /// alone, since only the new repository knows which conditions hold: whichever rule wins among
    /// several includes also wins beside the outside rules alone. A missing file counts as empty, as
    /// git ignores it. The reason names no path or URL.
    /// </summary>
    private static async Task<string?> WhyIncludesUnbindAsync(IGitConfiguration settings, List<GitConfigValue> outside, string url, string effective, CancellationToken cancellationToken)
    {
        for (var i = 0; i < outside.Count; i++)
        {
            if (!GitConfigIncludes.IsConditional(outside[i]))
            {
                continue;
            }

            IReadOnlyList<GitConfigValue> included;
            try
            {
                included = await settings.ReadIncludedAsync(outside[i], cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return "a conditional include of git's configuration could not be read";
            }

            if (included.Any(GitConfigIncludes.IsConditional))
            {
                return "a conditional include of git's configuration holds a conditional include of its own";
            }

            if (CloneUrlAfterRules([.. outside[..(i + 1)], .. included, .. outside[(i + 1)..]], url) != effective)
            {
                return "a conditional include of git's configuration rewrites the clone's URL";
            }
        }

        return null;
    }

    /// <summary>The URL git clones <paramref name="url"/> from after the <c>insteadOf</c> rules of <paramref name="values"/>, in their order (<see cref="UrlRewrites"/>).</summary>
    private static string? CloneUrlAfterRules(IEnumerable<GitConfigValue> values, string url) =>
        UrlRewrites.Parse(values.Where(value => value.Value is not null).Select(value => (value.Key, value.Value!)))
            .Apply([new RemoteUrl("origin", url, false)]) is [{ Fetch: [var effective, ..] }]
            ? effective
            : null;

    public void Dispose()
    {
        Recent.CollectionChanged -= OnRecentChanged;
        Workspaces.CollectionChanged -= OnWorkspacesChanged;
        _services.Workspaces.Changed -= OnWorkspaceStoreChanged;
        _services.Hosting.AccountChanged -= OnAccountChanged;
        _clone?.Cancel();
    }

    partial void OnCloneUrlChanged(string value)
    {
        if (_nameTyped)
        {
            return;
        }

        _settingName = true;
        CloneName = NameFromUrl(value);
        _settingName = false;
    }

    partial void OnCloneNameChanged(string value)
    {
        if (!_settingName)
        {
            _nameTyped = value.Length > 0;
        }
    }

    /// <summary>The last part of a URL or path, without ".git": what git itself would name the folder.</summary>
    public static string NameFromUrl(string url)
    {
        var trimmed = url.Trim().TrimEnd('/', '\\');
        var last = trimmed[(trimmed.LastIndexOfAny(['/', '\\', ':']) + 1)..];
        return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? last[..^4] : last;
    }

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasRecent));

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasWorkspaces));

    /// <summary>A sign-in, a sign-out or a refusal of any provider shows, hides or renames the browse button (D261, D302, D354); raised on the UI thread.</summary>
    private void OnAccountChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowsBrowseButton));
        OnPropertyChanged(nameof(BrowseButtonText));
    }

    /// <summary>A change of the store can set or clear its notice (a failed save, then one that succeeded, 1.3).</summary>
    private void OnWorkspaceStoreChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WorkspacesNotice));
        OnPropertyChanged(nameof(HasWorkspacesNotice));
        OnPropertyChanged(nameof(IsWorkspacesNoticeWarning));
    }
}
