namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The labels of phase 5's activity log menu (docs/phase-5-design.md 2.2, 13.1), which
/// <c>RepositoryMenus.ActivityLog.cs</c> builds its items from.
/// </summary>
public static partial class OperationTexts
{
    public static partial class MenuTexts
    {
        /// <summary>A git call's row: its command as the log shows it, credentials hidden.</summary>
        public const string CopyCommand = "Copy command";

        /// <summary>A git call's row with output: its standard output, then its standard error.</summary>
        public const string CopyOutput = "Copy output";

        /// <summary>An operation's row: its title.</summary>
        public const string CopyTitle = "Copy title";
    }
}
