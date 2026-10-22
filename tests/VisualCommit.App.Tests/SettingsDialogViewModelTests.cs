using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.GitSettingsViewModelTests;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen's view models without a window (docs/phase-6-design.md 7.3, 7.5, 7.8,
/// 7.11, 12.10): General's fields from the settings and their rules, Tools' editor and Other…'s
/// checks, the Git section's scopes, and Save in 7.8's order, Cancel and Esc, over fakes: a
/// counting settings store, git's configuration, a repository whose save the test holds, the
/// fixed tool catalogues and the dialog layer.
/// </summary>
public class SettingsDialogViewModelTests
{
    private static readonly string Program = OperatingSystem.IsWindows() ? @"D:\Tools\Ed.exe" : "/opt/tools/ed";

    [Fact]
    public async Task A_default_settings_fields_and_the_section_it_opens_on()
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync(SettingsSection.Tools);

        Assert.Equal(("Settings", SettingsSection.Tools), (dialog.Title, dialog.SelectedSection));
        Assert.True(dialog.ShowsTools);
        Assert.Equal((AppTheme.Dark, true, false, false), (dialog.General.Theme, dialog.General.IsDark, dialog.General.IsLight, dialog.General.IsSystem));
        Assert.Equal((true, "5", true, true, string.Empty), (dialog.General.IsAutoFetchOn, dialog.General.MinutesText, dialog.General.KeepIgnoredFiles, dialog.General.StashIncludeUntracked, dialog.General.ExtraExtensionsText));
        Assert.Equal(
            ".exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop",
            dialog.General.RunnableList);
        Assert.Equal(OperatingSystem.IsWindows() ? "From PATHEXT: .wsh .msc" : null, dialog.General.PathExtText);
        Assert.Null(dialog.General.AutoFetchError);
        Assert.Null(dialog.General.RunnableError);
        Assert.Equal((EditorChoice.None, "None", false, false), (dialog.Tools.Choice, dialog.Tools.EditorValue, dialog.Tools.ShowsEditorProgram, dialog.Tools.IsOther));
        Assert.False(dialog.Tools.IsLooking);
        Assert.Equal(ToolCatalogues.FixedEditors, dialog.Tools.Found);
        Assert.False(dialog.GitHasError || dialog.GeneralHasError || dialog.ToolsHasError);
        Assert.Null(dialog.SavingText);
    }

    [Theory]
    [InlineData(0, false, "5")]
    [InlineData(-3, false, "5")]
    [InlineData(1, true, "1")]
    [InlineData(60, true, "60")]
    [InlineData(90, true, "90")]
    public async Task The_auto_fetch_box_shows_the_files_value(int minutes, bool on, string text)
    {
        var screen = new Screen(new AppSettings { AutoFetchMinutes = minutes });
        var dialog = await screen.OpenAsync();

        Assert.Equal((on, text), (dialog.General.IsAutoFetchOn, dialog.General.MinutesText));
        Assert.Null(dialog.General.AutoFetchError);
    }

    [Fact]
    public async Task A_files_value_above_60_is_kept_without_an_error_until_the_box_is_edited()
    {
        var screen = new Screen(new AppSettings { AutoFetchMinutes = 90 });
        var dialog = await screen.OpenAsync();

        // Unchecking and checking again is no edit of the box (7.3).
        dialog.General.IsAutoFetchOn = false;
        dialog.General.IsAutoFetchOn = true;
        dialog.General.StashIncludeUntracked = false;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(dialog.IsClosed);
        Assert.Equal(90, screen.Settings.Current.AutoFetchMinutes);
        Assert.Equal(1, screen.Settings.Updates);
    }

    [Theory]
    [InlineData("90")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("")]
    [InlineData("5.5")]
    public async Task A_typed_number_outside_1_to_60_is_an_error_as_soon_as_typed_and_save_refuses_it(string typed)
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync(SettingsSection.Git);

        dialog.General.MinutesText = typed;
        Assert.Equal("Enter a whole number of minutes from 1 to 60.", dialog.General.AutoFetchError);
        Assert.False(dialog.GeneralHasError);
        var focused = new List<string>();
        dialog.FocusRequested += (_, field) => focused.Add(field);

        await dialog.ConfirmCommand.ExecuteAsync(null);

        // The section is marked and shown, its first wrong field focused; nothing saved.
        Assert.False(dialog.IsClosed);
        Assert.True(dialog.GeneralHasError);
        Assert.Equal("Has an error", dialog.GeneralNavHelp);
        Assert.False(dialog.GitHasError);
        Assert.Equal(SettingsSection.General, dialog.SelectedSection);
        Assert.Equal(["AutoFetchMinutesBox"], focused);
        Assert.Equal(0, screen.Settings.Updates);
        Assert.Empty(screen.Git.GlobalWrites);

        // A correcting edit takes the dot away at once (SD9); unchecked saves 0.
        dialog.General.MinutesText = "15";
        Assert.False(dialog.GeneralHasError);
        dialog.General.IsAutoFetchOn = false;
        dialog.General.MinutesText = "abc";
        Assert.Null(dialog.General.AutoFetchError);
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.True(dialog.IsClosed);
        Assert.Equal(0, screen.Settings.Current.AutoFetchMinutes);
    }

    [Theory]
    [InlineData(".py, pl;.PY", ".py .pl")]
    [InlineData(".exe .Py", ".py")]
    [InlineData("  ", "")]
    [InlineData("rb;rb", ".rb")]
    public async Task The_extensions_are_stored_lower_case_with_their_dot_without_duplicates_or_the_fixed_lists(string typed, string stored)
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync();

        dialog.General.ExtraExtensionsText = typed;
        Assert.Null(dialog.General.RunnableError);
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(dialog.IsClosed);
        Assert.Equal(stored, screen.Settings.Current.ExtraRunnableExtensions);
        Assert.Equal(stored.Length == 0 ? 0 : 1, screen.Settings.Updates);
    }

    [Fact]
    public async Task An_entry_that_is_not_an_extension_names_the_first_bad_one()
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync();

        dialog.General.ExtraExtensionsText = "a b/c d*e";
        Assert.Equal("\"b/c\" is not an extension such as .py: use letters and digits.", dialog.General.RunnableError);
        Assert.Equal("RunnableExtraBox", dialog.General.FirstErrorField);
        Assert.Equal("\".abcdefghijklmnopq\" is not an extension such as .py: use letters and digits.", GeneralSettingsViewModel.ExtensionsError(".abcdefghijklmnopq"));
        Assert.Null(GeneralSettingsViewModel.ExtensionsError(".abcdefghijklmnop c++ ~x"));
    }

    [Fact]
    public async Task Save_makes_one_update_with_only_the_changed_settings_and_uses_the_theme_only_when_changed()
    {
        var screen = new Screen(new AppSettings { FileList = FileListMode.Tree, ShowAllFiles = true });
        var dialog = await screen.OpenAsync();

        dialog.General.KeepIgnoredFiles = false;
        dialog.General.MinutesText = "15";
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(dialog.IsClosed);
        Assert.True(await dialog.Result);
        Assert.Equal(1, screen.Settings.Updates);
        Assert.Equal(new AppSettings { FileList = FileListMode.Tree, ShowAllFiles = true, KeepIgnoredFiles = false, AutoFetchMinutes = 15 }, screen.Settings.Current);
        Assert.Empty(screen.Themes);

        // The theme alone: no update of the store from the screen, one UseTheme.
        var themes = new List<AppTheme>();
        var screen2 = new Screen { UseTheme = themes.Add };
        var themed = await screen2.OpenAsync();
        themed.General.IsSystem = true;
        await themed.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal([AppTheme.System], themes);
        Assert.Equal(0, screen2.Settings.Updates);

        // Nothing changed: no update, no UseTheme, and still the read back (SD29).
        var screen3 = new Screen();
        var unchanged = await screen3.OpenAsync();
        var reads = screen3.Git.Reads.Count;
        await unchanged.ConfirmCommand.ExecuteAsync(null);
        Assert.True(unchanged.IsClosed);
        Assert.Equal(0, screen3.Settings.Updates);
        Assert.Equal(reads * 2, screen3.Git.Reads.Count);
        Assert.Equal(0, screen3.TabsTold);
    }

    [Fact]
    public async Task Cancel_and_esc_change_nothing()
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync();
        dialog.General.KeepIgnoredFiles = false;
        dialog.General.IsLight = true;
        dialog.Git.Global.UserName = "Ann";
        dialog.Cancel();
        Assert.False(await dialog.Result);

        var esc = await screen.OpenAsync();
        esc.General.StashIncludeUntracked = false;
        esc.HandleEscape();
        Assert.True(esc.IsClosed);
        Assert.False(await esc.Result);

        Assert.Equal(0, screen.Settings.Updates);
        Assert.Empty(screen.Themes);
        Assert.Empty(screen.Git.GlobalWrites);
        Assert.Equal(0, screen.TabsTold);
    }

    [Fact]
    public async Task This_repository_is_disabled_with_its_reason_without_a_repository()
    {
        var dialog = await new Screen { Repository = null }.OpenAsync(SettingsSection.Git);

        Assert.False(dialog.Git.CanUseRepositoryScope);
        Assert.Equal("Open a repository's tab to change its own settings", dialog.Git.RepositoryScopeToolTip);
        Assert.False(dialog.Git.IsRepositoryScope);
    }

    [Fact]
    public async Task The_git_section_opens_on_the_global_scope_on_a_repositorys_tab_and_keeps_both_scopes_edits()
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync(SettingsSection.Git);

        Assert.False(dialog.Git.IsRepositoryScope);
        dialog.Git.ShowRepositoryCommand.Execute(null);
        dialog.Git.Shown.UserName = "Local Name";
        dialog.Git.ShowGlobalCommand.Execute(null);
        dialog.Git.Shown.UserName = "Global Name";
        dialog.Git.ShowRepositoryCommand.Execute(null);
        Assert.Equal("Local Name", dialog.Git.Shown.UserName);
        dialog.Git.ShowGlobalCommand.Execute(null);
        Assert.Equal("Global Name", dialog.Git.Shown.UserName);

        // Saved together: the global keys through the app's writer, then the repository's operation.
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.True(dialog.IsClosed);
        Assert.Equal([[new GitConfigWrite("user.name", "Global Name")]], screen.Git.GlobalWrites);
        Assert.Equal([[new GitConfigWrite("user.name", "Local Name")]], screen.Repository!.Saves);
        Assert.Equal(1, screen.TabsTold);
    }

    [Fact]
    public async Task Cancel_drops_a_waiting_repository_write_through_the_token_given_to_the_save()
    {
        var screen = new Screen();
        screen.Repository!.HoldSaves = true;
        screen.Repository.Running = new OperationSnapshot("Fetch", "Fetching from origin…", OperationKind.Network, IsBackground: true, ErrorDisplay.Dialog, Progress: null, CanCancel: true);
        var dialog = await screen.OpenAsync(SettingsSection.Git);
        dialog.Git.ShowRepositoryCommand.Execute(null);
        dialog.Git.Shown.UserEmail = "bo@example.org";

        var saving = dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("Waiting for Fetch to finish…", dialog.SavingText);
        Assert.False(dialog.IsWriting);
        Assert.True(dialog.CanCancelNow);
        var token = Assert.Single(screen.Repository.SaveTokens);
        Assert.False(token.IsCancellationRequested);

        dialog.Cancel();
        await saving;

        Assert.True(token.IsCancellationRequested);
        Assert.True(dialog.IsClosed);
        Assert.False(await dialog.Result);
        Assert.Equal(0, screen.TabsTold);
    }

    [Fact]
    public async Task While_the_repositorys_write_runs_saving_shows_and_both_buttons_are_disabled()
    {
        var screen = new Screen();
        screen.Repository!.HoldSaves = true;
        screen.Repository.Running = new OperationSnapshot("Save repository settings", "Saving the repository's settings…", OperationKind.Local, IsBackground: false, ErrorDisplay.Dialog, Progress: null, CanCancel: false);
        var dialog = await screen.OpenAsync(SettingsSection.Git);
        dialog.Git.ShowRepositoryCommand.Execute(null);
        dialog.Git.Shown.UserEmail = "bo@example.org";

        var saving = dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("Saving…", dialog.SavingText);
        Assert.True(dialog.IsWriting);
        Assert.False(dialog.CanCancelNow);
        dialog.HandleEscape();
        Assert.False(dialog.IsClosed);

        // The write ends: the read back, the tabs told, the close.
        screen.Repository.Running = null;
        screen.Repository.ReleaseWaitingSave();
        await saving;
        Assert.True(dialog.IsClosed);
        Assert.Null(dialog.SavingText);
        Assert.Equal(1, screen.TabsTold);
    }

    [Fact]
    public async Task A_failed_global_write_opens_its_error_dialog_without_a_repository_and_keeps_the_screen_on_git()
    {
        var screen = new Screen();
        screen.Git.WriteFailure = new GitException("git config --global --replace-all user.name Ann", 255, "error: could not lock config file C:/x/global.gitconfig: File exists\n");
        var dialog = await screen.OpenAsync();
        dialog.General.StashIncludeUntracked = false;
        dialog.Git.Global.UserName = "Ann";
        dialog.Git.Repository.UserName = "Bo";

        var saving = dialog.ConfirmCommand.ExecuteAsync(null);
        var error = await screen.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(
            ("Saving the settings failed", "Git could not save the settings for all repositories.", false, "git config --global --replace-all user.name Ann"),
            (error.Title, error.Summary, error.ShowsRepository, error.Command));
        Assert.Contains("could not lock config file", error.Output, StringComparison.Ordinal);
        error.ConfirmCommand.Execute(null);
        await saving;

        // Steps 2 and 3 stay applied; no repository write after the failure; the tabs are told.
        Assert.False(dialog.IsClosed);
        Assert.Equal(SettingsSection.Git, dialog.SelectedSection);
        Assert.False(screen.Settings.Current.StashIncludeUntracked);
        Assert.Empty(screen.Repository!.Saves);
        Assert.Equal(1, screen.TabsTold);
    }

    [Fact]
    public async Task The_editor_pick_lists_the_fixed_catalogue_and_a_known_editor_is_stored_without_arguments()
    {
        var screen = new Screen();
        var dialog = await screen.OpenAsync(SettingsSection.Tools);

        var choosing = dialog.Tools.ChooseCommand.ExecuteAsync(null);
        var pick = await screen.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(("Choose an editor", "Choose", "None"), (pick.Title, pick.ConfirmText, pick.Selected));
        Assert.Equal(
            [
                new PickOption("None", "no Open in editor in the file menus"),
                .. ToolCatalogues.FixedEditors.Select(editor => new PickOption(editor.Name, editor.Program)),
                new PickOption("Other…", "a program of your choice"),
            ],
            pick.Options);
        pick.Selected = "Visual Studio Code";
        pick.ConfirmCommand.Execute(null);
        await choosing;

        Assert.Equal((EditorChoice.Known, "Visual Studio Code", ToolCatalogues.FixedEditors[0].Program), (dialog.Tools.Choice, dialog.Tools.EditorValue, dialog.Tools.EditorProgram));
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(new EditorSetting("vscode", ToolCatalogues.FixedEditors[0].Program), screen.Settings.Current.Editor);
    }

    [Fact]
    public async Task Gits_editor_is_marked_in_the_pick_and_the_value()
    {
        var screen = new Screen();
        screen.Git.Outside = Read(false, Global("core.editor", $"\"{ToolCatalogues.FixedEditors[1].Program}\" -w"));
        screen.Git.InRepository = Read(true, Global("core.editor", $"\"{ToolCatalogues.FixedEditors[1].Program}\" -w"));
        screen.Settings.Update(settings => settings with { Editor = new EditorSetting("sublime", ToolCatalogues.FixedEditors[1].Program) });
        var dialog = await screen.OpenAsync(SettingsSection.Tools);

        Assert.Equal("Sublime Text (git's editor)", dialog.Tools.EditorValue);
        var choosing = dialog.Tools.ChooseCommand.ExecuteAsync(null);
        var pick = await screen.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal("Sublime Text (git's editor)", pick.Selected);
        Assert.Contains(new PickOption("Sublime Text (git's editor)", ToolCatalogues.FixedEditors[1].Program), pick.Options);
        pick.Cancel();
        await choosing;
    }

    [AvaloniaFact]
    public async Task Others_checks_run_as_typed_but_the_disks_only_at_save_and_off_the_ui_thread()
    {
        var screen = new Screen
        {
            // A share that does not answer must never be read on the UI thread (0.9).
            Disk = new EditorDiskChecks(
                path => Dispatcher.UIThread.CheckAccess() ? throw new InvalidOperationException("A file check on the UI thread.") : path == Program,
                path => Dispatcher.UIThread.CheckAccess() ? throw new InvalidOperationException("A file check on the UI thread.") : true),
        };
        var dialog = await screen.OpenAsync(SettingsSection.Tools);
        var focused = new List<string>();
        dialog.FocusRequested += (_, field) => focused.Add(field);

        var choosing = dialog.Tools.ChooseCommand.ExecuteAsync(null);
        var pick = await screen.WaitForDialogAsync<PickDialogViewModel>();
        pick.Selected = "Other…";
        pick.ConfirmCommand.Execute(null);
        await choosing;

        // Other… shows the rows with the focus in the program box and no error yet (D114).
        Assert.True(dialog.Tools.IsOther);
        Assert.Equal(("Other", "{path}", (string?)null), (dialog.Tools.EditorValue, dialog.Tools.ArgumentsText, dialog.Tools.EditorError));
        Assert.Equal(["EditorProgramBox"], focused);

        dialog.Tools.ProgramText = "x";
        dialog.Tools.ProgramText = string.Empty;
        Assert.Equal("Choose the editor's program.", dialog.Tools.EditorError);
        dialog.Tools.ProgramText = OperatingSystem.IsWindows() ? @"D:\Tools\code.cmd" : "/opt/tools/missing";
        Assert.Equal(OperatingSystem.IsWindows() ? "Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe." : null, dialog.Tools.EditorError);
        dialog.Tools.ProgramText = OperatingSystem.IsWindows() ? @"D:\Tools\Missing.exe" : "/opt/tools/missing";
        Assert.Null(dialog.Tools.EditorError);

        // At Save, off the UI thread: the program is not there.
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("There is no file at this path.", dialog.Tools.EditorError);
        Assert.True(dialog.ToolsHasError);
        Assert.Equal(SettingsSection.Tools, dialog.SelectedSection);
        Assert.Equal("EditorProgramBox", focused[^1]);
        Assert.Null(screen.Settings.Current.Editor);

        // A quote not closed; then a good program saves.
        dialog.Tools.ProgramText = Program;
        dialog.Tools.ArgumentsText = "\"{path}";
        Assert.Equal("A quote in the arguments is not closed.", dialog.Tools.EditorError);
        dialog.Tools.ArgumentsText = "-l {line} {path}";
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.True(dialog.IsClosed);
        Assert.Equal(new EditorSetting(KnownEditors.CustomId, Program, "-l {line} {path}"), screen.Settings.Current.Editor);
    }

    [Fact]
    public async Task Off_windows_a_file_that_is_not_marked_as_a_program_is_refused_at_save()
    {
        var screen = new Screen { Platform = EditorPlatform.Linux, Disk = new EditorDiskChecks(_ => true, _ => false) };
        var dialog = await screen.OpenAsync(SettingsSection.Tools);
        await screen.ChooseOtherAsync(dialog);
        dialog.Tools.ProgramText = "/opt/tools/ed.sh";

        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("This file is not marked as a program.", dialog.Tools.EditorError);
    }

    [Fact]
    public async Task Browse_fills_the_program_box_from_the_file_dialog()
    {
        var screen = new Screen();
        screen.Folders.AnswerFile(Program);
        var dialog = await screen.OpenAsync(SettingsSection.Tools);
        await screen.ChooseOtherAsync(dialog);

        await dialog.Tools.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(["Choose the editor's program"], screen.Folders.FileTitles);
        Assert.Equal(Program, dialog.Tools.ProgramText);

        // A cancelled dialog leaves the box as it is.
        await dialog.Tools.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(Program, dialog.Tools.ProgramText);
    }

    [Fact]
    public async Task A_stored_editor_whose_id_is_unknown_shows_none_and_is_kept_when_tools_is_untouched()
    {
        var screen = new Screen();
        screen.Settings.Update(settings => settings with { Editor = new EditorSetting("an-editor-of-the-future", Program) });
        var dialog = await screen.OpenAsync(SettingsSection.Tools);

        Assert.Equal(EditorChoice.None, dialog.Tools.Choice);
        dialog.General.StashIncludeUntracked = false;
        await dialog.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("an-editor-of-the-future", screen.Settings.Current.Editor?.Id);
    }

    /// <summary>The screen's services over fakes, and a way to open it and answer its dialogs.</summary>
    private sealed class Screen
    {
        public CountingSettings Settings { get; } = new();

        public FakeGitConfiguration Git { get; } = new() { Outside = Read(false), InRepository = Read(true) };

        public FakeSettingsRepository? Repository { get; set; } = new();

        public FakeFolderPicker Folders { get; } = new();

        public DialogHostViewModel Dialogs { get; } = new();

        public List<AppTheme> Themes { get; } = [];

        public Action<AppTheme>? UseTheme { get; set; }

        public int TabsTold { get; private set; }

        public EditorDiskChecks Disk { get; set; } = new(_ => true, _ => true);

        public EditorPlatform? Platform { get; set; }

        public Screen(AppSettings? settings = null)
        {
            if (settings is not null)
            {
                Settings.Load(settings);
            }
        }

        public async Task<SettingsDialogViewModel> OpenAsync(SettingsSection section = SettingsSection.General)
        {
            var dialog = new SettingsDialogViewModel(
                new SettingsScreenServices(
                    Settings,
                    Settings.Current.Theme,
                    UseTheme ?? Themes.Add,
                    Repository,
                    Git,
                    ToolCatalogues.Fixed,
                    SettingsDialogViewTests.ShortcutTable,
                    Folders,
                    Dialogs,
                    () => TabsTold++,
                    new RecordingLog())
                {
                    Disk = Disk,
                    Platform = Platform,
                    FileExists = _ => false,
                },
                section);
            _ = Dialogs.ShowAsync(dialog);
            await dialog.Loaded;
            return dialog;
        }

        public async Task<T> WaitForDialogAsync<T>()
            where T : DialogViewModel
        {
            var until = DateTime.UtcNow.AddSeconds(30);
            while (Dialogs.Current is not T)
            {
                Assert.True(DateTime.UtcNow < until, $"No {typeof(T).Name} opened.");
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }

            return (T)Dialogs.Current;
        }

        public async Task ChooseOtherAsync(SettingsDialogViewModel dialog)
        {
            var choosing = dialog.Tools.ChooseCommand.ExecuteAsync(null);
            var pick = await WaitForDialogAsync<PickDialogViewModel>();
            pick.Selected = "Other…";
            pick.ConfirmCommand.Execute(null);
            await choosing;
        }
    }

    /// <summary>A settings store that counts its updates.</summary>
    private sealed class CountingSettings : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new();

        public int Updates { get; private set; }

        public void Load(AppSettings settings) => Current = settings;

        /// <summary>Counts every call (7.8 step 2: none changed, no call), as the store saves only a change.</summary>
        public void Update(Func<AppSettings, AppSettings> change)
        {
            Updates++;
            Current = change(Current);
        }
    }
}
