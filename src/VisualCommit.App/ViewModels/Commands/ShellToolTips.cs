namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The shell's tooltip and help texts of buttons whose command has a key (docs/phase-6-design.md
/// 6.8): the tab strip's, the welcome page's and the activity log toggle's, so that the views
/// that show them never hold a text of their own. Today's texts; work package WP-K appends each
/// key (<c>{what it does} ({key})</c>, D215). Read with <c>x:Static</c>: <c>KeyTexts.Current</c> is
/// set before the window is made.
/// </summary>
public static class ShellToolTips
{
    /// <summary>The active tab's close button.</summary>
    public static string CloseTab => "Close this tab";

    /// <summary>Another tab's close button: no key, since Mod+W closes the active tab only (6.5).</summary>
    public static string CloseOtherTab => "Close this tab";

    /// <summary>"+" (<c>AddRepoButton</c>), New tab.</summary>
    public static string NewTab => "Open, clone or init a repository";

    /// <summary>The activity log toggle while the log is hidden.</summary>
    public static string ShowActivityLog => "Show every git command the app has run";

    /// <summary>The activity log toggle while the log is shown.</summary>
    public static string HideActivityLog => "Hide the activity log";

    /// <summary>The welcome page's <c>OpenRepoButton</c>.</summary>
    public static string OpenRepository => "Open a repository on this computer";

    /// <summary>The welcome page's <c>CloneRepoButton</c>.</summary>
    public static string CloneRepository => "Clone a repository from a URL";

    /// <summary>The welcome page's <c>InitRepoButton</c>.</summary>
    public static string InitRepository => "Create a new repository in a folder";

    /// <summary>The key text of <see cref="OpenRepository"/>, its button's <c>AcceleratorKey</c>; null without one.</summary>
    public static string? OpenRepositoryKey => KeyTexts.Current.Of(AppCommand.OpenRepository);

    /// <summary>The key text of <see cref="CloneRepository"/>; null without one.</summary>
    public static string? CloneRepositoryKey => KeyTexts.Current.Of(AppCommand.CloneRepository);

    /// <summary>The key text of <see cref="InitRepository"/>; null without one.</summary>
    public static string? InitRepositoryKey => KeyTexts.Current.Of(AppCommand.InitRepository);
}
