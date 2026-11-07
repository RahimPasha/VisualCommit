using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
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
/// <param name="CloneUrl">
/// The service's clone URL without any user part (7a's ruling 5), but an Azure DevOps URL whose user
/// part is exactly its organization (<see cref="HostedRepository.OwnerRoot"/>, without case and without
/// a password), which is kept as the API gave it (docs/phase-7b-design.md 11.4; ruling 15).
/// </param>
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
            KeepsUserPart(repository) ? repository.CloneUrl.AbsoluteUri : GitCommand.RemoveCredentials(repository.CloneUrl.AbsoluteUri));
    }

    /// <summary>An Azure DevOps clone URL whose user part is its organization, without a password (ruling 15).</summary>
    private static bool KeepsUserPart(RepositoryInfo repository) =>
        repository.Repository.Provider == HostingProvider.AzureDevOps
        && repository.CloneUrl.IsAbsoluteUri
        && repository.CloneUrl.UserInfo is { Length: > 0 } user
        && !user.Contains(':', StringComparison.Ordinal)
        && user.Equals(repository.Repository.OwnerRoot, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One provider's button of the browser's switch (docs/phase-7b-design.md 11.4, D354): its display
/// name, its automation id <c>CloneFromGitHubProvider&lt;provider&gt;</c> (1.7, D362) and whether it is
/// the one shown (the flat button's <c>selected</c> look).
/// </summary>
public sealed partial class CloneProviderChoice : ObservableObject
{
    internal CloneProviderChoice(HostingProvider provider, Action<HostingProvider> show)
    {
        Provider = provider;
        ShowCommand = new RelayCommand(() => show(provider));
    }

    public HostingProvider Provider { get; }

    /// <summary>The button's text and automation name: <c>GitHub</c>, <c>GitLab</c>, <c>Azure DevOps</c>.</summary>
    public string Name => HostingTexts.Name(Provider);

    /// <summary><c>CloneFromGitHubProviderGitHub</c>, <c>…GitLab</c>, <c>…AzureDevOps</c>.</summary>
    public string AutomationId => "CloneFromGitHubProvider" + Provider;

    /// <summary>Shows this provider's list.</summary>
    public IRelayCommand ShowCommand { get; }

    /// <summary>This provider is the one shown.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// The repository browser "Clone from GitHub" (docs/phase-7a-design.md 9.4, D301), over the clone
/// form: the signed-in account's repositories in its service's order, a filter on the full name and
/// the detail, and "Choose", which answers the chosen row's clone URL (null when cancelled). A
/// sibling of <see cref="PickDialogViewModel"/>, whose filter it follows. Since phase 7b it serves
/// every signed-in provider (docs/phase-7b-design.md 11.4, D354): with two or three a switch shows
/// one at a time, the title naming it; each provider's list is asked once, when it is first shown,
/// and its rows, status, error and lines are kept and shown while it is; a switch keeps the filter
/// and clears the selection. <see cref="Start"/> begins the shown provider's load; every load runs on
/// the dialog's one cancellation, which every close cancels (Choose as well as Cancel, Esc and the
/// window's <c>CancelAll</c>, D234), so that no provider's list goes on paging with its token for a
/// dialog that has closed; an answer is applied on the thread that started it (the UI thread), one
/// for a provider not shown changes nothing on screen, and one that arrives after the dialog closed
/// changes nothing.
/// </summary>
public sealed partial class CloneFromGitHubDialogViewModel : DialogViewModel<string?>
{
    private readonly Func<HostingProvider, CancellationToken, Task<HostingResult<RepositoryList>>> _list;
    private readonly IAppLog _log;
    private readonly Func<HostingProvider, string?>? _login;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Dictionary<HostingProvider, ProviderList> _lists = [];
    private IReadOnlyList<GitHubRepositoryRow> _all = [];
    private HostingProvider _shown;

    /// <summary>7a's browser, GitHub alone (docs/phase-7a-design.md 9.4): no switch.</summary>
    /// <param name="list">The account's list (the session's <c>ListRepositoriesAsync</c>).</param>
    /// <param name="log">The app's log: a failure once as a warning, an exception as an error.</param>
    /// <param name="login">The signed-in account's login when the list fails (open point 13), for <see cref="HostingTexts.Describe"/>; null for none.</param>
    public CloneFromGitHubDialogViewModel(Func<CancellationToken, Task<HostingResult<RepositoryList>>> list, IAppLog log, Func<string?>? login = null)
        : this([HostingProvider.GitHub], HostingProvider.GitHub, GitHubAlone(list), log, login is null ? null : _ => login())
    {
    }

    /// <summary>The browser over the signed-in <paramref name="providers"/>, showing <paramref name="provider"/> first (11.4).</summary>
    /// <param name="providers">The providers whose accounts are signed in; shown in D353's order, a switch with two or more.</param>
    /// <param name="provider">The one shown first, among <paramref name="providers"/>.</param>
    /// <param name="list">A provider's list (the session's <c>ListRepositoriesAsync(provider, …)</c>).</param>
    /// <param name="log">The app's log: a failure once as a warning, an exception as an error.</param>
    /// <param name="login">A provider's signed-in login (the organization on Azure DevOps) when its list fails, for <see cref="HostingTexts.Describe"/>; null for none.</param>
    public CloneFromGitHubDialogViewModel(
        IReadOnlyList<HostingProvider> providers,
        HostingProvider provider,
        Func<HostingProvider, CancellationToken, Task<HostingResult<RepositoryList>>> list,
        IAppLog log,
        Func<HostingProvider, string?>? login = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(log);
        Providers = [.. HostingProviders.InAccountsOrder.Where(providers.Contains)];
        if (!Providers.Contains(provider))
        {
            throw new ArgumentException("The provider shown first must be one of the providers.", nameof(provider));
        }

        _list = list;
        _log = log;
        _login = login;
        ProviderChoices = [.. Providers.Select(each => new CloneProviderChoice(each, ShowProvider))];
        _shown = provider;
        foreach (var choice in ProviderChoices)
        {
            choice.IsSelected = choice.Provider == provider;
        }

        // Every close ends every load: Choose closes the dialog while another provider's list may
        // still be loading (or paging), whose answer nothing could use (RunAsync returns when closed).
        Closing += (_, _) => _cancel.Cancel();
    }

    /// <summary>The signed-in providers, in D353's order.</summary>
    public IReadOnlyList<HostingProvider> Providers { get; }

    /// <summary>The switch's buttons, one per provider of <see cref="Providers"/>.</summary>
    public IReadOnlyList<CloneProviderChoice> ProviderChoices { get; }

    /// <summary>The switch shows with two or more providers; with one the card is 7a's.</summary>
    public bool ShowsSwitch => Providers.Count >= 2;

    /// <summary>The provider shown; setting it to one of <see cref="Providers"/> shows that provider's list, asked the first time it shows.</summary>
    public HostingProvider Provider
    {
        get => _shown;
        set
        {
            if (value == _shown || !Providers.Contains(value))
            {
                return;
            }

            _shown = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
            foreach (var choice in ProviderChoices)
            {
                choice.IsSelected = choice.Provider == value;
            }

            Selected = null;
            Begin(value);
            ShowState(value);
        }
    }

    public override string Title => CloneTexts.DialogTitleOf(Provider);

    /// <summary>The rows the filter lets through, in the list's order; rebuilt in place.</summary>
    public ObservableCollection<GitHubRepositoryRow> Rows { get; } = [];

    /// <summary>Every row the shown provider's list gave, whatever the filter lets through.</summary>
    public IReadOnlyList<GitHubRepositoryRow> AllRows => _all;

    /// <summary>What the filter box holds; enabled while loading, so it filters the rows when they come; kept by a switch.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>The selected row, or null; a switch clears it.</summary>
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

    /// <summary>The cap's line when more repositories exist than were listed (<see cref="CloneTexts.MoreExistOf"/>), else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMore))]
    public partial string? MoreText { get; private set; }

    /// <summary>Single sign-on's line when some organisations' repositories are hidden (GitHub's alone), else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSso))]
    public partial string? SsoText { get; private set; }

    public bool ShowsStatus => StatusText is not null;

    public bool ShowsError => ErrorText is not null;

    public bool ShowsMore => MoreText is not null;

    public bool ShowsSso => SsoText is not null;

    /// <summary>The shown provider's load; a completed task before it begins.</summary>
    public Task Load => LoadOf(Provider);

    /// <summary>A provider's load; a completed task before it begins.</summary>
    public Task LoadOf(HostingProvider provider) => _lists.TryGetValue(provider, out var state) ? state.Load : Task.CompletedTask;

    protected override string? CancelledResult => null;

    /// <summary>Begins the shown provider's load (once) and returns it (<see cref="Load"/>).</summary>
    public Task Start() => Begin(Provider).Load;

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

    private void ShowProvider(HostingProvider provider)
    {
        if (!IsClosed)
        {
            Provider = provider;
        }
    }

    /// <summary>The provider's list, its load begun the first time.</summary>
    private ProviderList Begin(HostingProvider provider)
    {
        if (!_lists.TryGetValue(provider, out var state))
        {
            state = new ProviderList();
            _lists[provider] = state;
            state.Load = RunAsync(provider, state);
        }

        return state;
    }

    private async Task RunAsync(HostingProvider provider, ProviderList state)
    {
        var token = _cancel.Token;
        try
        {
            var result = await _list(provider, token);
            if (IsClosed)
            {
                return;
            }

            if (result is { Failure: null, Value: { } list })
            {
                state.Listed(
                    [.. list.Repositories.Select(GitHubRepositoryRow.Of)],
                    list.MoreExist ? CloneTexts.MoreExistOf(provider) : null,
                    list.SomeHiddenBySso && provider == HostingProvider.GitHub ? CloneTexts.SomeHiddenBySso : null);
            }
            else if (result.Failure is { } failure)
            {
                var request = failure.RequestId is null ? string.Empty : $" (request {failure.RequestId})";
                _log.Warning($"{CloneTexts.ListingFailedOf(provider)}: {failure.Kind}: {failure.Message}{request}");
                state.Failed(HostingTexts.Describe(failure, _login?.Invoke(provider)));
            }
            else
            {
                _log.Error($"Listing {HostingTexts.Name(provider)} repositories gave neither a list nor a failure.");
                state.Failed(CloneTexts.Unexpected);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Cancel or Esc: the dialog has closed, and nothing is shown.
            return;
        }
        catch (Exception ex)
        {
            _log.Error($"{CloneTexts.ListingFailedOf(provider)}.", ex);
            if (IsClosed)
            {
                return;
            }

            state.Failed(CloneTexts.Unexpected);
        }

        // An answer for a provider not shown is kept and changes nothing on screen (11.4).
        if (Provider == provider)
        {
            ShowState(provider);
        }
    }

    /// <summary>Shows a provider's kept rows, status, error and lines (loading before its answer).</summary>
    private void ShowState(HostingProvider provider)
    {
        var state = _lists.TryGetValue(provider, out var kept) ? kept : ProviderList.Waiting;
        _all = state.All;
        StatusText = state.Status;
        ErrorText = state.Error;
        MoreText = state.More;
        SsoText = state.Sso;
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

    /// <summary>One provider's list as the dialog keeps it: loading until its answer.</summary>
    private sealed class ProviderList
    {
        /// <summary>A provider not asked yet: loading, as 7a's dialog before its start.</summary>
        public static readonly ProviderList Waiting = new();

        public Task Load { get; set; } = Task.CompletedTask;

        public IReadOnlyList<GitHubRepositoryRow> All { get; private set; } = [];

        public string? Status { get; private set; } = CloneTexts.Loading;

        public string? Error { get; private set; }

        public string? More { get; private set; }

        public string? Sso { get; private set; }

        public void Listed(IReadOnlyList<GitHubRepositoryRow> rows, string? more, string? sso)
        {
            All = rows;
            Status = rows.Count == 0 ? CloneTexts.Empty : null;
            More = more;
            Sso = sso;
        }

        public void Failed(string text)
        {
            All = [];
            Status = null;
            Error = text;
        }
    }

    private static Func<HostingProvider, CancellationToken, Task<HostingResult<RepositoryList>>> GitHubAlone(Func<CancellationToken, Task<HostingResult<RepositoryList>>> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return (_, cancellationToken) => list(cancellationToken);
    }
}
