using Avalonia.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.ViewModels.Commands.ShortcutKey;
using static VisualCommit.App.ViewModels.Commands.ShortcutModifiers;

namespace VisualCommit.App.Tests;

/// <summary>
/// The fixed key table (D192, D199, D213; docs/phase-6-design.md 6.1, 6.11, 6.12): no clash, no
/// key a text box or the platform takes, none of the keys 6.1 leaves free, every key in exactly one
/// group of the settings list, which equals 6.11's table on both platforms, and every key the
/// router takes a case of the window's commands. Pure.
/// </summary>
public class AppShortcutsTests
{
    private static readonly IReadOnlyList<CommandKey> Platforms = [CommandKey.Ctrl, CommandKey.Cmd];

    /// <summary>The settings list in macOS's words (6.11), written out by hand.</summary>
    private static readonly IReadOnlyList<ShortcutGroup> MacShortcutTable =
    [
        new("App", [new("Command palette", "Cmd+Shift+P"), new("Open settings…", "Cmd+,"), new("Show or hide the activity log", "Cmd+J"), new("Refresh", "Cmd+R, F5")], null),
        new("Tabs", [new("New tab", "Cmd+T"), new("Close tab", "Cmd+W"), new("Next tab", "Ctrl+Tab, Ctrl+PageDown"), new("Previous tab", "Ctrl+Shift+Tab, Ctrl+PageUp"), new("Go to tab 1 to 8", "Cmd+1 to Cmd+8"), new("Go to the last tab", "Cmd+9"), new("Go to the next dashboard", "Cmd+Shift+D")], null),
        new("Repositories", [new("Open repository…", "Cmd+O"), new("Clone repository…", "Cmd+Shift+C"), new("Init repository…", "Cmd+Shift+I")], null),
        new(
            "Repository",
            [
                new("Fetch all remotes", "Cmd+Shift+F"), new("Pull", "Cmd+Shift+L"), new("Push", "Cmd+Shift+K"), new("Create branch…", "Cmd+B"),
                new("Stash changes…", "Cmd+Shift+S"), new("Pop the newest stash", "Cmd+Shift+O"), new("Undo", "Cmd+Z"), new("Redo", "Cmd+Shift+Z, Cmd+Y"),
                new("Search commits", "Cmd+F"), new("Next match", "Cmd+G, F3"), new("Previous match", "Cmd+Shift+G, Shift+F3"), new("Show reflog", "Cmd+Shift+R"),
            ],
            "On a dashboard, Fetch and Pull fetch and pull every repository of its workspace."),
        new("Changes", [new("Stage all", "Cmd+Shift+A"), new("Unstage all", "Cmd+Shift+X"), new("Go to the commit message", "Cmd+Shift+M"), new("Commit, in the message boxes", "Cmd+Enter")], null),
        new("Files", [new("File history of the open diff's file", "Cmd+Shift+H"), new("Blame of the open diff's file", "Cmd+Shift+B"), new("Close the diff, a page or the search bar", "Esc")], null),
        new(
            "Moving around",
            [
                new("Filter branches and tags", "Cmd+Shift+E"), new("Next region", "F6"), new("Previous region", "Shift+F6"), new("Context menu", "Shift+F10"),
                new("Move in the graph", "Up, Down, PageUp, PageDown, Home, End"),
                new("Extend the graph's selection", "Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End"),
            ],
            "On a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless \"Use F1, F2, etc. keys as standard function keys\" is on; Home, End, PageUp and PageDown are Fn with the arrows."),
        new("Interactive rebase", [new("Move the selected row", "Option+Up, Option+Down"), new("Pick, reword, squash, fixup, drop", "P, R, S, F, D")], null),
        new("Conflict resolver", [new("Previous or next conflict", "Option+Up, Option+Down"), new("Previous or next file", "Option+Left, Option+Right"), new("Leave the result", "Ctrl+Tab")], null),
        new("Dashboard", [new("Open the repository", "Enter"), new("Remove from workspace", "Cmd+Delete, Forward Delete"), new("Move the repository", "Option+Up, Option+Down")], null),
        new("Dialogs and menus", [new("Confirm", "Enter"), new("Confirm from a box of several lines", "Cmd+Enter"), new("Cancel or close", "Esc")], null),
    ];

    /// <summary>
    /// The keys a text box, AvaloniaEdit's text area or the platform takes (docs/phase-6-design.md
    /// 0.7), written out for Win32 and for macOS: the platform's text gestures (copy, cut, paste,
    /// undo, redo, select all), the line and document moves, the arrows with Shift and the word
    /// modifier, Back and Delete, Tab, Space, the context menu, Back, the pages, and AvaloniaEdit's
    /// Ctrl+D, Ctrl+I, Ctrl+H. Undo and Redo go by the bubbling pass (they are no tunnelling keys).
    /// </summary>
    private static IReadOnlyList<Shortcut> TextGestures(CommandKey platform)
    {
        var mac = platform == CommandKey.Cmd;
        var word = mac ? Alt : Command;
        // Ctrl+Insert and Shift+Insert (copy, paste) are left out: the app has no Insert key, so no key of 6.1 can be them.
        var keys = new List<Shortcut>
        {
            new(C, Command), new(X, Command), new(V, Command),
            new(Z, Command), new(Y, Command), new(Z, Command | Shift), new(A, Command),
            new(Menu), new(Left, Alt), new(PageUp, Shift), new(PageDown, Shift), new(PageUp), new(PageDown),
            new(Backspace), new(Delete), new(Backspace, word), new(Delete, word), new(Tab), new(Tab, Shift), new(Space), new(Enter),
            new(D, Control), new(I, Control), new(H, Control),
        };
        foreach (var key in new[] { Home, End })
        {
            keys.AddRange([new(key), new(key, Shift), new(key, Command), new(key, Command | Shift)]);
        }

        foreach (var key in new[] { Left, Right, Up, Down })
        {
            keys.AddRange([new(key), new(key, Shift), new(key, word), new(key, word | Shift)]);
        }

        if (mac)
        {
            keys.AddRange([new(Left, Command), new(Right, Command), new(Left, Command | Shift), new(Right, Command | Shift)]);
        }
        else
        {
            keys.Add(new(F10, Shift));
        }

        return keys;
    }

    /// <summary>The keys 6.1 leaves free on purpose (D213), written out.</summary>
    private static IReadOnlyList<Shortcut> FreeKeys(CommandKey platform)
    {
        var keys = new List<Shortcut> { new(P, Command), new(N, Command), new(Q, Command), new(H, Command), new(M, Command), new(U, Control | Shift), new(F10), new(F11), new(F12) };
        for (var digit = D0; digit <= D9; digit++)
        {
            keys.Add(new(digit, Command | Shift));
        }

        for (var letter = A; letter <= Z; letter++)
        {
            keys.Add(new(letter, Alt));
            keys.Add(new(letter, Control | Alt));
            keys.Add(new(letter, Control | Alt | Shift));
        }

        for (var function = F1; function <= F12; function++)
        {
            keys.Add(new(function, Control));
        }

        return keys;
    }

    [Fact]
    public void No_two_of_the_routers_commands_share_a_key_and_none_takes_a_views_key()
    {
        foreach (var platform in Platforms)
        {
            var router = AppShortcuts.Bindings.Where(binding => binding.Pass is KeyPass.Tunnel or KeyPass.Bubble).ToList();
            var view = AppShortcuts.Bindings.Where(binding => binding.Pass == KeyPass.View).SelectMany(binding => binding.Keys.Select(key => Normalised(key, platform))).ToHashSet();
            var seen = new Dictionary<Shortcut, AppCommand>();
            foreach (var binding in router)
            {
                foreach (var key in binding.Keys.Select(key => Normalised(key, platform)))
                {
                    Assert.True(seen.TryAdd(key, binding.Command), $"{binding.Command} and {(seen.TryGetValue(key, out var other) ? other : default)} share {ShortcutText.Format(key, platform)} on {platform}.");

                    // A view's keys are its own: undo and redo are the window's as the text gestures
                    // TextUndo and TextRedo are an editor's, by the bubbling pass (D163).
                    if (binding.Pass == KeyPass.Tunnel)
                    {
                        Assert.False(view.Contains(key), $"{binding.Command}'s {ShortcutText.Format(key, platform)} is a view's key on {platform}.");
                    }
                }
            }
        }
    }

    [Fact]
    public void No_tunnelling_key_is_a_text_gesture_of_the_platform_or_one_left_free()
    {
        foreach (var platform in Platforms)
        {
            var text = TextGestures(platform).Select(key => Normalised(key, platform)).ToHashSet();
            var free = FreeKeys(platform).Select(key => Normalised(key, platform)).ToHashSet();
            foreach (var binding in AppShortcuts.Bindings.Where(binding => binding.Pass == KeyPass.Tunnel))
            {
                foreach (var key in binding.Keys.Select(key => Normalised(key, platform)))
                {
                    var words = ShortcutText.Format(key, platform);
                    Assert.False(free.Contains(key), $"{binding.Command}'s {words} is a key 6.1 leaves free on {platform}.");

                    // Mod+F is phase 5's Search, taken since phase 5 with the editors' search panel
                    // removed; Mod+Enter commits only from the message boxes, taken on the way down
                    // there (6.10). No other key of 6.1 is a text box's or a text area's (0.7).
                    if (binding.Command is AppCommand.Search or AppCommand.Commit)
                    {
                        continue;
                    }

                    Assert.False(text.Contains(key), $"{binding.Command}'s {words} is a text gesture on {platform}.");
                }
            }
        }
    }

    [Fact]
    public void Every_key_of_the_router_and_of_the_views_listed_is_in_exactly_one_group()
    {
        var rows = AppShortcuts.SettingsRows;
        foreach (var binding in AppShortcuts.Bindings)
        {
            var count = rows.Count(row => row.Commands.Contains(binding.Command));

            // The resolver's result's text keys are not listed (6.1's second table, its last row).
            var listed = binding.Command is not (AppCommand.TextUndo or AppCommand.TextRedo or AppCommand.Cut or AppCommand.Copy or AppCommand.Paste or AppCommand.SelectAll);
            Assert.True(count == (listed ? 1 : 0), $"{binding.Command} is in {count} rows of the settings list.");
        }
    }

    [Fact]
    public void The_settings_list_is_6_11s_table_on_windows_and_linux_and_on_macOS()
    {
        SettingsDialogViewTests.AssertGroups(SettingsDialogViewTests.ShortcutTable, AppShortcuts.Groups(CommandKey.Ctrl));
        SettingsDialogViewTests.AssertGroups(MacShortcutTable, AppShortcuts.Groups(CommandKey.Cmd));
    }

    [Fact]
    public void Each_key_routes_to_its_command_and_Go_to_tab_to_its_place()
    {
        foreach (var platform in Platforms)
        {
            foreach (var binding in AppShortcuts.Bindings)
            {
                for (var index = 0; index < binding.Keys.Count; index++)
                {
                    var routed = AppShortcuts.Route(binding.Keys[index], platform);
                    if (binding.Pass == KeyPass.Tunnel)
                    {
                        Assert.Equal(new RoutedCommand(binding.Command, binding.Command == AppCommand.GoToTab ? index + 1 : 0), routed);
                    }
                    else if (binding.Pass == KeyPass.Bubble)
                    {
                        // Undo and redo keep phase 5's bubbling rules (6.3).
                        Assert.Null(routed);
                    }
                }
            }
        }

        // Control is the command modifier on Windows and Linux: Ctrl+Tab pressed reads as Mod+Tab.
        Assert.Equal(new RoutedCommand(AppCommand.NextTab), AppShortcuts.Route(new Shortcut(Tab, Command), CommandKey.Ctrl));
        Assert.Null(AppShortcuts.Route(new Shortcut(Tab, Command), CommandKey.Cmd));
        Assert.Equal(new RoutedCommand(AppCommand.NextTab), AppShortcuts.Route(new Shortcut(Tab, Control), CommandKey.Cmd));
        Assert.Null(AppShortcuts.Route(new Shortcut(P, Command), CommandKey.Ctrl));
        Assert.Null(AppShortcuts.Route(new Shortcut(F6, Alt), CommandKey.Ctrl));
    }

    [Fact]
    public void A_key_event_reads_in_the_apps_words_and_the_windows_key_is_no_word()
    {
        // 6.4: the command modifier is Command; Control on a Meta platform is Control; Meta on a Control platform has no word.
        Assert.Equal(new Shortcut(P, Command | Shift), ShortcutKeys.From(Key.P, KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Control));
        Assert.Equal(new Shortcut(P, Command | Shift), ShortcutKeys.From(Key.P, KeyModifiers.Meta | KeyModifiers.Shift, KeyModifiers.Meta));
        Assert.Equal(new Shortcut(Tab, Control), ShortcutKeys.From(Key.Tab, KeyModifiers.Control, KeyModifiers.Meta));
        Assert.Null(ShortcutKeys.From(Key.P, KeyModifiers.Meta | KeyModifiers.Control, KeyModifiers.Control));
        Assert.Equal(new Shortcut(D1, Command), ShortcutKeys.From(Key.NumPad1, KeyModifiers.Control, KeyModifiers.Control));
        Assert.Equal(new Shortcut(Comma, Command), ShortcutKeys.From(Key.OemComma, KeyModifiers.Control, KeyModifiers.Control));
        Assert.Equal(new Shortcut(Menu), ShortcutKeys.From(Key.Apps, KeyModifiers.None, KeyModifiers.Control));
        Assert.Equal(new Shortcut(Backspace, Command), ShortcutKeys.From(Key.Back, KeyModifiers.Meta, KeyModifiers.Meta));
        Assert.Equal(new Shortcut(Enter, Command), ShortcutKeys.From(Key.Return, KeyModifiers.Control, KeyModifiers.Control));
        Assert.Equal(new Shortcut(PageDown, Control), ShortcutKeys.From(Key.Next, KeyModifiers.Control, KeyModifiers.Meta));
        Assert.Null(ShortcutKeys.From(Key.OemPeriod, KeyModifiers.Control, KeyModifiers.Control));

        // And back, for the harnesses.
        Assert.Equal(new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift), ShortcutKeys.ToGesture(new Shortcut(P, Command | Shift), KeyModifiers.Control));
        Assert.Equal(new KeyGesture(Key.Tab, KeyModifiers.Control), ShortcutKeys.ToGesture(new Shortcut(Tab, Control), KeyModifiers.Meta));
        Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), ShortcutKeys.ToGesture(new Shortcut(Comma, Command), KeyModifiers.Meta));
    }

    [Fact]
    public void The_command_modifier_and_whose_words_it_takes_come_from_the_hotkey_configuration_alone()
    {
        // 6.1, 6.3, 6.4: the one place the router, the dialogs' Mod+Enter and the key texts read them.
        Assert.Equal(KeyModifiers.Meta, ShortcutKeys.CommandModifierOf(KeyModifiers.Meta));
        Assert.Equal(KeyModifiers.Control, ShortcutKeys.CommandModifierOf(KeyModifiers.Control));
        Assert.Equal(KeyModifiers.Control, ShortcutKeys.CommandModifierOf(KeyModifiers.None));
        Assert.Equal(KeyModifiers.Control, ShortcutKeys.CommandModifierOf((KeyModifiers?)null));
        Assert.Equal(KeyModifiers.Control, ShortcutKeys.CommandModifierOf((Avalonia.Input.Platform.PlatformHotkeyConfiguration?)null));
        Assert.Equal(CommandKey.Cmd, ShortcutKeys.CommandKeyOf(KeyModifiers.Meta));
        Assert.Equal(CommandKey.Ctrl, ShortcutKeys.CommandKeyOf(KeyModifiers.Control));
    }

    [Fact]
    public void The_commands_without_a_key_are_6_1s_list()
    {
        // D199: each reached through the palette and its menu or button. Discard all changes… is a
        // working-changes menu item without a key (6.7), so no command of this enum; the theme's
        // three; Choose an editor… (D226) and the dashboard's workspace edits are palette entries
        // of no command of this enum (PaletteEntriesTests).
        AppCommand[] withoutKey =
        [
            AppCommand.ShowKeyboardShortcuts, AppCommand.NewWorkspace, AppCommand.ShowBackups, AppCommand.CreateTagAtHead, AppCommand.AddRemote,
            AppCommand.ManageRemotes, AppCommand.ShowAllHiddenBranches, AppCommand.StopSoloing, AppCommand.UseDarkTheme, AppCommand.UseLightTheme,
            AppCommand.FollowSystemTheme,
        ];
        var keyed = AppShortcuts.Bindings.Select(binding => binding.Command).ToHashSet();
        Assert.Equal(withoutKey.Order(), Enum.GetValues<AppCommand>().Where(command => !keyed.Contains(command)).Order());
        Assert.All(withoutKey, command => Assert.Null(new KeyTexts(CommandKey.Ctrl).Of(command)));
    }

    [Fact]
    public void Every_key_the_router_takes_has_a_case_of_the_windows_commands()
    {
        // A table over the enum, written out (no reflection): each command the router takes, and
        // what the window's commands call it on a lone empty tab, where it does nothing but the
        // window's own commands (6.1's "Applies").
        var table = new Dictionary<AppCommand, (string Label, bool Applies)>
        {
            [AppCommand.CommandPalette] = ("Command palette", true),
            [AppCommand.Settings] = ("Open settings…", true),
            [AppCommand.ToggleActivityLog] = ("Show the activity log", false),
            [AppCommand.Refresh] = ("Refresh", false),
            [AppCommand.NewTab] = ("New tab", true),
            [AppCommand.CloseTab] = ("Close tab", true),
            [AppCommand.NextTab] = ("Next tab", true),
            [AppCommand.PreviousTab] = ("Previous tab", true),
            [AppCommand.GoToTab] = ("Go to tab 1", true),
            [AppCommand.GoToLastTab] = ("Go to the last tab", true),
            [AppCommand.GoToNextDashboard] = ("Go to the next dashboard", true),
            [AppCommand.OpenRepository] = ("Open repository…", true),
            [AppCommand.CloneRepository] = ("Clone repository…", true),
            [AppCommand.InitRepository] = ("Init repository…", true),
            [AppCommand.Fetch] = ("Fetch all remotes", false),
            [AppCommand.Pull] = ("Pull", false),
            [AppCommand.Push] = ("Push", false),
            [AppCommand.CreateBranch] = ("Create branch…", false),
            [AppCommand.Stash] = ("Stash changes…", false),
            [AppCommand.Pop] = ("Pop the newest stash", false),
            [AppCommand.Search] = ("Search commits", false),
            [AppCommand.NextMatch] = ("Next match", false),
            [AppCommand.PreviousMatch] = ("Previous match", false),
            [AppCommand.ShowReflog] = ("Show reflog", false),
            [AppCommand.StageAll] = ("Stage all", false),
            [AppCommand.UnstageAll] = ("Unstage all", false),
            [AppCommand.GoToCommitMessage] = ("Go to the commit message", false),
            [AppCommand.Commit] = ("Commit", false),
            [AppCommand.FileHistoryOfDiff] = ("File history", false),
            [AppCommand.BlameOfDiff] = ("Blame", false),
            [AppCommand.FilterRefs] = ("Filter branches and tags", false),
            [AppCommand.NextRegion] = ("Next region", true),
            [AppCommand.PreviousRegion] = ("Previous region", true),
        };
        Assert.Equal(table.Keys.Order(), AppShortcuts.Bindings.Where(binding => binding.Pass == KeyPass.Tunnel).Select(binding => binding.Command).Order());

        var shell = new MainWindowViewModel(
            new MainWindowViewModelTests.FakeSettings(),
            new MainWindowViewModelTests.FakeThemes(),
            _ => Task.FromResult(new GitDetection(GitAvailability.Available, "git", new GitVersion(2, 36, 0), null)));
        var commands = new AppCommands(shell, new KeyTexts(CommandKey.Ctrl));
        foreach (var (command, (label, applies)) in table)
        {
            var state = commands.Describe(command, command == AppCommand.GoToTab ? 1 : 0);
            Assert.Equal((label, applies), (state.Label, state.IsListed));
        }
    }

    /// <summary>A key with Control and the command modifier one where the command modifier is Control (6.12).</summary>
    private static Shortcut Normalised(Shortcut key, CommandKey platform) =>
        platform == CommandKey.Ctrl && key.Modifiers.HasFlag(Control)
            ? key with { Modifiers = (key.Modifiers & ~Control) | Command }
            : key;
}
