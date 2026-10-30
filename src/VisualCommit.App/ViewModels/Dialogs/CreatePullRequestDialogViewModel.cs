using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Logging;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// One end of a pull request as the form shows it (docs/phase-7a-design.md 12.4, D318): its text
/// (<c>topic/no-pr</c>, <c>origin/main</c>, <c>octo:main</c>) and its repository on GitHub
/// (<c>octo/hello</c>).
/// </summary>
/// <param name="Text">What the form shows: a local branch's name, a remote branch's ref name, or GitHub's <c>owner:branch</c>.</param>
/// <param name="Repository">The repository's full name on GitHub.</param>
public sealed record PullRequestEnd(string Text, string Repository)
{
    /// <summary>The branch's name on GitHub (<c>main</c> for <c>origin/main</c>), for the same branch's text; null: <see cref="Text"/>.</summary>
    public string? Branch { get; init; }
}

/// <summary>What <see cref="ICreatePullRequestActions.StartAsync"/> found: the default base (A3), or why there is none.</summary>
public sealed record CreatePullRequestStart(PullRequestEnd? Base, string? Error);

/// <summary>How a Create ended for the form: created (it closes), or the error line and, after a 422 that found one open, its number.</summary>
public sealed record CreatePullRequestOutcome(bool Created, string? Error, int? ExistingNumber);

/// <summary>
/// What the form asks of the repository (12.4): it never runs git or HTTP itself. The create's
/// cancel window (ruling 59, design 17 item 8) is <see cref="CanCancelCreate"/>: while it is open
/// the form's Cancel cancels the operation through the queue (<see cref="CancelCreate"/>).
/// </summary>
public interface ICreatePullRequestActions
{
    /// <summary>The head: <c>topic/no-pr</c> · <c>octo/hello</c>.</summary>
    PullRequestEnd Head { get; }

    /// <summary>The head's tip's subject, or empty.</summary>
    string DefaultTitle { get; }

    /// <summary>A drop's target, or null.</summary>
    PullRequestEnd? GivenBase { get; }

    /// <summary>What Create pushes first, from the refs now (and 12.3 step 3's count); null when GitHub has the head as it is.</summary>
    string? HeadNote();

    /// <summary>The default base (A3).</summary>
    Task<CreatePullRequestStart> StartAsync(CancellationToken ct);

    /// <summary>The stacked Pick of the bases (12.3); null when it was cancelled.</summary>
    Task<PullRequestEnd?> ChooseBaseAsync(PullRequestEnd? current);

    /// <summary>Head and base are one branch of one repository (D318).</summary>
    bool IsSameBranch(PullRequestEnd baseEnd);

    /// <summary>The push when needed and the POST, as one operation of the queue (12.5).</summary>
    Task<CreatePullRequestOutcome> CreateAsync(PullRequestEnd baseEnd, string title, string body, bool draft);

    /// <summary>Opens the pull request the last <see cref="CreateAsync"/> found open.</summary>
    Task OpenExistingAsync();

    /// <summary>The running create's cancel window is open (12.5 step 1, the push): Cancel stops it (D315).</summary>
    bool CanCancelCreate { get; }

    /// <summary>Raised on the UI thread when <see cref="CanCancelCreate"/> changes.</summary>
    event EventHandler? CanCancelCreateChanged;

    /// <summary>Cancels the running create through the queue while its window is open; nothing otherwise.</summary>
    void CancelCreate();
}

/// <summary>
/// The "Create pull request" form (docs/phase-7a-design.md 12.4; D312, D313, D315, D318; ruling
/// 59): From the head and its note, Into the base and <c>Choose…</c>, the title, the description,
/// Draft, the error line and the existing pull request's button, Cancel and Create. States:
/// loading (A3 runs without a given base: Into reads <c>Reading GitHub…</c>; a base chosen
/// meanwhile wins), ready, creating (every control disabled, Create reads <c>Creating…</c>, Esc
/// ignored; Cancel enabled only while the create's cancel window is open, when it cancels the
/// operation and the form stays open with <c>Cancelled</c> on its error line), failed (the error
/// line, the controls enabled again, the head's note read again) and done (true). Answers false
/// when cancelled or after "Open pull request #n".
/// </summary>
public sealed partial class CreatePullRequestDialogViewModel : DialogViewModel<bool>
{
    private readonly ICreatePullRequestActions _actions;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _baseChosen;

    public CreatePullRequestDialogViewModel(ICreatePullRequestActions actions, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions;
        _log = log ?? NullAppLog.Instance;
        Head = actions.Head;
        PullRequestTitle = actions.DefaultTitle;
        HeadNote = actions.HeadNote();
        actions.CanCancelCreateChanged += OnCanCancelCreateChanged;
        if (actions.GivenBase is { } given)
        {
            Base = given;
            Starting = Task.CompletedTask;
        }
        else
        {
            IsReadingBase = true;
            Starting = StartAsync();
        }

        ShowSameBranch();
    }

    public override string Title => Texts.DialogTitle;

    /// <summary>The default base's read: complete once it is shown (or failed, or the form closed).</summary>
    public Task Starting { get; }

    public PullRequestEnd Head { get; }

    /// <summary>What Create pushes first, or null (<c>PullRequestHeadNote</c> hidden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHeadNote))]
    public partial string? HeadNote { get; private set; }

    public bool HasHeadNote => HeadNote is not null;

    /// <summary>The base, or null while it is read or after a failed read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaseText), nameof(BaseRepository))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial PullRequestEnd? Base { get; private set; }

    /// <summary>Into's text: the base's, <c>Reading GitHub…</c> while it is read, or empty.</summary>
    public string BaseText => Base?.Text ?? (IsReadingBase ? Texts.ReadingGitHub : string.Empty);

    public string BaseRepository => Base?.Repository ?? string.Empty;

    /// <summary>A3 runs and no base is chosen yet: Into reads <c>Reading GitHub…</c> in the secondary colour.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaseText))]
    public partial bool IsReadingBase { get; private set; }

    /// <summary>The pull request's title (<c>PullRequestTitle</c>): the head's subject to begin with.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string PullRequestTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDraft { get; set; }

    /// <summary>The error line, or null (<c>PullRequestError</c> hidden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>The open pull request a 422 found (D316): <c>PullRequestOpenExisting</c> shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExisting), nameof(OpenExistingText))]
    public partial int? ExistingNumber { get; private set; }

    public bool HasExisting => ExistingNumber is not null;

    public string OpenExistingText => ExistingNumber is { } number ? Texts.OpenExisting(number) : string.Empty;

    /// <summary>Create runs: every control is disabled and Create reads <c>Creating…</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CreateText), nameof(CreateToolTip))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand), nameof(ChooseBaseCommand), nameof(OpenExistingCommand), nameof(CancelFormCommand))]
    public partial bool IsCreating { get; private set; }

    /// <summary>The boxes and Draft are enabled.</summary>
    public bool IsIdle => !IsCreating;

    public string CreateText => IsCreating ? Texts.Creating : Texts.Create;

    /// <summary>Create's reason while it is disabled (its tooltip, <c>ToolTip.ShowOnDisabled</c>); null while it can run or runs.</summary>
    public string? CreateToolTip => IsCreating
        ? null
        : Base is not { } baseEnd
            ? Texts.ChooseBase
            : _actions.IsSameBranch(baseEnd)
                ? Texts.SameBranch(SameBranchName(baseEnd))
                : string.IsNullOrWhiteSpace(PullRequestTitle) ? Texts.EnterTitle : null;

    /// <summary>The form's Cancel can run: not creating, or creating with the create's cancel window open (ruling 59).</summary>
    public bool CanCancelForm => !IsCreating || _actions.CanCancelCreate;

    /// <summary>Esc does nothing while creating (12.4); otherwise it cancels.</summary>
    public override void HandleEscape()
    {
        if (!IsCreating)
        {
            Cancel();
        }
    }

    protected override bool CancelledResult => false;

    protected override void OnCancel()
    {
        _lifetime.Cancel();
        _actions.CanCancelCreateChanged -= OnCanCancelCreateChanged;
        base.OnCancel();
    }

    partial void OnPullRequestTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CreateToolTip));
        ClearError();
    }

    partial void OnDescriptionChanged(string value) => ClearError();

    partial void OnIsDraftChanged(bool value) => ClearError();

    partial void OnBaseChanged(PullRequestEnd? value)
    {
        OnPropertyChanged(nameof(CreateToolTip));
        ClearError();
    }

    /// <summary>Not creating, a base set, the title not blank and head and base not one branch (D318).</summary>
    private bool CanCreate() =>
        !IsCreating && Base is { } baseEnd && !string.IsNullOrWhiteSpace(PullRequestTitle) && !_actions.IsSameBranch(baseEnd);

    /// <summary>Create (Enter in the title, Mod+Enter in the description): the push when needed and the POST (12.5).</summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task Create()
    {
        if (!CanCreate() || Base is not { } baseEnd)
        {
            return;
        }

        IsCreating = true;
        CreatePullRequestOutcome outcome;
        try
        {
            outcome = await _actions.CreateAsync(baseEnd, PullRequestTitle.TrimEnd(), Description.TrimEnd(), IsDraft);
        }
        catch (Exception ex)
        {
            _log.Error("Creating a pull request failed unexpectedly.", ex);
            outcome = new CreatePullRequestOutcome(false, Texts.Operation(Head.Text).FailureSummary, null);
        }

        if (outcome.Created)
        {
            Complete(true);
            return;
        }

        IsCreating = false;
        HeadNote = _actions.HeadNote();
        Error = outcome.Error;
        ExistingNumber = outcome.ExistingNumber;
    }

    /// <summary><c>Choose…</c>: the stacked Pick (12.3, D104); a choice replaces the base, also one still being read.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task ChooseBase()
    {
        if (await _actions.ChooseBaseAsync(Base) is not { } chosen || IsClosed)
        {
            return;
        }

        _baseChosen = true;
        IsReadingBase = false;
        Base = chosen;
        ShowSameBranch();
    }

    /// <summary><c>Open pull request #n</c>: opens it and closes the form (false).</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task OpenExisting()
    {
        await _actions.OpenExistingAsync();
        Complete(false);
    }

    /// <summary>
    /// The card's Cancel: closes the form; while creating, only within the create's cancel window,
    /// when it cancels the operation and the form stays open (ruling 59).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelForm))]
    private void CancelForm()
    {
        if (!IsCreating)
        {
            Cancel();
        }
        else if (_actions.CanCancelCreate)
        {
            _actions.CancelCreate();
        }
    }

    private async Task StartAsync()
    {
        CreatePullRequestStart start;
        try
        {
            start = await _actions.StartAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _log.Error("Reading the default base of a pull request failed unexpectedly.", ex);
            start = new CreatePullRequestStart(null, Texts.Operation(Head.Text).FailureSummary);
        }

        if (IsClosed || _baseChosen)
        {
            return;
        }

        IsReadingBase = false;
        if (start.Base is { } found)
        {
            Base = found;
            ShowSameBranch();
        }
        else if (start.Error is { } error)
        {
            Error = error;
        }
    }

    private void OnCanCancelCreateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanCancelForm));
        CancelFormCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Clears the error line and the existing pull request's button; the same branch's line stays while it holds.</summary>
    private void ClearError()
    {
        ExistingNumber = null;
        Error = null;
        ShowSameBranch();
    }

    private void ShowSameBranch()
    {
        if (Base is { } baseEnd && _actions.IsSameBranch(baseEnd))
        {
            Error = Texts.SameBranch(SameBranchName(baseEnd));
        }
    }

    private static string SameBranchName(PullRequestEnd baseEnd) => baseEnd.Branch ?? baseEnd.Text;
}
