using System.Globalization;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part F's helpers (docs/test-reports/phase-8a.md, "Conventions of part F").</summary>
public sealed partial class Phase8aChecksF
{
    /// <summary>
    /// Starts the app on <paramref name="data"/> with <paramref name="tabs"/> restored (none: one
    /// empty tab, no session written), git looked for by <paramref name="probe"/> (the machine's
    /// search when null), the theme written before the start as the support's starts write it, and
    /// waits for the start-up work to end. The pointer rests at the neutral point.
    /// </summary>
    private static async Task<ShellDriver> StartFirstRunAsync(
        TempDirectory data,
        IGitSetupProbe? probe,
        IReadOnlyList<string?>? tabs = null,
        AppTheme theme = AppTheme.Dark,
        ToolCatalogues? tools = null,
        string? askPassHelper = null,
        HostingOptions? hosting = null)
    {
        if (theme != AppTheme.Dark)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = theme });
        }

        var app = tabs is null
            ? ShellDriver.Start(data.Path, tools: tools, hosting: hosting, gitProbe: probe, askPassHelper: askPassHelper)
            : ShellDriver.StartWithTabs(data.Path, tabs, tools: tools, hosting: hosting, gitProbe: probe, askPassHelper: askPassHelper);
        try
        {
            await app.WaitUntilReadyAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>A picture of part F: <see cref="Phase8aChecks.Shot"/>, then no text cut off, no tooltip open and the set-up button not hovered.</summary>
    private static Screenshot Picture(ShellDriver app, string name)
    {
        var shot = Shot(app, name);
        AssertNoClippedText(app);
        Assert.False(ToolTip.GetIsOpen(app.FindByAutomationId(F.StatusId)), "GitStatus's tooltip is open in the picture.");
        Assert.False(app.FindByAutomationId(F.SetUpButtonId).IsPointerOver, "GitSetupButton is hovered in the picture.");
        return shot;
    }

    /// <summary>The screen's title, body, path, git's words, guidance and command as the step gives them; a null part is not shown.</summary>
    private static void AssertScreen(ShellDriver app, string title, string body, string? path, string? gitSaid, string guidance, string? command)
    {
        Assert.True(app.ShowsGitSetupView(), "The first-run screen is not shown.");
        Assert.Equal(title, app.GitSetupText(F.TitleId));
        Assert.Equal(body, app.GitSetupText(F.BodyId));
        Assert.Equal(path, app.GitSetupText(F.PathId));
        Assert.Equal(gitSaid, app.GitSetupText(F.GitSaidId));
        Assert.Equal(guidance, app.GitSetupText(F.GuidanceId));
        Assert.Equal(command, app.GitSetupText(F.CommandId));
        Assert.Equal(command is not null, app.GitSetupPart(F.CopyId) is not null);
        if (command is not null)
        {
            Assert.Equal(F.Copy, app.GitSetupText(F.CopyId));
        }

        Assert.Equal(gitSaid is not null, app.GitSetupView().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "GitSetupGitSaidLabel" && text.IsEffectivelyVisible && text.Text == F.GitSaid));

        // The screen's name and help text are the state's title and body; the screen takes no focus.
        var view = app.GitSetupView();
        Assert.Equal(title, AutomationProperties.GetName(view));
        Assert.Equal(body, AutomationProperties.GetHelpText(view));
        Assert.False(view.Focusable);

        // Nothing of the screen is pushed out of its width, where the graph area would cut it off.
        var screen = app.BoundsOf(view);
        foreach (var part in view.GetVisualDescendants().OfType<StackPanel>().First().GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0))
        {
            var bounds = app.BoundsOf(part);
            Assert.True(bounds.Left >= screen.Left - 0.5 && bounds.Right <= screen.Right + 0.5, $"{part.Name} at {bounds} lies outside the screen at {screen}.");
        }

        // The welcome page is not shown in its place.
        Assert.False(app.Session.Shell.ActiveTab.ShowsWelcome);
        Assert.DoesNotContain(TextsIn(app.Window), text => text == F.NoRepositoryOpen);
    }

    /// <summary>The screen's buttons and links shown, in their order, each enabled and named by its text.</summary>
    private static void AssertOffers(ShellDriver app, (string Id, string Text)[] buttons, (string Id, string Text)[] links)
    {
        Assert.Equal(buttons.Select(button => button.Id), app.GitSetupButtonIds());
        Assert.Equal(links.Select(link => link.Id), app.GitSetupLinkIds());
        foreach (var (id, text) in buttons.Concat(links))
        {
            var control = app.GitSetupPart(id)!;
            Assert.True(control.IsEffectivelyEnabled, $"{id} is disabled.");
            Assert.Equal(text, AutomationProperties.GetName(control));
        }

        // Every id the order lists that is not offered is not shown.
        foreach (var id in F.ButtonOrder.Concat(F.LinkOrder).Except(buttons.Concat(links).Select(offer => offer.Id)))
        {
            Assert.Null(app.GitSetupPart(id));
        }

        Assert.Null(app.GitSetupPart(F.ErrorId));
    }

    /// <summary>The status bar's git box: its text, its tooltip and help text (equal; null for none), and the set-up button's text (null: not shown).</summary>
    private static void AssertGitStatus(ShellDriver app, string text, string? tip, string? setUp)
    {
        var status = (TextBlock)app.FindByAutomationId(F.StatusId);
        Assert.Equal(text, status.Text);
        Assert.Equal(tip, ToolTip.GetTip(status) as string);
        Assert.Equal(tip, AutomationProperties.GetHelpText(status));
        var button = (Button)app.FindByAutomationId(F.SetUpButtonId);
        Assert.Equal(setUp is not null, button.IsEffectivelyVisible);
        if (setUp is not null)
        {
            Assert.Equal(setUp, AutomationProperties.GetName(button));
            Assert.Equal(setUp, app.Session.Shell.GitSetup.StatusButtonText);
        }
    }

    /// <summary>The screen's look by token (5.3): the title at 16 SemiBold, the body secondary, the links in the accent, at most 520 wide.</summary>
    private static void AssertLook(ShellDriver app, bool dark)
    {
        var view = app.GitSetupView();
        var title = (TextBlock)app.GitSetupPart(F.TitleId)!;
        Assert.Equal((F.TitleSize, FontWeight.SemiBold), (title.FontSize, title.FontWeight));
        Assert.Equal(dark ? F.SecondaryDark : F.SecondaryLight, ((ISolidColorBrush)((TextBlock)app.GitSetupPart(F.BodyId)!).Foreground!).Color);
        Assert.Contains("secondary", app.GitSetupPart(F.BodyId)!.Classes);
        foreach (var link in app.GitSetupLinkIds())
        {
            var text = app.GitSetupPart(link)!.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible);
            Assert.Equal(dark ? F.AccentDark : F.AccentLight, ((ISolidColorBrush)text.Foreground!).Color);
        }

        var stack = view.GetVisualDescendants().OfType<StackPanel>().First();
        Assert.Equal(F.MaxWidth, stack.MaxWidth);
        Assert.True(stack.Bounds.Width <= F.MaxWidth + 0.5, $"The screen is {stack.Bounds.Width} wide.");
        Assert.Equal(VerticalAlignment.Center, stack.VerticalAlignment);
    }

    /// <summary>The confirm over the backdrop: its title, git-scm.com's question, the detail, Cancel focused, the primary confirm button.</summary>
    private static void AssertConfirm(ShellDriver app, string title, string detail, string confirm)
    {
        var view = app.TopDialogView!;
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(F.ConfirmQuestion, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        Assert.Equal(detail, ((TextBlock)app.FindByAutomationId(view, "ConfirmDetail")).Text);
        Assert.False(app.FindByAutomationId(view, "DialogRepository").IsEffectivelyVisible);
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var ok = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(F.Cancel, AutomationProperties.GetName(cancel) ?? cancel.Content as string);
        Assert.Equal(confirm, AutomationProperties.GetName(ok) ?? ok.Content as string);
        Assert.DoesNotContain("danger", ok.Classes);
        Assert.Equal("CancelButton", app.FocusedAutomationId());
        Assert.False(app.Session.Services.DialogHost.Current is ConfirmationViewModel { Request.IsDestructive: true });
    }

    /// <summary>Clicks the confirm's <paramref name="button"/> and waits until no dialog shows.</summary>
    private static async Task CloseConfirmAsync(ShellDriver app, string button)
    {
        app.Click(app.FindByAutomationId(app.TopDialogView!, button));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the confirm to close");
        app.Settle();
    }

    /// <summary>The settings file's <c>gitExecutable</c>, or null when it has none or there is no file.</summary>
    private static string? SavedGitExecutable(ShellDriver app)
    {
        var file = app.Session.Paths.SettingsFile;
        if (!File.Exists(file))
        {
            return null;
        }

        using var json = JsonDocument.Parse(File.ReadAllText(file));
        return json.RootElement.TryGetProperty("gitExecutable", out var value) ? value.GetString() : null;
    }

    /// <summary>A settings file's properties but <paramref name="left"/>, as name and raw value, in order.</summary>
    private static List<string> PropertiesBut(string json, string left)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Where(property => property.Name != left).Select(property => property.Name + "=" + property.Value.GetRawText())];
    }

    /// <summary>The settings file's text, or null when there is none.</summary>
    private static string? SettingsText(ShellDriver app) =>
        File.Exists(app.Session.Paths.SettingsFile) ? File.ReadAllText(app.Session.Paths.SettingsFile) : null;

    /// <summary>The machine git's version, as the status bar of every earlier picture shows it.</summary>
    private static string MachineVersion(ShellDriver app) =>
        app.Session.Shell.Git?.Version?.ToString() ?? throw new InvalidOperationException("No git version is known.");

    /// <summary>A text of the report with its values put in.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);

    /// <summary>The tooltip a path found on the PATH has.</summary>
    private static string OnPath(string path) => Format(F.FoundOnPathTipFormat, path);

    /// <summary>The focused control has the keyboard outline (<c>:focus-visible</c>).</summary>
    private static bool HasFocusVisible(ShellDriver app) =>
        app.FocusedElement is Control focused && focused.Classes.Contains(":focus-visible");

    /// <summary>The probe's lookups' chosen paths, in order, as one text (<see cref="Paths"/>).</summary>
    private static string ChosenPaths(FakeGitSetupProbe probe) => Paths([.. probe.Lookups.Select(lookup => lookup.ChosenPath)]);

    /// <summary>Chosen paths as one text, <c>(none)</c> for a lookup without one.</summary>
    private static string Paths(params string?[] paths) => string.Join(" | ", paths.Select(path => path ?? "(none)"));

    /// <summary>The log has no line with <paramref name="text"/> since the start.</summary>
    private static void AssertNotLogged(ShellDriver app, string text) =>
        Assert.DoesNotContain(app.LogLines(), line => line.Contains(text, StringComparison.Ordinal));

    /// <summary>How many log lines end with <paramref name="message"/>.</summary>
    private static int Logged(ShellDriver app, string message) =>
        app.LogLines().Count(line => line.EndsWith(": " + message, StringComparison.Ordinal) || line.EndsWith(" " + message, StringComparison.Ordinal));
}
