using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// What the settings screen works with (docs/phase-6-design.md 7.2): built by the shell's
/// <c>OpenSettings</c> each time the screen opens.
/// </summary>
/// <param name="Settings">The app's settings store.</param>
/// <param name="Theme">The theme as chosen now (the shell's <c>Theme</c>).</param>
/// <param name="UseTheme">The shell's <c>UseTheme</c> (11.6), Save's one way to change the theme.</param>
/// <param name="Repository">The active tab's repository, or null on an empty or a dashboard tab and while its refs are unread.</param>
/// <param name="Git">Git's configuration (8.1, <c>TabServices.GitSettings</c>); null without one.</param>
/// <param name="Tools">The tool catalogues (12.9).</param>
/// <param name="Shortcuts">The Keyboard shortcuts section's groups (6.11).</param>
/// <param name="Folders">The file dialog of Browse… (12.5).</param>
/// <param name="Dialogs">The window's dialog layer, where the Pick and error dialogs stack over the screen (D104).</param>
/// <param name="GitSettingsChanged">Tells the open tabs after a Save that wrote a git key (8.11); null for none.</param>
/// <param name="Log">The app's log.</param>
public sealed record SettingsScreenServices(
    ISettingsStore Settings,
    AppTheme Theme,
    Action<AppTheme> UseTheme,
    ISettingsRepository? Repository,
    IGitConfiguration? Git,
    ToolCatalogues Tools,
    IReadOnlyList<ShortcutGroup> Shortcuts,
    IFolderPicker Folders,
    DialogHostViewModel Dialogs,
    Action? GitSettingsChanged,
    IAppLog Log)
{
    /// <summary>The disk checks of Other…'s program, which Save makes off the UI thread (12.5).</summary>
    public EditorDiskChecks Disk { get; init; } = EditorDiskChecks.System;

    /// <summary>The platform whose editor rules apply; null for this one.</summary>
    public EditorPlatform? Platform { get; init; }

    /// <summary>Whether a file exists (a merge tool's path, 8.10), called off the UI thread only; null for the disk.</summary>
    public Func<string, bool>? FileExists { get; init; }
}

/// <summary>
/// The settings screen (<c>SettingsDialog</c>, docs/phase-6-design.md 7): a large dialog of the
/// dialog layer (D69) with four sections, General, Git, Tools and Keyboard shortcuts, behind a
/// section list. Its reads start when it opens (<see cref="Loaded"/>); changes apply at Save only
/// (7.8), in 7.8's order: validation, the app's settings in one update, the theme through the
/// shell's <c>UseTheme</c>, the global git writes, this repository's writes as one operation of its
/// queue, the read back, the tabs told, and the close unless a key is still decided by another
/// file of its scope or a write failed. Cancel and Esc change nothing. Answers true after a Save
/// that closed it, false for Cancel and Esc.
/// </summary>
public sealed partial class SettingsDialogViewModel : DialogViewModel<bool>
{
    private readonly SettingsScreenServices _services;
    private readonly CancellationTokenSource _repositoryCancel = new();
    private readonly SynchronizationContext? _ui;
    private bool _saveAttempted;
    private bool _writingRepository;

    /// <param name="services">What the screen works with.</param>
    /// <param name="section">The section it opens on (7.2): General unless the entry says otherwise.</param>
    public SettingsDialogViewModel(SettingsScreenServices services, SettingsSection section = SettingsSection.General)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _ui = SynchronizationContext.Current;
        SelectedSection = section;
        General = new GeneralSettingsViewModel(services.Settings.Current, services.Theme, services.Tools.PathExtensions());
        Git = new GitSettingsViewModel(services.Git, services.Repository, services.Tools.MergeTools, ShowPickAsync, services.Log, services.FileExists);
        Tools = new ToolSettingsViewModel(services.Settings.Current.Editor, services.Tools.FindEditors, services.Folders, ShowPickAsync, services.Log, services.Platform);
        Shortcuts = services.Shortcuts;
        General.PropertyChanged += OnSectionChanged;
        Git.Global.PropertyChanged += OnSectionChanged;
        Git.Repository.PropertyChanged += OnSectionChanged;
        Tools.PropertyChanged += OnSectionChanged;
        Git.FocusRequested += (_, field) => FocusRequested?.Invoke(this, field);
        Tools.FocusRequested += (_, field) => FocusRequested?.Invoke(this, field);
        Loaded = LoadAsync();
    }

    public override string Title => Texts.Title;

    protected override bool CancelledResult => false;

    public GeneralSettingsViewModel General { get; }

    public GitSettingsViewModel Git { get; }

    public ToolSettingsViewModel Tools { get; }

    /// <summary>The Keyboard shortcuts section's groups (6.11, 7.6), read-only (D192).</summary>
    public IReadOnlyList<ShortcutGroup> Shortcuts { get; }

    /// <summary>Completes when the reads of the opening have ended (8.1, 8.6, 8.7, 12.3, SD29).</summary>
    public Task Loaded { get; }

    /// <summary>The section shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGeneral), nameof(ShowsGit), nameof(ShowsTools), nameof(ShowsShortcuts))]
    public partial SettingsSection SelectedSection { get; private set; }

    public bool ShowsGeneral => SelectedSection == SettingsSection.General;

    public bool ShowsGit => SelectedSection == SettingsSection.Git;

    public bool ShowsTools => SelectedSection == SettingsSection.Tools;

    public bool ShowsShortcuts => SelectedSection == SettingsSection.KeyboardShortcuts;

    /// <summary>The section list's error dots (<c>SettingsNavError</c>): after a Save, while the section shows an error (SD9).</summary>
    public bool GeneralHasError => _saveAttempted && General.HasError;

    public bool GitHasError => _saveAttempted && Git.HasError;

    public bool ToolsHasError => _saveAttempted && Tools.HasError;

    /// <summary>The section buttons' help texts: <c>Has an error</c> while their dot shows (7.1).</summary>
    public string? GeneralNavHelp => GeneralHasError ? Texts.HasError : null;

    public string? GitNavHelp => GitHasError ? Texts.HasError : null;

    public string? ToolsNavHelp => ToolsHasError ? Texts.HasError : null;

    /// <summary><c>SettingsSaving</c>: <c>Saving…</c> while a write runs, <c>Waiting for {title} to finish…</c> while this repository's waits (7.8); null otherwise.</summary>
    [ObservableProperty]
    public partial string? SavingText { get; private set; }

    /// <summary>A write runs: Cancel and Save are disabled (well under a second, 7.8 step 5).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancelNow))]
    public partial bool IsWriting { get; private set; }

    /// <summary>Cancel is enabled: no write runs.</summary>
    public bool CanCancelNow => !IsWriting;

    /// <summary>Raised when a field should take the keyboard focus, by automation id (the first wrong one after a refused Save, Other…'s program box).</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>Shows a section (a click or Enter on its button), without focusing into it (7.1).</summary>
    [RelayCommand]
    private void ShowSection(SettingsSection section) => SelectedSection = section;

    /// <summary>Esc closes the screen, but not while a write runs (7.8).</summary>
    public override void HandleEscape()
    {
        if (!IsWriting)
        {
            base.HandleEscape();
        }
    }

    /// <summary>Cancel and Esc: nothing changes; a waiting repository write is dropped through its token (7.8 step 5, 4.5).</summary>
    protected override void OnCancel()
    {
        _repositoryCancel.Cancel();
        base.OnCancel();
    }

    /// <summary>Save (7.8): the steps in order; Enter saves, also from a text box.</summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsClosed)
        {
            return;
        }

        // 1. Validate every section; the disk checks of Other… off the UI thread.
        _saveAttempted = true;
        var general = General.ShowErrors();
        var git = Git.ShowErrors();
        var tools = await Tools.ValidateAsync(_services.Disk);
        RaiseNavErrors();
        if (IsClosed)
        {
            return;
        }

        if (general || git || tools)
        {
            SelectedSection = general ? SettingsSection.General : git ? SettingsSection.Git : SettingsSection.Tools;
            var field = general ? General.FirstErrorField : git ? Git.Shown.FirstErrorField : Tools.FirstErrorField;
            if (field is not null)
            {
                FocusRequested?.Invoke(this, field);
            }

            return;
        }

        // 2. The app's settings, but the theme, in one update; none changed, no call.
        var settings = _services.Settings;
        if (Changed(settings.Current) != settings.Current)
        {
            settings.Update(Changed);
        }

        // 3. The theme, when changed (11.6).
        if (General.Theme != General.StartTheme)
        {
            _services.UseTheme(General.Theme);
        }

        // 4. The global writes, through the app-level writer; the first failure stops them.
        var globalWrites = Git.GlobalWrites();
        var repositoryWrites = Git.RepositoryWrites();
        IReadOnlyList<GitConfigWrite> globalWritten = [];
        IReadOnlyList<GitConfigWrite> repositoryWritten = [];
        var anyWritten = false;
        var failed = false;
        if (globalWrites.Count > 0)
        {
            IsWriting = true;
            SavingText = Texts.Saving;
            try
            {
                await Git.WriteGlobalAsync(globalWrites);
                globalWritten = globalWrites;
                anyWritten = true;
            }
            catch (Exception ex) when (ex is GitException or GitUnavailableException or IOException)
            {
                // Part-way, some keys may be written: the tabs are told all the same (7.8 step 7).
                failed = true;
                anyWritten = true;
                _services.Log.Warning("Saving git's global settings failed.", ex);
                IsWriting = false;
                SavingText = null;
                await ShowGlobalFailureAsync(ex);
            }
            finally
            {
                IsWriting = false;
                SavingText = null;
            }
        }

        // 5. This repository's writes, one operation of its queue.
        if (!failed && repositoryWrites.Count > 0 && !IsClosed)
        {
            var result = await WriteRepositoryAsync(repositoryWrites);
            switch (result.Outcome)
            {
                case OperationOutcome.Succeeded:
                    repositoryWritten = repositoryWrites;
                    anyWritten = true;
                    break;
                case OperationOutcome.Failed:
                    // Its own error dialog was shown over the screen (8.3); a first key may be written.
                    failed = true;
                    anyWritten = true;
                    break;
                default:
                    // Dropped while it waited: the screen's Cancel closed it.
                    break;
            }
        }

        if (IsClosed)
        {
            if (anyWritten)
            {
                _services.GitSettingsChanged?.Invoke();
            }

            return;
        }

        // 6. Read back: each scope's read becomes its fields' baseline (8.4).
        // Only what was written whole is compared: a failed list wrote what git's error says.
        var still = await Git.ReadBackAsync(globalWritten, repositoryWritten);
        Tools.SetGitEditor(Git.CoreEditor);

        // 7. Tell the tabs (8.11).
        if (anyWritten)
        {
            _services.GitSettingsChanged?.Invoke();
        }

        // 8. Close, unless a key is still decided by another file of its scope or a write failed.
        if (still || failed)
        {
            SelectedSection = SettingsSection.Git;
            return;
        }

        Complete(true);
    }

    /// <summary>The settings with General's and Tools' changes, but the theme (7.8 step 2).</summary>
    private AppSettings Changed(AppSettings settings)
    {
        var next = General.Apply(settings);
        return Tools.IsChanged ? next with { Editor = Tools.Setting } : next;
    }

    private async Task<OperationResult> WriteRepositoryAsync(IReadOnlyList<GitConfigWrite> writes)
    {
        var repository = _services.Repository!;
        void Follow(object? sender, EventArgs e) => Post(ShowRepositoryProgress);
        repository.QueueChanged += Follow;
        _writingRepository = true;
        try
        {
            var running = Git.WriteRepositoryAsync(writes, _repositoryCancel.Token);
            ShowRepositoryProgress();
            return await running;
        }
        finally
        {
            repository.QueueChanged -= Follow;
            _writingRepository = false;
            IsWriting = false;
            SavingText = null;
        }
    }

    /// <summary>
    /// <c>SettingsSaving</c> while this repository's writes are queued: <c>Waiting for {title} to
    /// finish…</c> behind another operation, with Cancel enabled; <c>Saving…</c> once they run, with
    /// both buttons disabled.
    /// </summary>
    private void ShowRepositoryProgress()
    {
        if (!_writingRepository || IsClosed)
        {
            return;
        }

        var running = _services.Repository?.Running;
        var ours = running is null || running.Title == OperationTexts.SaveRepositorySettings.Title;
        IsWriting = ours;
        SavingText = ours ? Texts.Saving : Texts.WaitingFor(running!.Title);
    }

    private async Task ShowGlobalFailureAsync(Exception exception)
    {
        var dialog = exception is GitException git
            ? ErrorDialogViewModel.ForFailure(OperationTexts.SaveSettingsFailedTitle, RepositoryOperations.FailureOf(git, OperationTexts.SaveGlobalSettingsFailed), null, log: _services.Log)
            : new ErrorDialogViewModel(OperationTexts.SaveSettingsFailedTitle, OperationTexts.SaveGlobalSettingsFailed, null, exception.Message, log: _services.Log);
        await _services.Dialogs.ShowAsync(dialog);
    }

    /// <summary>A Pick dialog stacked over the screen (7.7, D104).</summary>
    private Task<string?> ShowPickAsync(DialogViewModel<string?> dialog) => _services.Dialogs.ShowAsync(dialog);

    private async Task LoadAsync()
    {
        var git = Git.LoadAsync();
        var tools = Tools.StartLookingAsync();
        await git;
        Tools.SetGitEditor(Git.CoreEditor);
        await tools;
    }

    private void OnSectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GeneralSettingsViewModel.HasError) or null)
        {
            RaiseNavErrors();
        }
    }

    private void RaiseNavErrors()
    {
        OnPropertyChanged(nameof(GeneralHasError));
        OnPropertyChanged(nameof(GitHasError));
        OnPropertyChanged(nameof(ToolsHasError));
        OnPropertyChanged(nameof(GeneralNavHelp));
        OnPropertyChanged(nameof(GitNavHelp));
        OnPropertyChanged(nameof(ToolsNavHelp));
    }

    private void Post(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }
}

/// <summary>A repository view model as the settings screen sees it (7.4, 8.3): its name, folders, branch, Pull's reader and its queue.</summary>
public sealed class RepositorySettingsTarget : ISettingsRepository
{
    private readonly RepositoryViewModel _repository;

    public RepositorySettingsTarget(RepositoryViewModel repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public string Name => _repository.Name;

    public string Path => _repository.Path;

    public string ConfigFile => System.IO.Path.Combine(_repository.Repository.CommonDirectory, "config");

    public string? Branch => _repository.Refs?.Head is { BranchName: { } branch, IsUnborn: false } ? branch : null;

    public OperationSnapshot? Running => _repository.Queue.Current;

    public event EventHandler? QueueChanged
    {
        add => _repository.Queue.Changed += value;
        remove => _repository.Queue.Changed -= value;
    }

    public Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default) =>
        _repository.Repository.ReadPullConfigurationAsync(branch, cancellationToken);

    public Task<OperationResult> SaveAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellation) =>
        _repository.Operations.SaveRepositorySettingsAsync(writes, cancellation);
}
