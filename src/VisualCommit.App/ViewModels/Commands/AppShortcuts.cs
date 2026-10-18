using static VisualCommit.App.ViewModels.Commands.ShortcutKey;
using static VisualCommit.App.ViewModels.Commands.ShortcutModifiers;

namespace VisualCommit.App.ViewModels.Commands;

/// <summary>How a key reaches its command (docs/phase-6-design.md 6.2, 6.4).</summary>
public enum KeyPass
{
    /// <summary>The window's tunnelling pass: the router takes it before the focused control.</summary>
    Tunnel,

    /// <summary>The window's bubbling pass, after the focused control (undo and redo, phase 5 design 1.3).</summary>
    Bubble,

    /// <summary>Drawn and listed only: its view handles it.</summary>
    View,
}

/// <summary>One command's keys (6.4).</summary>
/// <param name="Command">The command.</param>
/// <param name="Keys">Its keys, in the platform-neutral words of <see cref="ShortcutModifiers"/>.</param>
/// <param name="Pass">How they reach it.</param>
/// <param name="ShownOnCtrl">The index in <paramref name="Keys"/> of the key tooltips, menus and the palette show where the command modifier is Ctrl.</param>
/// <param name="ShownOnCmd">The same where it is Cmd (macOS).</param>
public sealed record ShortcutBinding(AppCommand Command, IReadOnlyList<Shortcut> Keys, KeyPass Pass, int ShownOnCtrl = 0, int ShownOnCmd = 0);

/// <summary>A command routed from a key (6.3): the command and its argument (the place for Mod+1 to Mod+8, from 1; otherwise 0).</summary>
public readonly record struct RoutedCommand(AppCommand Command, int Argument = 0);

/// <summary>A group of the settings screen's Keyboard shortcuts section (6.11).</summary>
/// <param name="Title">The group's title (App, Tabs, …).</param>
/// <param name="Rows">Its rows.</param>
/// <param name="Note">A line under the group, or null.</param>
public sealed record ShortcutGroup(string Title, IReadOnlyList<ShortcutRow> Rows, string? Note);

/// <summary>One row of the Keyboard shortcuts section (6.11): the action's name and its keys in the platform's words, joined by ", ".</summary>
public sealed record ShortcutRow(string Action, string Keys);

/// <summary>
/// The fixed key table (D192, D199, D213; docs/phase-6-design.md 6.1) as data: no screen changes
/// it. Work package WP-K gives its reads their bodies (6.3, 6.4, 6.11); until then no key routes
/// and none is shown, so every key text is null and the app behaves as in phase 5.
/// </summary>
public static class AppShortcuts
{
    /// <summary>Every command's keys, in 6.1's order: the router's (tunnel and bubble) and the views' own.</summary>
    public static IReadOnlyList<ShortcutBinding> Bindings { get; } =
    [
        new(AppCommand.CommandPalette, [new(P, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Settings, [new(Comma, Command)], KeyPass.Tunnel),
        new(AppCommand.ToggleActivityLog, [new(J, Command)], KeyPass.Tunnel),
        new(AppCommand.Refresh, [new(F5), new(R, Command)], KeyPass.Tunnel, ShownOnCtrl: 0, ShownOnCmd: 1),
        new(AppCommand.NewTab, [new(T, Command)], KeyPass.Tunnel),
        new(AppCommand.CloseTab, [new(W, Command)], KeyPass.Tunnel),
        new(AppCommand.NextTab, [new(Tab, Control), new(PageDown, Control)], KeyPass.Tunnel),
        new(AppCommand.PreviousTab, [new(Tab, Control | Shift), new(PageUp, Control)], KeyPass.Tunnel),
        new(
            AppCommand.GoToTab,
            [new(D1, Command), new(D2, Command), new(D3, Command), new(D4, Command), new(D5, Command), new(D6, Command), new(D7, Command), new(D8, Command)],
            KeyPass.Tunnel),
        new(AppCommand.GoToLastTab, [new(D9, Command)], KeyPass.Tunnel),
        new(AppCommand.GoToNextDashboard, [new(D, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.OpenRepository, [new(O, Command)], KeyPass.Tunnel),
        new(AppCommand.CloneRepository, [new(C, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.InitRepository, [new(I, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Fetch, [new(F, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Pull, [new(L, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Push, [new(K, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.CreateBranch, [new(B, Command)], KeyPass.Tunnel),
        new(AppCommand.Stash, [new(S, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Pop, [new(O, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Undo, [new(Z, Command)], KeyPass.Bubble),
        new(AppCommand.Redo, [new(Y, Command), new(Z, Command | Shift)], KeyPass.Bubble, ShownOnCtrl: 0, ShownOnCmd: 1),
        new(AppCommand.Search, [new(F, Command)], KeyPass.Tunnel),
        new(AppCommand.NextMatch, [new(F3), new(G, Command)], KeyPass.Tunnel, ShownOnCtrl: 0, ShownOnCmd: 1),
        new(AppCommand.PreviousMatch, [new(F3, Shift), new(G, Command | Shift)], KeyPass.Tunnel, ShownOnCtrl: 0, ShownOnCmd: 1),
        new(AppCommand.ShowReflog, [new(R, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.StageAll, [new(A, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.UnstageAll, [new(X, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.GoToCommitMessage, [new(M, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.Commit, [new(Enter, Command)], KeyPass.Tunnel),
        new(AppCommand.FileHistoryOfDiff, [new(H, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.BlameOfDiff, [new(B, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.FilterRefs, [new(E, Command | Shift)], KeyPass.Tunnel),
        new(AppCommand.NextRegion, [new(F6)], KeyPass.Tunnel),
        new(AppCommand.PreviousRegion, [new(F6, Shift)], KeyPass.Tunnel),

        // Drawn and listed only (6.1's second table, 6.7's menus, 6.8's tooltips).
        new(AppCommand.ContextMenu, [new(F10, Shift), new(Menu)], KeyPass.View),
        new(AppCommand.TextUndo, [new(Z, Command)], KeyPass.View),
        new(AppCommand.TextRedo, [new(Y, Command), new(Z, Command | Shift)], KeyPass.View, ShownOnCtrl: 0, ShownOnCmd: 1),
        new(AppCommand.Cut, [new(X, Command)], KeyPass.View),
        new(AppCommand.Copy, [new(C, Command)], KeyPass.View),
        new(AppCommand.Paste, [new(V, Command)], KeyPass.View),
        new(AppCommand.SelectAll, [new(A, Command)], KeyPass.View),
        new(AppCommand.RebasePick, [new(P)], KeyPass.View),
        new(AppCommand.RebaseReword, [new(R)], KeyPass.View),
        new(AppCommand.RebaseSquash, [new(S)], KeyPass.View),
        new(AppCommand.RebaseFixup, [new(F)], KeyPass.View),
        new(AppCommand.RebaseDrop, [new(D)], KeyPass.View),
        new(AppCommand.PreviousConflict, [new(Up, Alt)], KeyPass.View),
        new(AppCommand.NextConflict, [new(Down, Alt)], KeyPass.View),
        new(AppCommand.PreviousFile, [new(Left, Alt)], KeyPass.View),
        new(AppCommand.NextFile, [new(Right, Alt)], KeyPass.View),
        new(AppCommand.Close, [new(Escape)], KeyPass.View),
    ];

    /// <summary>
    /// The command a key on the tunnelling pass runs on <paramref name="platform"/>, with its
    /// argument, or null: the key goes on (6.3). Work package WP-K gives it its body; until then
    /// every key goes on.
    /// </summary>
    public static RoutedCommand? Route(Shortcut shortcut, CommandKey platform) => null;

    /// <summary>
    /// The key of <paramref name="command"/> that tooltips, menus and the palette show on
    /// <paramref name="platform"/>, or null for a command without one (6.4). Work package WP-K
    /// gives it its body; until then none is shown.
    /// </summary>
    public static Shortcut? Shown(AppCommand command, CommandKey platform) => null;

    /// <summary>Every key of <paramref name="command"/> (6.4). Work package WP-K gives it its body; until then none.</summary>
    public static IReadOnlyList<Shortcut> All(AppCommand command) => [];

    /// <summary>The settings screen's Keyboard shortcuts section on <paramref name="platform"/> (6.11). Work package WP-K gives it its body; until then no group.</summary>
    public static IReadOnlyList<ShortcutGroup> Groups(CommandKey platform) => [];
}
