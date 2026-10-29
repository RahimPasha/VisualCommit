namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// Every command with a key or a palette entry (docs/phase-6-design.md 6.1, 6.4, D192, D199), and
/// the keys only drawn and listed, which their own views handle. No Avalonia types.
/// </summary>
public enum AppCommand
{
    // The window's commands (6.1's first table, and those without a key, reached through the palette).
    CommandPalette,
    Settings,
    ShowKeyboardShortcuts,
    ToggleActivityLog,
    Refresh,
    NewTab,
    CloseTab,
    NextTab,
    PreviousTab,
    GoToTab,
    GoToLastTab,
    GoToNextDashboard,
    OpenRepository,
    CloneRepository,
    InitRepository,
    NewWorkspace,
    Fetch,
    Pull,
    Push,
    CreateBranch,
    Stash,
    Pop,
    Undo,
    Redo,
    Search,
    NextMatch,
    PreviousMatch,
    ShowReflog,
    ShowBackups,
    CreateTagAtHead,
    AddRemote,
    ManageRemotes,
    ShowAllHiddenBranches,
    StopSoloing,
    StageAll,
    UnstageAll,
    GoToCommitMessage,
    Commit,
    FileHistoryOfDiff,
    BlameOfDiff,
    FilterRefs,
    NextRegion,
    PreviousRegion,
    UseDarkTheme,
    UseLightTheme,
    FollowSystemTheme,

    // Phase 7a's keyless palette command (docs/phase-7a-design.md 9.5, D299): after the theme's,
    // so that no value before it moves.
    CloneFromGitHub,

    // Keys only drawn and listed (KeyPass.View): their views handle them.
    ContextMenu,
    TextUndo,
    TextRedo,
    Cut,
    Copy,
    Paste,
    SelectAll,
    RebasePick,
    RebaseReword,
    RebaseSquash,
    RebaseFixup,
    RebaseDrop,

    // For 6.8's tooltips: the conflict resolver's Alt+Up, Alt+Down, Alt+Left, Alt+Right, and Esc.
    PreviousConflict,
    NextConflict,
    PreviousFile,
    NextFile,
    Close,
}
