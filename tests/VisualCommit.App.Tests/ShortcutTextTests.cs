using VisualCommit.App.ViewModels.Commands;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The one formatter of key texts (docs/phase-6-design.md 6.4, 6.12, 14.12, D214): every binding of
/// the catalogue in Windows' and Linux's words and in macOS's, against a table written out by
/// hand, never read from the operating system (D163's rule). Pure.
/// </summary>
public class ShortcutTextTests
{
    /// <summary>
    /// Every command's keys as 6.1 writes them: all of them (in the catalogue's order) and the shown
    /// one, on Windows and Linux, then on macOS.
    /// </summary>
    private static readonly Dictionary<AppCommand, (string CtrlAll, string CtrlShown, string CmdAll, string CmdShown)> Table = new()
    {
        [AppCommand.CommandPalette] = ("Ctrl+Shift+P", "Ctrl+Shift+P", "Cmd+Shift+P", "Cmd+Shift+P"),
        [AppCommand.Settings] = ("Ctrl+,", "Ctrl+,", "Cmd+,", "Cmd+,"),
        [AppCommand.ToggleActivityLog] = ("Ctrl+J", "Ctrl+J", "Cmd+J", "Cmd+J"),
        [AppCommand.Refresh] = ("F5, Ctrl+R", "F5", "F5, Cmd+R", "Cmd+R"),
        [AppCommand.NewTab] = ("Ctrl+T", "Ctrl+T", "Cmd+T", "Cmd+T"),
        [AppCommand.CloseTab] = ("Ctrl+W", "Ctrl+W", "Cmd+W", "Cmd+W"),
        [AppCommand.NextTab] = ("Ctrl+Tab, Ctrl+PageDown", "Ctrl+Tab", "Ctrl+Tab, Ctrl+PageDown", "Ctrl+Tab"),
        [AppCommand.PreviousTab] = ("Ctrl+Shift+Tab, Ctrl+PageUp", "Ctrl+Shift+Tab", "Ctrl+Shift+Tab, Ctrl+PageUp", "Ctrl+Shift+Tab"),
        [AppCommand.GoToTab] = ("Ctrl+1 to Ctrl+8", "Ctrl+1", "Cmd+1 to Cmd+8", "Cmd+1"),
        [AppCommand.GoToLastTab] = ("Ctrl+9", "Ctrl+9", "Cmd+9", "Cmd+9"),
        [AppCommand.GoToNextDashboard] = ("Ctrl+Shift+D", "Ctrl+Shift+D", "Cmd+Shift+D", "Cmd+Shift+D"),
        [AppCommand.OpenRepository] = ("Ctrl+O", "Ctrl+O", "Cmd+O", "Cmd+O"),
        [AppCommand.CloneRepository] = ("Ctrl+Shift+C", "Ctrl+Shift+C", "Cmd+Shift+C", "Cmd+Shift+C"),
        [AppCommand.InitRepository] = ("Ctrl+Shift+I", "Ctrl+Shift+I", "Cmd+Shift+I", "Cmd+Shift+I"),
        [AppCommand.Fetch] = ("Ctrl+Shift+F", "Ctrl+Shift+F", "Cmd+Shift+F", "Cmd+Shift+F"),
        [AppCommand.Pull] = ("Ctrl+Shift+L", "Ctrl+Shift+L", "Cmd+Shift+L", "Cmd+Shift+L"),
        [AppCommand.Push] = ("Ctrl+Shift+K", "Ctrl+Shift+K", "Cmd+Shift+K", "Cmd+Shift+K"),
        [AppCommand.CreateBranch] = ("Ctrl+B", "Ctrl+B", "Cmd+B", "Cmd+B"),
        [AppCommand.Stash] = ("Ctrl+Shift+S", "Ctrl+Shift+S", "Cmd+Shift+S", "Cmd+Shift+S"),
        [AppCommand.Pop] = ("Ctrl+Shift+O", "Ctrl+Shift+O", "Cmd+Shift+O", "Cmd+Shift+O"),
        [AppCommand.Undo] = ("Ctrl+Z", "Ctrl+Z", "Cmd+Z", "Cmd+Z"),
        [AppCommand.Redo] = ("Ctrl+Y, Ctrl+Shift+Z", "Ctrl+Y", "Cmd+Y, Cmd+Shift+Z", "Cmd+Shift+Z"),
        [AppCommand.Search] = ("Ctrl+F", "Ctrl+F", "Cmd+F", "Cmd+F"),
        [AppCommand.NextMatch] = ("F3, Ctrl+G", "F3", "F3, Cmd+G", "Cmd+G"),
        [AppCommand.PreviousMatch] = ("Shift+F3, Ctrl+Shift+G", "Shift+F3", "Shift+F3, Cmd+Shift+G", "Cmd+Shift+G"),
        [AppCommand.ShowReflog] = ("Ctrl+Shift+R", "Ctrl+Shift+R", "Cmd+Shift+R", "Cmd+Shift+R"),
        [AppCommand.StageAll] = ("Ctrl+Shift+A", "Ctrl+Shift+A", "Cmd+Shift+A", "Cmd+Shift+A"),
        [AppCommand.UnstageAll] = ("Ctrl+Shift+X", "Ctrl+Shift+X", "Cmd+Shift+X", "Cmd+Shift+X"),
        [AppCommand.GoToCommitMessage] = ("Ctrl+Shift+M", "Ctrl+Shift+M", "Cmd+Shift+M", "Cmd+Shift+M"),
        [AppCommand.Commit] = ("Ctrl+Enter", "Ctrl+Enter", "Cmd+Enter", "Cmd+Enter"),
        [AppCommand.FileHistoryOfDiff] = ("Ctrl+Shift+H", "Ctrl+Shift+H", "Cmd+Shift+H", "Cmd+Shift+H"),
        [AppCommand.BlameOfDiff] = ("Ctrl+Shift+B", "Ctrl+Shift+B", "Cmd+Shift+B", "Cmd+Shift+B"),
        [AppCommand.FilterRefs] = ("Ctrl+Shift+E", "Ctrl+Shift+E", "Cmd+Shift+E", "Cmd+Shift+E"),
        [AppCommand.NextRegion] = ("F6", "F6", "F6", "F6"),
        [AppCommand.PreviousRegion] = ("Shift+F6", "Shift+F6", "Shift+F6", "Shift+F6"),
        [AppCommand.ContextMenu] = ("Shift+F10, Menu", "Shift+F10", "Shift+F10, Menu", "Shift+F10"),
        [AppCommand.TextUndo] = ("Ctrl+Z", "Ctrl+Z", "Cmd+Z", "Cmd+Z"),
        [AppCommand.TextRedo] = ("Ctrl+Y, Ctrl+Shift+Z", "Ctrl+Y", "Cmd+Y, Cmd+Shift+Z", "Cmd+Shift+Z"),
        [AppCommand.Cut] = ("Ctrl+X", "Ctrl+X", "Cmd+X", "Cmd+X"),
        [AppCommand.Copy] = ("Ctrl+C", "Ctrl+C", "Cmd+C", "Cmd+C"),
        [AppCommand.Paste] = ("Ctrl+V", "Ctrl+V", "Cmd+V", "Cmd+V"),
        [AppCommand.SelectAll] = ("Ctrl+A", "Ctrl+A", "Cmd+A", "Cmd+A"),
        [AppCommand.RebasePick] = ("P", "P", "P", "P"),
        [AppCommand.RebaseReword] = ("R", "R", "R", "R"),
        [AppCommand.RebaseSquash] = ("S", "S", "S", "S"),
        [AppCommand.RebaseFixup] = ("F", "F", "F", "F"),
        [AppCommand.RebaseDrop] = ("D", "D", "D", "D"),
        [AppCommand.PreviousConflict] = ("Alt+Up", "Alt+Up", "Option+Up", "Option+Up"),
        [AppCommand.NextConflict] = ("Alt+Down", "Alt+Down", "Option+Down", "Option+Down"),
        [AppCommand.PreviousFile] = ("Alt+Left", "Alt+Left", "Option+Left", "Option+Left"),
        [AppCommand.NextFile] = ("Alt+Right", "Alt+Right", "Option+Right", "Option+Right"),
        [AppCommand.Close] = ("Esc", "Esc", "Esc", "Esc"),
    };

    [Fact]
    public void Every_binding_is_written_in_each_platforms_words()
    {
        Assert.Equal(Table.Keys.Order(), AppShortcuts.Bindings.Select(binding => binding.Command).Order());
        foreach (var binding in AppShortcuts.Bindings)
        {
            var (ctrlAll, ctrlShown, cmdAll, cmdShown) = Table[binding.Command];
            Assert.Equal(ctrlAll, ShortcutText.FormatAll(binding.Keys, CommandKey.Ctrl));
            Assert.Equal(cmdAll, ShortcutText.FormatAll(binding.Keys, CommandKey.Cmd));
            Assert.Equal(ctrlShown, new KeyTexts(CommandKey.Ctrl).Of(binding.Command));
            Assert.Equal(cmdShown, new KeyTexts(CommandKey.Cmd).Of(binding.Command));
        }
    }

    [Theory]
    [InlineData(ShortcutKey.P, ShortcutModifiers.Command | ShortcutModifiers.Shift, "Ctrl+Shift+P", "Cmd+Shift+P")]
    [InlineData(ShortcutKey.Comma, ShortcutModifiers.Command, "Ctrl+,", "Cmd+,")]
    [InlineData(ShortcutKey.Tab, ShortcutModifiers.Control, "Ctrl+Tab", "Ctrl+Tab")]
    [InlineData(ShortcutKey.PageDown, ShortcutModifiers.Control, "Ctrl+PageDown", "Ctrl+PageDown")]
    [InlineData(ShortcutKey.Up, ShortcutModifiers.Alt, "Alt+Up", "Option+Up")]
    [InlineData(ShortcutKey.F10, ShortcutModifiers.Shift, "Shift+F10", "Shift+F10")]
    [InlineData(ShortcutKey.F6, ShortcutModifiers.None, "F6", "F6")]
    [InlineData(ShortcutKey.D9, ShortcutModifiers.Command, "Ctrl+9", "Cmd+9")]
    [InlineData(ShortcutKey.Escape, ShortcutModifiers.None, "Esc", "Esc")]
    [InlineData(ShortcutKey.Enter, ShortcutModifiers.Command, "Ctrl+Enter", "Cmd+Enter")]
    [InlineData(ShortcutKey.Backspace, ShortcutModifiers.Command, "Ctrl+Backspace", "Cmd+Delete")]
    [InlineData(ShortcutKey.Delete, ShortcutModifiers.None, "Delete", "Forward Delete")]
    [InlineData(ShortcutKey.Space, ShortcutModifiers.None, "Space", "Space")]
    [InlineData(ShortcutKey.Menu, ShortcutModifiers.None, "Menu", "Menu")]
    [InlineData(ShortcutKey.Home, ShortcutModifiers.Shift, "Shift+Home", "Shift+Home")]
    public void A_key_is_written_in_words_joined_by_plus_on_both_platforms(ShortcutKey key, ShortcutModifiers modifiers, string ctrl, string cmd)
    {
        // 6.4, 14.12: the command modifier, the Control key where it is not the command modifier,
        // Shift, Alt (Option on macOS), then the key; on macOS Backspace is "Delete" and Delete
        // "Forward Delete" (Apple's words: the dashboard's Remove reads "Cmd+Delete").
        Assert.Equal(ctrl, ShortcutText.Format(new Shortcut(key, modifiers), CommandKey.Ctrl));
        Assert.Equal(cmd, ShortcutText.Format(new Shortcut(key, modifiers), CommandKey.Cmd));
    }

    [Fact]
    public void The_Control_key_with_the_command_modifier_is_written_once_on_windows_and_both_on_macOS()
    {
        var both = new Shortcut(ShortcutKey.K, ShortcutModifiers.Command | ShortcutModifiers.Control | ShortcutModifiers.Shift | ShortcutModifiers.Alt);
        Assert.Equal("Ctrl+Shift+Alt+K", ShortcutText.Format(both, CommandKey.Ctrl));
        Assert.Equal("Cmd+Ctrl+Shift+Option+K", ShortcutText.Format(both, CommandKey.Cmd));
    }

    [Fact]
    public void Several_keys_are_joined_by_commas_and_a_run_of_digits_reads_from_to()
    {
        Assert.Equal("F5, Ctrl+R", ShortcutText.FormatAll([new(ShortcutKey.F5), new(ShortcutKey.R, ShortcutModifiers.Command)], CommandKey.Ctrl));
        Assert.Equal("Cmd+R, F5", ShortcutText.FormatAll([new(ShortcutKey.R, ShortcutModifiers.Command), new(ShortcutKey.F5)], CommandKey.Cmd));
        Assert.Equal("Ctrl+1 to Ctrl+8", ShortcutText.FormatAll(AppShortcuts.All(AppCommand.GoToTab), CommandKey.Ctrl));
        Assert.Equal("Cmd+1 to Cmd+8", ShortcutText.FormatAll(AppShortcuts.All(AppCommand.GoToTab), CommandKey.Cmd));

        // Two digits are no run; a gap ends one.
        Assert.Equal("Ctrl+1, Ctrl+2", ShortcutText.FormatAll([new(ShortcutKey.D1, ShortcutModifiers.Command), new(ShortcutKey.D2, ShortcutModifiers.Command)], CommandKey.Ctrl));
        Assert.Equal(
            "Ctrl+1 to Ctrl+3, Ctrl+5",
            ShortcutText.FormatAll([new(ShortcutKey.D1, ShortcutModifiers.Command), new(ShortcutKey.D2, ShortcutModifiers.Command), new(ShortcutKey.D3, ShortcutModifiers.Command), new(ShortcutKey.D5, ShortcutModifiers.Command)], CommandKey.Ctrl));
        Assert.Equal(string.Empty, ShortcutText.FormatAll([], CommandKey.Ctrl));
    }

    [Fact]
    public void A_text_takes_the_key_after_it_in_brackets_or_stays_alone_without_one()
    {
        // 6.8: "{what it does} ({key})", or the text alone for a command without a key.
        Assert.Equal("Open settings (Ctrl+,)", new KeyTexts(CommandKey.Ctrl).WithKey("Open settings", AppCommand.Settings));
        Assert.Equal("Open settings (Cmd+,)", new KeyTexts(CommandKey.Cmd).WithKey("Open settings", AppCommand.Settings));
        Assert.Equal("Use the light theme", new KeyTexts(CommandKey.Ctrl).WithKey("Use the light theme", AppCommand.UseLightTheme));
        Assert.Null(new KeyTexts(CommandKey.Cmd).Of(AppCommand.ShowKeyboardShortcuts));
        Assert.Equal(CommandKey.Cmd, new KeyTexts(CommandKey.Cmd).Platform);
    }
}
