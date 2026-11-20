using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.GitSetup;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The first-run screen's helpers (docs/phase-8a-design.md 5.5; the report's part F): where the
/// screen and its parts are, which of its buttons and links show and in what order, and the wait
/// for the screen with its guidance applied, which never waits on the screen's visibility alone.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The window's first-run screen (one per window).</summary>
    public GitSetupViewModel GitSetupScreen => Session.Shell.GitSetup;

    /// <summary>The screen's view in the active tab's graph area (<c>GitSetup</c>), shown or not.</summary>
    public Control GitSetupView() =>
        Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == "GitSetup")
        ?? throw new InvalidOperationException("The first-run screen is not in the window.");

    /// <summary>Whether the screen shows: shown in the tree, with a laid-out size.</summary>
    public bool ShowsGitSetupView() =>
        Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == "GitSetup") is { IsEffectivelyVisible: true, Bounds.Height: > 0 };

    /// <summary>A part of the screen by its automation id: shown with a laid-out size, else null.</summary>
    public Control? GitSetupPart(string automationId) =>
        GitSetupView().GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId && control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0);

    /// <summary>The text of a part of the screen (a text block's, or a button's name); null when it is not shown.</summary>
    public string? GitSetupText(string automationId) => GitSetupPart(automationId) switch
    {
        TextBlock text => text.Text,
        Button button => AutomationProperties.GetName(button),
        { } other => AutomationProperties.GetName(other),
        null => null,
    };

    /// <summary>The screen's shown buttons, as their automation ids, in their order on the screen: by line, then left to right.</summary>
    public IReadOnlyList<string> GitSetupButtonIds() => ShownIn("GitSetupButtons");

    /// <summary>The screen's shown links, as their automation ids, left to right.</summary>
    public IReadOnlyList<string> GitSetupLinkIds() => ShownIn("GitSetupLinks");

    /// <summary>
    /// Waits until the screen shows with its guidance applied (the report's "Waiting for the
    /// screen"): <c>GitSetupGuidance</c> reads <paramref name="guidance"/> (or starts with
    /// <paramref name="guidancePrefix"/>), <c>GitSetupChooseButton</c> reads <paramref name="choose"/>
    /// where it is given, and <c>GitSetupWingetButton</c> and <c>GitSetupCommand</c> show where
    /// <paramref name="winget"/> and <paramref name="command"/> say. A wait that times out names
    /// what did not come.
    /// </summary>
    public async Task WaitForGitSetupAsync(
        string? guidance = null,
        string? guidancePrefix = null,
        string? choose = null,
        bool winget = false,
        bool command = false,
        TimeSpan? timeout = null)
    {
        var missing = "the first-run screen";
        bool Ready()
        {
            var shown = ShowsGitSetupView();
            var text = shown ? GitSetupText("GitSetupGuidance") : null;
            var parts = new List<string>();
            if (!shown)
            {
                parts.Add("GitSetup shown");
            }

            if (guidance is not null && text != guidance)
            {
                parts.Add($"GitSetupGuidance \"{guidance}\" (it reads \"{text}\")");
            }

            if (guidancePrefix is not null && text?.StartsWith(guidancePrefix, StringComparison.Ordinal) != true)
            {
                parts.Add($"GitSetupGuidance starting \"{guidancePrefix}\" (it reads \"{text}\")");
            }

            if (shown && choose is not null && GitSetupText("GitSetupChooseButton") != choose)
            {
                parts.Add($"GitSetupChooseButton \"{choose}\"");
            }

            if (shown && winget && GitSetupPart("GitSetupWingetButton") is null)
            {
                parts.Add("GitSetupWingetButton shown");
            }

            if (shown && command && GitSetupPart("GitSetupCommand") is null)
            {
                parts.Add("GitSetupCommand shown");
            }

            missing = "the first-run screen with " + string.Join(", ", parts);
            return parts.Count == 0;
        }

        try
        {
            await WaitForAsync(Ready, "the first-run screen with its guidance", timeout).ConfigureAwait(true);
        }
        catch (TimeoutException timedOut)
        {
            throw new TimeoutException($"Timed out waiting for {missing}.", timedOut);
        }

        Settle();
    }

    private List<string> ShownIn(string panelName)
    {
        var panel = GitSetupView().GetVisualDescendants().OfType<Panel>().FirstOrDefault(candidate => candidate.Name == panelName)
            ?? throw new InvalidOperationException($"The first-run screen has no {panelName}.");
        return
        [
            .. panel.Children.OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && button.Bounds.Width > 0)
                .OrderBy(button => Math.Round(BoundsOf(button).Top))
                .ThenBy(button => BoundsOf(button).Left)
                .Select(button => AutomationProperties.GetAutomationId(button) ?? string.Empty),
        ];
    }
}
