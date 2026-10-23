using VisualCommit.App.ViewModels.Operations;
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
/// it. The router routes by it (6.3), every key text comes from it (6.4) and the settings
/// screen's list is made from it (6.11).
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
    /// argument (the place for Mod+1 to Mod+8, from 1), or null: the key goes on (6.3). Only the
    /// router's own keys (<see cref="KeyPass.Tunnel"/>) route; undo and redo keep phase 5's
    /// bubbling rules, and the views' keys are their own.
    /// </summary>
    public static RoutedCommand? Route(Shortcut shortcut, CommandKey platform)
    {
        var pressed = Normalised(shortcut, platform);
        foreach (var binding in Bindings)
        {
            if (binding.Pass != KeyPass.Tunnel)
            {
                continue;
            }

            for (var index = 0; index < binding.Keys.Count; index++)
            {
                if (Normalised(binding.Keys[index], platform) == pressed)
                {
                    return new RoutedCommand(binding.Command, binding.Command == AppCommand.GoToTab ? index + 1 : 0);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The key of <paramref name="command"/> that tooltips, menus and the palette show on
    /// <paramref name="platform"/>, or null for a command without one (6.4): of two keys the
    /// binding's <see cref="ShortcutBinding.ShownOnCtrl"/> or <see cref="ShortcutBinding.ShownOnCmd"/>
    /// (Refresh F5 and Cmd+R, Redo Ctrl+Y and Cmd+Shift+Z, the matches F3 and Cmd+G).
    /// </summary>
    public static Shortcut? Shown(AppCommand command, CommandKey platform) =>
        BindingOf(command) is { Keys.Count: > 0 } binding
            ? binding.Keys[platform == CommandKey.Cmd ? binding.ShownOnCmd : binding.ShownOnCtrl]
            : null;

    /// <summary>Every key of <paramref name="command"/> (6.4), in the catalogue's order; none for a command without a key.</summary>
    public static IReadOnlyList<Shortcut> All(AppCommand command) => BindingOf(command)?.Keys ?? [];

    /// <summary>
    /// Every key of <paramref name="command"/> with the shown one first (6.11): what the settings
    /// list writes, <c>F5, Ctrl+R</c> but <c>Cmd+R, F5</c> on macOS.
    /// </summary>
    public static IReadOnlyList<Shortcut> ShownFirst(AppCommand command, CommandKey platform)
    {
        if (BindingOf(command) is not { Keys.Count: > 0 } binding)
        {
            return [];
        }

        var shown = platform == CommandKey.Cmd ? binding.ShownOnCmd : binding.ShownOnCtrl;
        return [binding.Keys[shown], .. binding.Keys.Where((_, index) => index != shown)];
    }

    /// <summary>
    /// The settings screen's Keyboard shortcuts section on <paramref name="platform"/> (6.11): its
    /// groups in order, each row 6.1's action name with its keys joined by <c>, </c>, the shown key
    /// first, in the platform's words.
    /// </summary>
    public static IReadOnlyList<ShortcutGroup> Groups(CommandKey platform) =>
        [.. SettingsGroups.Select(group => new ShortcutGroup(
            group.Title,
            [.. group.Rows.Select(row => new ShortcutRow(row.Action, ShortcutText.FormatAll(row.Keys(platform), platform)))],
            group.Note?.Invoke(platform)))];

    /// <summary>
    /// The catalogue's commands each row of the settings list shows the keys of, row by row as
    /// <see cref="Groups"/> lists them (6.11): one command for most rows, several for a row such as
    /// <c>Previous or next conflict</c>, none for keys that are no command of the catalogue (the
    /// graph's arrows, the dashboard's list, the dialogs').
    /// </summary>
    public static IReadOnlyList<(string Group, string Action, IReadOnlyList<AppCommand> Commands)> SettingsRows =>
        [.. SettingsGroups.SelectMany(group => group.Rows.Select(row => (group.Title, row.Action, row.Commands)))];

    /// <summary>The dashboard list's Enter, which opens the selected row (docs/phase-6-design.md 2.7): its view handles it; the settings list and the row menu (6.7) show it.</summary>
    public static Shortcut DashboardOpen => new(Enter);

    /// <summary>The dashboard list's Alt+Up, which moves the selected row up (2.7, 1.10).</summary>
    public static Shortcut DashboardMoveUp => new(Up, Alt);

    /// <summary>The dashboard list's Alt+Down, which moves the selected row down (2.7, 1.10).</summary>
    public static Shortcut DashboardMoveDown => new(Down, Alt);

    /// <summary>
    /// The dashboard list's keys that remove the selected row on <paramref name="platform"/> (2.7):
    /// Delete; on macOS Cmd+Backspace first (the key a Mac labels "delete" sends Backspace), then
    /// Delete. The row menu shows the first.
    /// </summary>
    public static IReadOnlyList<Shortcut> DashboardRemove(CommandKey platform) =>
        platform == CommandKey.Cmd ? [new(Backspace, Command), new(Delete)] : [new(Delete)];

    /// <summary>The binding of <paramref name="command"/>, or null for a command without a key.</summary>
    private static ShortcutBinding? BindingOf(AppCommand command) => Bindings.FirstOrDefault(binding => binding.Command == command);

    /// <summary>
    /// <paramref name="shortcut"/> with Control and the command modifier one where the command
    /// modifier is Control (Windows and Linux): Ctrl+Tab is then Mod+Tab.
    /// </summary>
    private static Shortcut Normalised(Shortcut shortcut, CommandKey platform) =>
        platform == CommandKey.Ctrl && shortcut.Modifiers.HasFlag(ShortcutModifiers.Control)
            ? shortcut with { Modifiers = (shortcut.Modifiers & ~ShortcutModifiers.Control) | ShortcutModifiers.Command }
            : shortcut;

    /// <summary>A row of the settings list from the catalogue's commands: their keys in the row's order, each command's shown key first.</summary>
    private static SettingsRow FromCommands(string action, params AppCommand[] commands) =>
        new(action, platform => [.. commands.SelectMany(command => ShownFirst(command, platform))], commands);

    /// <summary>A row of the settings list for keys that are no command of the catalogue: the same keys on every platform.</summary>
    private static SettingsRow FromKeys(string action, params Shortcut[] keys) => new(action, _ => keys, []);

    /// <summary>6.11's groups, in order, with 6.1's action names (from both of its tables).</summary>
    private static IReadOnlyList<SettingsGroup> SettingsGroups { get; } =
    [
        new("App",
        [
            FromCommands("Command palette", AppCommand.CommandPalette),
            FromCommands("Open settings…", AppCommand.Settings),
            FromCommands("Show or hide the activity log", AppCommand.ToggleActivityLog),
            FromCommands("Refresh", AppCommand.Refresh),
        ]),
        new("Tabs",
        [
            FromCommands("New tab", AppCommand.NewTab),
            FromCommands("Close tab", AppCommand.CloseTab),
            FromCommands("Next tab", AppCommand.NextTab),
            FromCommands("Previous tab", AppCommand.PreviousTab),
            FromCommands("Go to tab 1 to 8", AppCommand.GoToTab),
            FromCommands("Go to the last tab", AppCommand.GoToLastTab),
            FromCommands("Go to the next dashboard", AppCommand.GoToNextDashboard),
        ]),
        new("Repositories",
        [
            FromCommands("Open repository…", AppCommand.OpenRepository),
            FromCommands("Clone repository…", AppCommand.CloneRepository),
            FromCommands("Init repository…", AppCommand.InitRepository),
        ]),
        new(
            "Repository",
            [
                FromCommands("Fetch all remotes", AppCommand.Fetch),
                FromCommands(OperationTexts.MenuTexts.ToolbarPull, AppCommand.Pull),
                FromCommands(OperationTexts.MenuTexts.ToolbarPush, AppCommand.Push),
                FromCommands("Create branch…", AppCommand.CreateBranch),
                FromCommands("Stash changes…", AppCommand.Stash),
                FromCommands("Pop the newest stash", AppCommand.Pop),
                FromCommands("Undo", AppCommand.Undo),
                FromCommands("Redo", AppCommand.Redo),
                FromCommands("Search commits", AppCommand.Search),
                FromCommands("Next match", AppCommand.NextMatch),
                FromCommands("Previous match", AppCommand.PreviousMatch),
                FromCommands("Show reflog", AppCommand.ShowReflog),
            ],
            _ => "On a dashboard, Fetch and Pull fetch and pull every repository of its workspace."),
        new("Changes",
        [
            FromCommands("Stage all", AppCommand.StageAll),
            FromCommands("Unstage all", AppCommand.UnstageAll),
            FromCommands("Go to the commit message", AppCommand.GoToCommitMessage),
            FromCommands("Commit, in the message boxes", AppCommand.Commit),
        ]),
        new("Files",
        [
            FromCommands("File history of the open diff's file", AppCommand.FileHistoryOfDiff),
            FromCommands("Blame of the open diff's file", AppCommand.BlameOfDiff),
            FromCommands("Close the diff, a page or the search bar", AppCommand.Close),
        ]),
        new(
            "Moving around",
            [
                FromCommands("Filter branches and tags", AppCommand.FilterRefs),
                FromCommands("Next region", AppCommand.NextRegion),
                FromCommands("Previous region", AppCommand.PreviousRegion),

                // A Mac keyboard has no Menu key (6.1's second table): Shift+F10 alone there.
                new("Context menu", platform => platform == CommandKey.Cmd ? [new(F10, Shift)] : ShownFirst(AppCommand.ContextMenu, platform), [AppCommand.ContextMenu]),
                FromKeys("Move in the graph", new(Up), new(Down), new(PageUp), new(PageDown), new(Home), new(End)),
                FromKeys("Extend the graph's selection", new(Up, Shift), new(Down, Shift), new(PageUp, Shift), new(PageDown, Shift), new(Home, Shift), new(End, Shift)),
            ],
            platform => platform == CommandKey.Cmd
                ? "On a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless \"Use F1, F2, etc. keys as standard function keys\" is on; Home, End, PageUp and PageDown are Fn with the arrows."
                : null),
        new("Interactive rebase",
        [
            FromKeys("Move the selected row", new(Up, Alt), new(Down, Alt)),
            FromCommands("Pick, reword, squash, fixup, drop", AppCommand.RebasePick, AppCommand.RebaseReword, AppCommand.RebaseSquash, AppCommand.RebaseFixup, AppCommand.RebaseDrop),
        ]),
        new("Conflict resolver",
        [
            FromCommands("Previous or next conflict", AppCommand.PreviousConflict, AppCommand.NextConflict),
            FromCommands("Previous or next file", AppCommand.PreviousFile, AppCommand.NextFile),

            // The result leaves Tab with Control to the window's navigation (phase 4 design 11.10): Control on every platform.
            FromKeys("Leave the result", new Shortcut(Tab, Control)),
        ]),
        new("Dashboard",
        [
            FromKeys("Open the repository", DashboardOpen),

            // On a Mac the key labelled "delete" sends Backspace, so Cmd+Backspace there too (2.7).
            new("Remove from workspace", DashboardRemove, []),
            FromKeys("Move the repository", DashboardMoveUp, DashboardMoveDown),
        ]),
        new("Dialogs and menus",
        [
            FromKeys("Confirm", new Shortcut(Enter)),
            FromKeys("Confirm from a box of several lines", new Shortcut(Enter, Command)),
            FromKeys("Cancel or close", new Shortcut(Escape)),
        ]),
    ];

    /// <summary>One row of the settings list: its action, its keys on a platform and the catalogue's commands it lists.</summary>
    private sealed record SettingsRow(string Action, Func<CommandKey, IReadOnlyList<Shortcut>> Keys, IReadOnlyList<AppCommand> Commands);

    /// <summary>One group of the settings list: its title, its rows and its note on a platform.</summary>
    private sealed record SettingsGroup(string Title, IReadOnlyList<SettingsRow> Rows, Func<CommandKey, string?>? Note = null);
}
