namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The shown key texts of the shell's buttons (docs/phase-6-design.md 6.8): what
/// <c>AutomationProperties.AcceleratorKey</c> of the toolbar's, the tab strip's and the status
/// bar's buttons binds to (<see cref="MainWindowViewModel.Keys"/>), and the dashboard header's
/// Fetch all, Pull all and Refresh (<c>DashboardViewModel.Keys</c>), enabled or not; null for a
/// command without a key. A class, not a record: a record may not have a member named
/// <see cref="Clone"/>.
/// </summary>
public sealed class ShellKeys
{
    /// <summary>The shell's key texts from <paramref name="keys"/>.</summary>
    public ShellKeys(KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Palette = keys.Of(AppCommand.CommandPalette);
        Settings = keys.Of(AppCommand.Settings);
        Refresh = keys.Of(AppCommand.Refresh);
        NewTab = keys.Of(AppCommand.NewTab);
        CloseTab = keys.Of(AppCommand.CloseTab);
        Fetch = keys.Of(AppCommand.Fetch);
        Pull = keys.Of(AppCommand.Pull);
        Push = keys.Of(AppCommand.Push);
        Branch = keys.Of(AppCommand.CreateBranch);
        Stash = keys.Of(AppCommand.Stash);
        Pop = keys.Of(AppCommand.Pop);
        Undo = keys.Of(AppCommand.Undo);
        Redo = keys.Of(AppCommand.Redo);
        Search = keys.Of(AppCommand.Search);
        ActivityLog = keys.Of(AppCommand.ToggleActivityLog);
        Open = keys.Of(AppCommand.OpenRepository);
        Clone = keys.Of(AppCommand.CloneRepository);
        Init = keys.Of(AppCommand.InitRepository);
    }

    public string? Palette { get; }

    public string? Settings { get; }

    /// <summary>Refresh's key (F5; Cmd+R on macOS): the dashboard header's <c>DashboardRefresh</c>.</summary>
    public string? Refresh { get; }

    public string? NewTab { get; }

    public string? CloseTab { get; }

    public string? Fetch { get; }

    public string? Pull { get; }

    public string? Push { get; }

    public string? Branch { get; }

    public string? Stash { get; }

    public string? Pop { get; }

    public string? Undo { get; }

    public string? Redo { get; }

    public string? Search { get; }

    public string? ActivityLog { get; }

    public string? Open { get; }

    public string? Clone { get; }

    public string? Init { get; }
}
