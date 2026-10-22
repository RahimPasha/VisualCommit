using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>What the editor field holds (docs/phase-6-design.md 7.5).</summary>
public enum EditorChoice
{
    /// <summary>No editor: no Open in editor in the file menus.</summary>
    None,

    /// <summary>An editor of the catalogue that was found (12.3).</summary>
    Known,

    /// <summary>A program of the user's choice (12.5).</summary>
    Other,
}

/// <summary>
/// How the Tools section reads the disk (12.5): whether a file exists, and on Linux and macOS
/// whether it is marked as a program. Only Save calls them, off the UI thread (7.8 step 1).
/// </summary>
/// <param name="FileExists">Whether a file exists at the path.</param>
/// <param name="IsExecutable">Whether the file is marked as a program (an execute bit); ignored on Windows.</param>
public sealed record EditorDiskChecks(Func<string, bool> FileExists, Func<string, bool> IsExecutable)
{
    /// <summary>
    /// The disk as it is. Off Windows a folder counts as there, so that a macOS <c>.app</c> bundle
    /// gets "This file is not marked as a program." (12.5) rather than "no file".
    /// </summary>
    public static EditorDiskChecks System { get; } = new(
        path => File.Exists(path) || (!OperatingSystem.IsWindows() && Directory.Exists(path)),
        IsMarkedExecutable);

    private static bool IsMarkedExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            const UnixFileMode Executable = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return !Directory.Exists(path) && (File.GetUnixFileMode(path) & Executable) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// The settings screen's Tools section (docs/phase-6-design.md 7.5, 12): the editor "Open in
/// editor" starts. Choose… opens the Pick dialog "Choose an editor" (7.7) over the screen, with
/// the editors the catalogue found (12.3), which it looks for when the screen opens, off the UI
/// thread (<c>EditorStatus</c> meanwhile); Other… takes a program and its arguments (12.5), checked
/// as they are typed but for the two checks that read the disk, which Save makes off the UI thread.
/// </summary>
public sealed partial class ToolSettingsViewModel : ObservableObject
{
    private readonly EditorSetting? _start;
    private readonly Func<CancellationToken, Task<IReadOnlyList<FoundEditor>>> _findEditors;
    private readonly IFolderPicker _folders;
    private readonly Func<DialogViewModel<string?>, Task<string?>> _showPick;
    private readonly EditorPlatform _platform;
    private readonly IAppLog _log;
    private Task<IReadOnlyList<FoundEditor>>? _finding;
    private string? _gitEditorCommand;
    private bool _programEdited;
    private bool _argumentsEdited;
    private bool _showErrors;
    private string? _saveError;
    private bool _saveChecked;

    /// <param name="editor">The editor as stored, or null for none.</param>
    /// <param name="findEditors">The session's catalogue of editors (12.9).</param>
    /// <param name="folders">The file dialog of Browse… (<see cref="IFolderPicker.PickFileAsync"/>).</param>
    /// <param name="showPick">Shows a Pick dialog over the screen and returns its answer.</param>
    /// <param name="log">The app's log.</param>
    /// <param name="platform">The platform whose rules the program follows; null for this one.</param>
    public ToolSettingsViewModel(
        EditorSetting? editor,
        Func<CancellationToken, Task<IReadOnlyList<FoundEditor>>> findEditors,
        IFolderPicker folders,
        Func<DialogViewModel<string?>, Task<string?>> showPick,
        IAppLog? log = null,
        EditorPlatform? platform = null)
    {
        ArgumentNullException.ThrowIfNull(findEditors);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(showPick);
        _findEditors = findEditors;
        _folders = folders;
        _showPick = showPick;
        _log = log ?? NullAppLog.Instance;
        _platform = platform ?? EditorSearchContext.CurrentPlatform;

        // An id the catalogue no longer knows means none chosen (12.6).
        _start = editor is not null && (editor.Id == KnownEditors.CustomId || KnownEditors.Find(editor.Id, _platform) is not null) ? editor : null;
        if (_start is null)
        {
            Choice = EditorChoice.None;
        }
        else if (_start.Id == KnownEditors.CustomId)
        {
            Choice = EditorChoice.Other;
            ProgramText = _start.Program;
            ArgumentsText = _start.Arguments ?? Texts.DefaultArguments;
        }
        else
        {
            Choice = EditorChoice.Known;
            KnownId = _start.Id;
            KnownName = KnownEditors.Find(_start.Id, _platform)!.Name;
            KnownProgram = _start.Program;
        }

        _programEdited = false;
        _argumentsEdited = false;
    }

    /// <summary>What the field holds now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorValue), nameof(EditorProgram), nameof(ShowsEditorProgram), nameof(IsOther), nameof(EditorError), nameof(HasError))]
    public partial EditorChoice Choice { get; private set; }

    /// <summary>The known editor's id, while <see cref="Choice"/> is <see cref="EditorChoice.Known"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorValue))]
    public partial string? KnownId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorValue))]
    public partial string? KnownName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorProgram), nameof(EditorValue))]
    public partial string? KnownProgram { get; private set; }

    /// <summary><c>EditorProgramBox</c>'s text (Other).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorError), nameof(HasError))]
    public partial string ProgramText { get; set; } = string.Empty;

    /// <summary><c>EditorArgumentsBox</c>'s text (Other).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorError), nameof(HasError))]
    public partial string ArgumentsText { get; set; } = Texts.DefaultArguments;

    /// <summary>The editors are being looked for (<c>EditorStatus</c>, <c>Looking for editors…</c>).</summary>
    [ObservableProperty]
    public partial bool IsLooking { get; private set; }

    /// <summary>The editors the catalogue found, once it has answered.</summary>
    public IReadOnlyList<FoundEditor> Found { get; private set; } = [];

    /// <summary>Raised when Other… wants the focus in <c>EditorProgramBox</c> (7.5).</summary>
    public event EventHandler<string>? FocusRequested;

    public bool IsOther => Choice == EditorChoice.Other;

    /// <summary><c>EditorValue</c>: <c>None</c>, the editor's name (<c> (git's editor)</c> after it when 12.3's mark applies), or <c>Other</c>.</summary>
    public string EditorValue => Choice switch
    {
        EditorChoice.Known => KnownName + (IsGitEditor(KnownProgram) ? Texts.GitsEditor : string.Empty),
        EditorChoice.Other => Texts.EditorOther,
        _ => Texts.EditorNone,
    };

    /// <summary><c>EditorProgram</c>: the known editor's program (hidden for None and for Other, whose box shows it).</summary>
    public string? EditorProgram => Choice == EditorChoice.Known ? KnownProgram : null;

    public bool ShowsEditorProgram => EditorProgram is not null;

    /// <summary><c>EditorError</c> (12.5), once a box was edited or after a Save; the disk's errors only after a Save.</summary>
    public string? EditorError => Choice == EditorChoice.Other && (_programEdited || _argumentsEdited || _showErrors) ? CurrentError() : null;

    public bool HasError => EditorError is not null;

    /// <summary>The first field with an error, by automation id, for the focus after a refused Save; null for none.</summary>
    public string? FirstErrorField => Choice != EditorChoice.Other || CurrentError() is not { } error
        ? null
        : error == EditorCommands.UnclosedQuote ? "EditorArgumentsBox" : "EditorProgramBox";

    /// <summary>The editor as Save stores it (12.6); call it only when there is no error.</summary>
    public EditorSetting? Setting => Choice switch
    {
        EditorChoice.Known => new EditorSetting(KnownId!, KnownProgram!),
        EditorChoice.Other => new EditorSetting(KnownEditors.CustomId, ProgramText.Trim(), ArgumentsText),
        _ => null,
    };

    /// <summary>Whether the field differs from what was stored when the screen opened.</summary>
    public bool IsChanged => Setting != _start;

    /// <summary>Starts looking for the editors (12.3), off the UI thread; Choose… waits for it.</summary>
    public Task<IReadOnlyList<FoundEditor>> StartLookingAsync()
    {
        _finding ??= LookAsync();
        return _finding;
    }

    /// <summary>The <c>core.editor</c> of git's configuration (from 8.1's read), for the ` (git's editor)` mark (12.3); null when unset.</summary>
    public void SetGitEditor(string? coreEditor)
    {
        _gitEditorCommand = coreEditor;
        OnPropertyChanged(nameof(EditorValue));
    }

    /// <summary>
    /// Save's checks (7.8 step 1, 12.5): the typed ones and the two that read the disk, which run
    /// off the UI thread; shows the errors and returns whether there is one.
    /// </summary>
    public async Task<bool> ValidateAsync(EditorDiskChecks disk)
    {
        ArgumentNullException.ThrowIfNull(disk);
        _showErrors = true;
        _saveError = null;
        _saveChecked = false;
        if (Choice == EditorChoice.Other && ProgramText.Trim() is { Length: > 0 } program)
        {
            // 12.5's order, first that holds: empty, no file, the extension (Windows), the execute bit
            // (elsewhere), the quote. The disk is read off the UI thread: a share that does not answer
            // holds a check for minutes (0.9).
            var platform = _platform;
            var (exists, executable) = await Task.Run(() =>
            {
                var found = disk.FileExists(program);
                return (found, platform == EditorPlatform.Windows || !found || disk.IsExecutable(program));
            });
            _saveError = !exists ? Texts.ProgramMissing
                : platform == EditorPlatform.Windows && !EditorCommands.IsWindowsProgram(program) ? Texts.ProgramNotExe
                : !executable ? Texts.ProgramNotExecutable
                : EditorCommands.SplitArguments(ArgumentsText) is null ? EditorCommands.UnclosedQuote
                : null;
            _saveChecked = true;
        }

        OnPropertyChanged(nameof(EditorError));
        OnPropertyChanged(nameof(HasError));
        return HasError;
    }

    /// <summary>Choose…: the Pick dialog <c>Choose an editor</c> (7.5, 7.7) with the editors found, after the search has answered.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ChooseAsync()
    {
        var found = await StartLookingAsync();
        var gitEditor = EditorCatalog.GitEditor(_gitEditorCommand, found, _platform);
        var options = new List<PickOption> { new(Texts.PickNone, Texts.PickNoneDetail) };
        options.AddRange(found.Select(editor => new PickOption(OptionText(editor, gitEditor), editor.Program)));
        options.Add(new PickOption(Texts.PickOther, Texts.PickOtherDetail));
        var selected = Choice switch
        {
            EditorChoice.None => Texts.PickNone,
            EditorChoice.Other => Texts.PickOther,
            _ => found.FirstOrDefault(editor => editor.Id == KnownId && editor.Program == KnownProgram) is { } current ? OptionText(current, gitEditor) : null,
        };
        var answer = await _showPick(new PickDialogViewModel(Texts.ChooseEditorTitle, options, selected, Texts.PickConfirm));
        if (answer is null)
        {
            return;
        }

        if (answer == Texts.PickNone)
        {
            Choice = EditorChoice.None;
        }
        else if (answer == Texts.PickOther)
        {
            Choice = EditorChoice.Other;
            FocusRequested?.Invoke(this, "EditorProgramBox");
        }
        else if (found.FirstOrDefault(editor => OptionText(editor, gitEditor) == answer) is { } chosen)
        {
            KnownId = chosen.Id;
            KnownName = chosen.Name;
            KnownProgram = chosen.Program;
            Choice = EditorChoice.Known;
        }
    }

    /// <summary>Browse…: the platform's file dialog <c>Choose the editor's program</c> (12.5) fills the box.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task BrowseAsync()
    {
        try
        {
            if (await _folders.PickFileAsync(Texts.ChooseProgramTitle) is { Length: > 0 } file)
            {
                ProgramText = file;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("The file dialog for the editor's program failed.", ex);
        }
    }

    partial void OnProgramTextChanged(string value)
    {
        _programEdited = true;
        _saveChecked = false;
    }

    partial void OnArgumentsTextChanged(string value)
    {
        _argumentsEdited = true;
        _saveChecked = false;
    }

    /// <summary>The error the field has now: Save's whole check until the next edit, then the typed checks alone.</summary>
    private string? CurrentError() => _saveChecked ? _saveError : TypedError();

    private static string OptionText(FoundEditor editor, FoundEditor? gitEditor) =>
        editor.Name + (gitEditor is not null && gitEditor == editor ? Texts.GitsEditor : string.Empty);

    private async Task<IReadOnlyList<FoundEditor>> LookAsync()
    {
        IsLooking = true;
        try
        {
            Found = await _findEditors(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("Looking for editors failed.", ex);
            Found = [];
        }
        finally
        {
            IsLooking = false;
        }

        OnPropertyChanged(nameof(EditorValue));
        return Found;
    }

    private bool IsGitEditor(string? program) =>
        program is not null
        && EditorCatalog.GitEditor(_gitEditorCommand, [new FoundEditor(KnownId ?? string.Empty, KnownName ?? string.Empty, program)], _platform) is not null;

    /// <summary>The checks that read no disk, run on each edit (12.5): empty, the extension on Windows, the quote.</summary>
    private string? TypedError()
    {
        var program = ProgramText.Trim();
        if (program.Length == 0)
        {
            return Texts.ProgramEmpty;
        }

        if (_platform == EditorPlatform.Windows && !EditorCommands.IsWindowsProgram(program))
        {
            return Texts.ProgramNotExe;
        }

        return EditorCommands.SplitArguments(ArgumentsText) is null ? EditorCommands.UnclosedQuote : null;
    }
}
