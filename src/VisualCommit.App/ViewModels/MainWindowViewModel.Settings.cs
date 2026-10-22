using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels;

// The settings screen (docs/phase-6-design.md 7): OpenSettingsCommand, which the toolbar's
// Settings button, Mod+, the palette and the editor failure's "Open settings…" run, and
// TabServices.OpenSettings. Work package WP-SET builds it.
public partial class MainWindowViewModel
{
    /// <summary>The machine's tool catalogues, made at the first opening when the session was given none (12.9).</summary>
    private ToolCatalogues? _machineTools;

    /// <summary>
    /// The toolbar's <c>SettingsButton</c>'s tooltip and help text (7.2, 6.8): <c>Open settings
    /// ({key})</c>, the key in the platform's words. Never disabled.
    /// </summary>
    public string SettingsToolTip => SettingsToolTipFor(KeyTexts.Current);

    /// <summary><see cref="SettingsToolTip"/> in <paramref name="keys"/>' words (Cmd on macOS).</summary>
    public static string SettingsToolTipFor(KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.WithKey(Texts.OpenSettings, AppCommand.Settings);
    }

    /// <summary>
    /// Opens the settings screen on <paramref name="section"/> (7.2): the toolbar's button passes
    /// none (General); Mod+, the palette and the editor failure's "Open settings…" pass theirs. The
    /// screen gets the active tab's repository (none on an empty or a dashboard tab, nor while its
    /// refs are unread), the settings store, the shell's <c>UseTheme</c>, git's configuration, the
    /// tool catalogues and the key list, and shows on the dialog layer. Not awaited: the button
    /// stays enabled while the screen is open.
    /// </summary>
    [RelayCommand]
    private void OpenSettings(SettingsSection? section) => _ = ShowSettingsAsync(section ?? SettingsSection.General);

    /// <summary>Shows the settings screen on <paramref name="section"/> and completes when it has closed (7.2, 12.7).</summary>
    public async Task ShowSettingsAsync(SettingsSection section)
    {
        try
        {
            var repository = ActiveTab is { IsDashboard: false, Repository: { Refs: not null, IsSummary: false } open } ? new RepositorySettingsTarget(open) : null;
            var tools = _tabServices.Tools ?? (_machineTools ??= EditorCatalog.MachineCatalogues(_tabServices.GitSettings, _settings, _log));
            var dialog = new SettingsDialogViewModel(
                new SettingsScreenServices(
                    _settings,
                    Theme,
                    UseTheme,
                    repository,
                    _tabServices.GitSettings,
                    tools,
                    AppShortcuts.Groups(KeyTexts.Current.Platform),
                    _tabServices.Folders,
                    Dialogs,
                    _tabServices.RaiseGitSettingsChanged,
                    _log),
                section);
            await Dialogs.ShowAsync(dialog);
        }
        catch (Exception ex)
        {
            _log.Error("The settings screen failed.", ex);
        }
    }

    /// <summary>Called once from the constructor: the editor failure's "Open settings…" opens the screen through the window (12.7).</summary>
    private void WireSettings() => _tabServices.OpenSettings = ShowSettingsAsync;
}
