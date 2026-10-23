namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The shown key texts of the shell's buttons (docs/phase-6-design.md 6.8): what
/// <c>AutomationProperties.AcceleratorKey</c> of the toolbar's buttons, the tab strip's "+" and the
/// status bar's activity log toggle binds to (<see cref="MainWindowViewModel.Keys"/>), and the
/// dashboard header's Fetch all, Pull all and Refresh (<c>DashboardViewModel.Keys</c>), enabled or
/// not; null for a command without a key. Each key text has one source: the close button's is
/// its tab's (<see cref="RepoTabViewModel.CloseKeyText"/>), the welcome page's and the buttons'
/// outside the shell are <see cref="ShellToolTips"/>'.
/// </summary>
public sealed class ShellKeys
{
    /// <summary>The shell's key texts from <paramref name="keys"/>.</summary>
    public ShellKeys(KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Settings = keys.Of(AppCommand.Settings);
        Refresh = keys.Of(AppCommand.Refresh);
        NewTab = keys.Of(AppCommand.NewTab);
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
    }

    public string? Settings { get; }

    /// <summary>Refresh's key (F5; Cmd+R on macOS): the dashboard header's <c>DashboardRefresh</c>.</summary>
    public string? Refresh { get; }

    public string? NewTab { get; }

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
}
