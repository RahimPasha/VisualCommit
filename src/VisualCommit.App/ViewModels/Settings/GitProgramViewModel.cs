using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The settings screen's "Git program" row (D423): last in the Git section's global
/// scope, in every state of the section. It shows the pending choice, else the git in use, else
/// <c>None found</c>, with a note on where it came from and, while the choice differs from the git
/// in use, when it applies: from the next start while git works, at Save while it does not. Browse…
/// checks a file as the first-run screen's Choose does; the button beside it drops the choice.
/// Save writes the choice as <c>gitExecutable</c> (<see cref="Choice"/>, <see cref="IsChanged"/>);
/// Cancel and Esc drop it.
/// </summary>
public sealed partial class GitProgramViewModel : ObservableObject
{
    private readonly string? _saved;
    private readonly GitDetection? _inUse;
    private readonly Func<string, CancellationToken, Task<GitDetection>>? _probe;
    private readonly IFolderPicker _folders;
    private readonly CancellationToken _lifetime;

    /// <param name="saved">The git chosen in Settings when the screen opened; null for none.</param>
    /// <param name="inUse">What the session's last search found; null while none has ended.</param>
    /// <param name="probe">What a chosen file would give as the git (<c>GitAccess.ProbeAsync</c>); null: Browse finds no git.</param>
    /// <param name="folders">The file dialog of Browse….</param>
    /// <param name="lifetime">The screen's lifetime, which ends a probe that still runs.</param>
    public GitProgramViewModel(string? saved, GitDetection? inUse, Func<string, CancellationToken, Task<GitDetection>>? probe, IFolderPicker folders, CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(folders);
        _saved = saved;
        _inUse = inUse;
        _probe = probe;
        _folders = folders;
        _lifetime = lifetime;
        Choice = saved;
    }

    /// <summary>The label of the row.</summary>
    public string Label => GitSetupTexts.GitProgramLabel;

    /// <summary>The pending choice, which Save writes as <c>gitExecutable</c>; null for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged), nameof(PathText), nameof(NoteText), nameof(ShowsUsePath), nameof(UsePathText))]
    public partial string? Choice { get; private set; }

    /// <summary>The choice differs from the one saved: Save writes it.</summary>
    public bool IsChanged => !string.Equals(Choice, _saved, StringComparison.Ordinal);

    /// <summary>
    /// <c>GitProgramStatus</c>: <c>Checking…</c> while Browse's probe runs, then Browse's refusal;
    /// null for none.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStatus), nameof(IsStatusRefusal))]
    public partial string? StatusText { get; private set; }

    public bool ShowsStatus => !string.IsNullOrEmpty(StatusText);

    /// <summary>The status is a refusal, not <c>Checking…</c>: drawn as an error.</summary>
    public bool IsStatusRefusal => ShowsStatus && StatusText != GitSetupTexts.Checking;

    /// <summary><c>GitProgramPath</c>: the pending choice, else the git in use, else <c>None found</c>.</summary>
    public string PathText => Choice ?? InUsePath ?? GitSetupTexts.NoneFound;

    /// <summary><c>GitProgramNote</c>: where the shown git came from, then, while the choice differs from the git in use, when it applies.</summary>
    public string NoteText
    {
        get
        {
            var windows = OperatingSystem.IsWindows();
            string note;
            if (Choice is { } choice)
            {
                note = _inUse is { Availability: GitAvailability.NotFound, ChosenMissing: { } missing } && string.Equals(missing, choice, StringComparison.Ordinal)
                    ? GitSetupTexts.SourceNote(_inUse, windows)
                    : GitSetupTexts.ChosenHereNote;
            }
            else if (_inUse is { ChosenMissing: not null })
            {
                // The gone choice dropped: what Save does next is the only thing to say.
                return GitSetupTexts.WhenYouSaveSuffix.Trim();
            }
            else
            {
                note = GitSetupTexts.SourceNote(_inUse, windows);
            }

            return DiffersFromInUse ? note + (GitWorks ? GitSetupTexts.NextStartSuffix : GitSetupTexts.WhenYouSaveSuffix) : note;
        }
    }

    /// <summary><c>GitProgramUsePathButton</c> shows while a path is chosen.</summary>
    public bool ShowsUsePath => Choice is not null;

    /// <summary>
    /// <c>Use the Git it found</c> while the chosen git is gone and another was found, as on the
    /// first-run screen, or while the choice would give way to a git of a usual install folder;
    /// <c>Use the Git on the PATH</c> otherwise.
    /// </summary>
    public string UsePathText =>
        (_inUse is { ChosenMissing: not null, PathCandidate: not null } && string.Equals(_inUse.ChosenMissing, Choice, StringComparison.Ordinal))
        || (Choice is not null && _inUse is { Source: GitSource.InstallFolder, ExecutablePath: not null })
            ? GitSetupTexts.UseTheGitItFound
            : GitSetupTexts.UseTheGitOnThePath;

    /// <summary>The path of the git the session uses now; null for none.</summary>
    private string? InUsePath => _inUse?.ExecutablePath;

    /// <summary>Git works now: a changed choice applies from the next start (D423).</summary>
    private bool GitWorks => _inUse is { Availability: GitAvailability.Available };

    /// <summary>The choice the last search was told and went by: the chosen git in use, or the chosen one it found gone; null when it went by none.</summary>
    private string? ChoiceInUse => _inUse is { Source: GitSource.Setting } ? _inUse.ExecutablePath ?? _inUse.ChosenMissing : null;

    /// <summary>The choice is not the one the git in use came from: another path, or none while that git was chosen.</summary>
    private bool DiffersFromInUse => !string.Equals(Choice, ChoiceInUse, StringComparison.Ordinal);

    /// <summary>Browse…: the file dialog, then Choose's checks; a git that is found and new enough becomes the choice.</summary>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var file = await _folders.PickFileAsync(OperatingSystem.IsWindows() ? GitSetupTexts.ChooseGitExeTitle : GitSetupTexts.ChooseGitProgramTitle, _lifetime);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        StatusText = null;
        string? refusal;
        try
        {
            refusal = await GitSetupViewModel.CheckChosenAsync(
                file,
                _probe ?? ((_, _) => Task.FromResult(new GitDetection(GitAvailability.NotFound, null, null, null))),
                text => StatusText = text,
                _lifetime);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The screen closed while the file was checked.
            return;
        }

        StatusText = refusal;
        if (refusal is null)
        {
            Choice = file;
        }
    }

    /// <summary>"Use the Git on the PATH" and "Use the Git it found": the choice dropped; Save removes the setting.</summary>
    [RelayCommand]
    private void UsePath()
    {
        StatusText = null;
        Choice = null;
    }
}
