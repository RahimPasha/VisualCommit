using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.Win32;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// Phase 6's additions to the real-window driver (docs/phase-6-design.md 17.2, package WP-R;
/// docs/test-reports/phase-6.md, "Conventions of every part", "The real window"): the files a
/// launch starts from (<c>workspaces.json</c>, the session's own global git file), the per-launch
/// git environment as <see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/> gives it,
/// the tab strip with its dashboard tabs, git found at the start, the UI Automation patterns phase
/// 6's controls are read through (a check box's or radio button's state, a progress bar's value,
/// an item's selection, its item status and key), and 11.8's read-only reads of the desktop: the
/// app mode and high contrast, which the pass never changes (D224). Every wait is on a condition
/// with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>Where Windows keeps the app mode: <c>AppsUseLightTheme</c> under this key of the current user (11.8).</summary>
    public const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // ---- The files a launch starts from (17.2) ----

    /// <summary>
    /// Writes the workspaces file of a data folder before the app starts on it, as the scripted
    /// walk-through writes it (<see cref="ShellDriver.WriteWorkspaces"/>, the store's own
    /// <c>JsonWorkspaceStore.Write</c>).
    /// </summary>
    public static void WriteWorkspaces(string dataDirectory, WorkspaceList workspaces)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(workspaces);
        ShellDriver.WriteWorkspaces(dataDirectory, workspaces);
    }

    /// <summary>
    /// Writes <paramref name="text"/> as the whole global git configuration of the launches on
    /// <paramref name="dataDirectory"/> before the app starts (<see cref="ShellDriver.SeedGlobalGitConfig"/>,
    /// the scripted run's helper): part S's seed, such as <c>user.useConfigOnly = true</c> (SD3).
    /// </summary>
    public static void SeedGlobalGitConfig(string dataDirectory, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(text);
        ShellDriver.SeedGlobalGitConfig(dataDirectory, text);
    }

    /// <summary>
    /// The global git configuration this launch's git calls use: <c>&lt;data&gt;/harness/global.gitconfig</c>
    /// (<c>GIT_CONFIG_GLOBAL</c> of <see cref="GitIsolation.SessionGitEnvironment"/>, which
    /// <see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/> sets), in this launch's own
    /// data folder (design 8.12, 8.13).
    /// </summary>
    public string GlobalGitConfigFile => GitIsolation.SessionGlobalConfigFile(DataDirectory);

    /// <summary>
    /// The git environment <see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/> gives a
    /// launch on <paramref name="dataDirectory"/> (17.2): <c>GIT_CONFIG_GLOBAL</c> and
    /// <c>XDG_CONFIG_HOME</c> in the data folder's <c>harness</c> folder, as a scripted session's.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> GitEnvironmentOf(string dataDirectory) => GitIsolation.SessionGitEnvironment(dataDirectory);

    // ---- 11.8's read-only reads of the desktop (D224) ----

    /// <summary>
    /// Whether Windows asks apps for the light mode (11.8): <c>AppsUseLightTheme</c> under
    /// <see cref="PersonalizeKey"/> is <c>1</c> or missing (Windows' default: light), <c>0</c> dark.
    /// Only read: the pass never writes the key or broadcasts a change (D224).
    /// </summary>
    public static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    /// <summary>
    /// Whether Windows' high contrast is on (11.8): <c>SystemParametersInfo(SPI_GETHIGHCONTRAST)</c>
    /// and its flag <c>HCF_HIGHCONTRASTON</c>. Only read; the pass never turns it off (D224).
    /// </summary>
    /// <exception cref="InvalidOperationException">Windows did not answer the read.</exception>
    public static bool HighContrastIsOn()
    {
        var settings = new NativeMethods.HighContrast { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.HighContrast>() };
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SpiGetHighContrast, settings.Size, ref settings, 0))
        {
            throw new InvalidOperationException($"SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
        }

        return (settings.Flags & NativeMethods.HcfHighContrastOn) != 0;
    }

    // ---- Git found at the start (phase 0's status bar) ----

    /// <summary>
    /// Waits until the status bar names the git the app found (<c>GitStatus</c> reads <c>Git
    /// 2.36.0</c> or the like): the app's start-up work has ended, as the scripted run's
    /// <c>WaitUntilReadyAsync</c> awaits it.
    /// </summary>
    public void WaitForGitFound(TimeSpan timeout) =>
        WaitForTelling(
            () => NamesOnScreen("GitStatus") is [var text] && text.StartsWith("Git ", StringComparison.Ordinal) && text.Length > 4 && char.IsDigit(text[4]),
            "the status bar to name the git found",
            timeout,
            () => $"It reads \"{string.Join(" | ", NamesOnScreen("GitStatus"))}\".");

    // ---- The tab strip with its dashboard tabs (13.4) ----

    /// <summary>The automation id of a dashboard's tab (13.4); every other tab is a <c>RepoTab</c>.</summary>
    public const string DashboardTabId = "DashboardTab";

    /// <summary>One tab of the strip as UI Automation shows it: its title (its name), whether it is a dashboard's, its item status, where it is.</summary>
    public sealed record StripTab(string Title, bool IsDashboard, string ItemStatus, RectangleF Bounds, AutomationElement Element);

    /// <summary>The tabs of the strip on screen, left to right: repository and empty tabs (<c>RepoTab</c>) and dashboards (<c>DashboardTab</c>).</summary>
    public IReadOnlyList<StripTab> StripTabs() =>
        [.. ShownElements("RepoTab").Select(tab => (Tab: tab, Dashboard: false))
            .Concat(ShownElements(DashboardTabId).Select(tab => (Tab: tab, Dashboard: true)))
            .Select(entry => new StripTab(entry.Tab.Name, entry.Dashboard, ItemStatusOf(entry.Tab), BoundsOf(entry.Tab), entry.Tab))
            .OrderBy(tab => tab.Bounds.Left)];

    /// <summary>Waits until the strip shows exactly the tabs <paramref name="titles"/>, left to right, of every kind.</summary>
    public void WaitForStripTabs(TimeSpan timeout, params string[] titles)
    {
        ArgumentNullException.ThrowIfNull(titles);
        WaitForTelling(
            () => StripTabs().Select(tab => tab.Title).SequenceEqual(titles),
            $"the tabs {string.Join(", ", titles)}",
            timeout,
            () => $"The strip shows {string.Join(", ", StripTabs().Select(tab => (tab.IsDashboard ? "dashboard " : string.Empty) + tab.Title))}.");
    }

    /// <summary>The strip's tab titled <paramref name="title"/>, of either kind; throws when there is not exactly one.</summary>
    public StripTab TabTitled(string title) =>
        StripTabs().Where(tab => tab.Title == title).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"{found.Count} tabs are titled \"{title}\"; the tabs: {string.Join(" | ", StripTabs().Select(tab => tab.Title))}."),
        };

    // ---- UI Automation's patterns, as phase 6's controls give them ----

    /// <summary>An element's item status (<c>AutomationProperties.ItemStatus</c>: a dashboard tab's <c>Workspace</c>, a palette row's detail), empty when it has none.</summary>
    public static string ItemStatusOf(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Properties.ItemStatus.ValueOrDefault ?? string.Empty;
    }

    /// <summary>An element's key (<c>AutomationProperties.AcceleratorKey</c>, UI Automation's property 30006), empty when it has none.</summary>
    public static string AcceleratorKeyOf(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Properties.AcceleratorKey.ValueOrDefault ?? string.Empty;
    }

    /// <summary>
    /// Whether a check box or radio button is on: its Toggle pattern's state, or, for a control that
    /// gives a selection instead, its SelectionItem pattern's. False when it gives neither.
    /// </summary>
    public static bool IsOn(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Patterns.Toggle.PatternOrDefault is { } toggle)
        {
            return toggle.ToggleState.ValueOrDefault == ToggleState.On;
        }

        return element.Patterns.SelectionItem.PatternOrDefault is { } selection && selection.IsSelected.ValueOrDefault;
    }

    /// <summary>Whether an item (a dashboard's row, a Pick dialog's option, a palette's row) is selected: its SelectionItem pattern.</summary>
    public static bool IsSelected(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Patterns.SelectionItem.PatternOrDefault is { } selection && selection.IsSelected.ValueOrDefault;
    }

    /// <summary>A progress bar's value (its RangeValue pattern), or null when it gives none.</summary>
    public static double? RangeValueOf(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Patterns.RangeValue.PatternOrDefault is { } range ? range.Value.ValueOrDefault : null;
    }

    /// <summary>
    /// The automation id of the element UI Automation reports as having the keyboard focus (its
    /// focused element, K10's <c>FilterBox</c> and <c>GraphRows</c>), or null when there is none or it
    /// cannot be read.
    /// </summary>
    public string? FocusedAutomationId()
    {
        try
        {
            return _automation.FocusedElement()?.Properties.AutomationId.ValueOrDefault;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    /// <summary>Waits until UI Automation's focused element has the automation id <paramref name="automationId"/>.</summary>
    public void WaitForFocusedElement(string automationId, TimeSpan timeout) =>
        WaitForTelling(
            () => FocusedAutomationId() == automationId,
            $"UI Automation's focused element to be {automationId}",
            timeout,
            () => $"It is {FocusedAutomationId() ?? "none"}.");

    /// <summary>
    /// The text of the key a keyed menu item draws at its right (<c>ContextMenuItemKey</c>, 6.7): the
    /// key element inside <paramref name="item"/>, or else the one on screen beside it (its vertical
    /// centre within the item's bounds); null when the item draws no key.
    /// </summary>
    public string? MenuItemKeyText(AutomationElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.FindFirstDescendant(condition => condition.ByAutomationId("ContextMenuItemKey")) is { } inside)
        {
            return inside.Name;
        }

        var bounds = BoundsOf(item);
        return ShownElements("ContextMenuItemKey")
            .Select(key => (Key: key, Bounds: BoundsOf(key)))
            .Where(key => key.Bounds.Top + (key.Bounds.Height / 2) > bounds.Top && key.Bounds.Top + (key.Bounds.Height / 2) < bounds.Bottom)
            .Select(key => key.Key.Name)
            .FirstOrDefault();
    }

    /// <summary>A text box's text, read through its Value pattern.</summary>
    public string BoxText(string automationId) => Find(automationId).AsTextBox().Text;

    /// <summary>
    /// The names of the text elements inside <paramref name="element"/>, in the order UI Automation
    /// lists them: the parts of a row whose own name is only one of them (a Pick dialog's option
    /// and its detail).
    /// </summary>
    public static IReadOnlyList<string> TextsIn(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return [.. element.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Select(text => text.Name)
            .Where(name => !string.IsNullOrEmpty(name))];
    }

    /// <summary>
    /// The element <paramref name="automationId"/> on screen inside <paramref name="scope"/> (a dialog, a
    /// row): for ids two layers share, such as the settings screen's and its stacked Pick dialog's
    /// <c>ConfirmButton</c>. Throws when there is not exactly one.
    /// </summary>
    public AutomationElement FindIn(AutomationElement scope, string automationId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        return scope.FindAllDescendants(condition => condition.ByAutomationId(automationId))
            .Where(element => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 })
            .ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"{found.Count} elements with the automation id '{automationId}' are on screen in '{scope.AutomationId}'."),
        };
    }
}
