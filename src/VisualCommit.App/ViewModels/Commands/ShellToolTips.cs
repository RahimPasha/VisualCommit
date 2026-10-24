namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The tooltip and help texts of buttons whose command has a key (docs/phase-6-design.md 6.8,
/// 14.11, D199, D215): <c>{what it does} ({key})</c>, the key in the platform's words, and beside
/// each the key's text for <c>AutomationProperties.AcceleratorKey</c>. The tab strip's, the
/// welcome page's and the activity log toggle's, and the buttons outside the shell (the conflict
/// resolver's five, the interactive list's Cancel, the commit button's key), so that the views
/// that show them never hold a text of their own. The properties read <see cref="KeyTexts.Current"/>,
/// which <c>AppSession.Start</c> sets before the window is made, so views read them with
/// <c>x:Static</c>; each has a <c>…For(KeyTexts)</c> twin for another platform's words.
/// </summary>
public static class ShellToolTips
{
    /// <summary>The active tab's close button: <c>Close this tab (Ctrl+W)</c>.</summary>
    public static string CloseTab => CloseTabFor(KeyTexts.Current);

    /// <summary>Another tab's close button: no key, since Mod+W closes the active tab only (6.5).</summary>
    public static string CloseOtherTab => "Close this tab";

    /// <summary>"+" (<c>AddRepoButton</c>), New tab's key: <c>Open, clone or init a repository (Ctrl+T)</c>.</summary>
    public static string NewTab => NewTabFor(KeyTexts.Current);

    /// <summary>The activity log toggle while the log is hidden (and on an empty tab, where it is disabled): <c>Show every git command the app has run (Ctrl+J)</c>.</summary>
    public static string ShowActivityLog => ShowActivityLogFor(KeyTexts.Current);

    /// <summary>The activity log toggle while the log is shown: <c>Hide the activity log (Ctrl+J)</c>.</summary>
    public static string HideActivityLog => HideActivityLogFor(KeyTexts.Current);

    /// <summary>The welcome page's <c>OpenRepoButton</c>: <c>Open a repository on this computer (Ctrl+O)</c>.</summary>
    public static string OpenRepository => OpenRepositoryFor(KeyTexts.Current);

    /// <summary>The welcome page's <c>CloneRepoButton</c>: <c>Clone a repository from a URL (Ctrl+Shift+C)</c>.</summary>
    public static string CloneRepository => CloneRepositoryFor(KeyTexts.Current);

    /// <summary>The welcome page's <c>InitRepoButton</c>: <c>Create a new repository in a folder (Ctrl+Shift+I)</c>.</summary>
    public static string InitRepository => InitRepositoryFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="OpenRepository"/>, its button's <c>AcceleratorKey</c>.</summary>
    public static string? OpenRepositoryKey => KeyTexts.Current.Of(AppCommand.OpenRepository);

    /// <summary>The key text of <see cref="CloneRepository"/>.</summary>
    public static string? CloneRepositoryKey => KeyTexts.Current.Of(AppCommand.CloneRepository);

    /// <summary>The key text of <see cref="InitRepository"/>.</summary>
    public static string? InitRepositoryKey => KeyTexts.Current.Of(AppCommand.InitRepository);

    /// <summary>The resolver's <c>ResolverPreviousConflict</c>: <c>Previous conflict (Alt+Up)</c>, <c>Option+Up</c> on macOS.</summary>
    public static string PreviousConflict => PreviousConflictFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="PreviousConflict"/>.</summary>
    public static string? PreviousConflictKey => KeyTexts.Current.Of(AppCommand.PreviousConflict);

    /// <summary>The resolver's <c>ResolverNextConflict</c>: <c>Next conflict (Alt+Down)</c>.</summary>
    public static string NextConflict => NextConflictFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="NextConflict"/>.</summary>
    public static string? NextConflictKey => KeyTexts.Current.Of(AppCommand.NextConflict);

    /// <summary>The resolver's <c>ResolverPreviousFile</c>: <c>Previous file (Alt+Left)</c>.</summary>
    public static string PreviousFile => PreviousFileFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="PreviousFile"/>.</summary>
    public static string? PreviousFileKey => KeyTexts.Current.Of(AppCommand.PreviousFile);

    /// <summary>The resolver's <c>ResolverNextFile</c>: <c>Next file (Alt+Right)</c>.</summary>
    public static string NextFile => NextFileFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="NextFile"/>.</summary>
    public static string? NextFileKey => KeyTexts.Current.Of(AppCommand.NextFile);

    /// <summary>The resolver's <c>ResolverCloseButton</c>: <c>Close (Esc)</c>, the Esc chain's.</summary>
    public static string CloseResolver => CloseResolverFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="CloseResolver"/>.</summary>
    public static string? CloseResolverKey => KeyTexts.Current.Of(AppCommand.Close);

    /// <summary>The interactive list's <c>RebaseCancelButton</c>: <c>Cancel (Esc)</c>, the Esc chain's.</summary>
    public static string CancelList => CancelListFor(KeyTexts.Current);

    /// <summary>The key text of <see cref="CancelList"/>.</summary>
    public static string? CancelListKey => KeyTexts.Current.Of(AppCommand.Close);

    /// <summary>The stage panel's <c>CommitButton</c>'s key (Mod+Enter, from the message boxes, 6.10), enabled or not.</summary>
    public static string? CommitKey => KeyTexts.Current.Of(AppCommand.Commit);

    /// <summary><see cref="CloseTab"/> in <paramref name="keys"/>' words.</summary>
    public static string CloseTabFor(KeyTexts keys) => With(keys, "Close this tab", AppCommand.CloseTab);

    /// <summary><see cref="NewTab"/> in <paramref name="keys"/>' words.</summary>
    public static string NewTabFor(KeyTexts keys) => With(keys, "Open, clone or init a repository", AppCommand.NewTab);

    /// <summary><see cref="ShowActivityLog"/> in <paramref name="keys"/>' words.</summary>
    public static string ShowActivityLogFor(KeyTexts keys) => With(keys, "Show every git command the app has run", AppCommand.ToggleActivityLog);

    /// <summary><see cref="HideActivityLog"/> in <paramref name="keys"/>' words.</summary>
    public static string HideActivityLogFor(KeyTexts keys) => With(keys, "Hide the activity log", AppCommand.ToggleActivityLog);

    /// <summary><see cref="OpenRepository"/> in <paramref name="keys"/>' words.</summary>
    public static string OpenRepositoryFor(KeyTexts keys) => With(keys, "Open a repository on this computer", AppCommand.OpenRepository);

    /// <summary><see cref="CloneRepository"/> in <paramref name="keys"/>' words.</summary>
    public static string CloneRepositoryFor(KeyTexts keys) => With(keys, "Clone a repository from a URL", AppCommand.CloneRepository);

    /// <summary><see cref="InitRepository"/> in <paramref name="keys"/>' words.</summary>
    public static string InitRepositoryFor(KeyTexts keys) => With(keys, "Create a new repository in a folder", AppCommand.InitRepository);

    /// <summary><see cref="PreviousConflict"/> in <paramref name="keys"/>' words.</summary>
    public static string PreviousConflictFor(KeyTexts keys) => With(keys, "Previous conflict", AppCommand.PreviousConflict);

    /// <summary><see cref="NextConflict"/> in <paramref name="keys"/>' words.</summary>
    public static string NextConflictFor(KeyTexts keys) => With(keys, "Next conflict", AppCommand.NextConflict);

    /// <summary><see cref="PreviousFile"/> in <paramref name="keys"/>' words.</summary>
    public static string PreviousFileFor(KeyTexts keys) => With(keys, "Previous file", AppCommand.PreviousFile);

    /// <summary><see cref="NextFile"/> in <paramref name="keys"/>' words.</summary>
    public static string NextFileFor(KeyTexts keys) => With(keys, "Next file", AppCommand.NextFile);

    /// <summary><see cref="CloseResolver"/> in <paramref name="keys"/>' words.</summary>
    public static string CloseResolverFor(KeyTexts keys) => With(keys, "Close", AppCommand.Close);

    /// <summary><see cref="CancelList"/> in <paramref name="keys"/>' words.</summary>
    public static string CancelListFor(KeyTexts keys) => With(keys, "Cancel", AppCommand.Close);

    private static string With(KeyTexts keys, string text, AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.WithKey(text, command);
    }
}
