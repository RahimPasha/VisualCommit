using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The settings screen's About section in the headless app (D409, D431, D432), without pictures: its
/// texts are the pinned identity's; Show license… by Enter puts the focus in the license's text and
/// Esc gives it back, as Enter in the text does, which is the dialog's Close; the notices' switch
/// shows Avalonia's file; the links reach the fake launcher and nothing else is launched.
/// </summary>
public sealed class AboutSectionTests
{
    private static async Task<ShellDriver> StartOnAboutAsync(TempDirectory data)
    {
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            app.Session.Shell.OpenSettingsCommand.Execute(SettingsSection.About);
            await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true }, "the settings screen on About");
            await app.WaitForSettingsReadsAsync();
            await app.WaitForAsync(() => app.FocusedAutomationId() == A.NavAboutId, "the focus on About's button");
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static Control InView(ShellDriver app, string id) => app.FindByAutomationId(app.TopDialogView!, id);

    [AvaloniaFact]
    public async Task The_sections_texts_are_the_pinned_identitys()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data);

        Assert.Equal(TestIdentity.Pinned.Name, ((TextBlock)InView(app, A.NameId)).Text);
        Assert.Equal("Created by " + TestIdentity.Pinned.Creator, ((TextBlock)InView(app, A.CreatorId)).Text);
        Assert.Equal(TestIdentity.Pinned.Version.Display, ((TextBlock)InView(app, A.VersionId)).Text);
        Assert.Equal(PinnedVersion, AutomationProperties.GetName(InView(app, A.VersionId)));
        Assert.Equal(TestIdentity.Pinned.Copyright, ((TextBlock)InView(app, A.CopyrightId)).Text);
        Assert.Equal(A.MitLicense, ((TextBlock)InView(app, A.LicenseId)).Text);
        Assert.Equal(A.NoticesNote, ((TextBlock)InView(app, A.NoticesNoteId)).Text);
        Assert.Equal(A.RepositoryLink, AutomationProperties.GetName(InView(app, A.RepositoryLinkId)));
        Assert.Equal(A.OpenUserGuide, AutomationProperties.GetName(InView(app, A.GuideLinkId)));

        // Tab walks the section's four buttons and links, then Cancel and Save.
        foreach (var id in A.TabOrder.Concat(["CancelButton", "ConfirmButton"]))
        {
            app.PressKey(Key.Tab);
            Assert.Equal(id, app.FocusedAutomationId());
        }
    }

    [AvaloniaFact]
    public async Task Show_license_by_Enter_puts_the_focus_in_its_text_and_Esc_gives_it_back()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data);
        app.PressKey(Key.Tab);
        Assert.Equal(A.ShowLicenseId, app.FocusedAutomationId());

        app.PressKey(Key.Enter);
        var dialog = await app.WaitForDialogAsync<TextDocumentDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedAutomationId() == A.TextId, "the focus in the license's text");
        var text = (DocumentTextView)app.FindByAutomationId(app.TopDialogView!, A.TextId);
        Assert.True(text.TextArea.IsFocused);
        Assert.False(app.FocusVisible, "The text took the focus with the keyboard's frame.");
        Assert.Equal(A.LicenseTitle, dialog.Title);
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.License), text.Document.Text);

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => dialog.IsClosed && app.FocusedAutomationId() == A.ShowLicenseId, "the focus back on Show license…");
        Assert.False(await dialog.Result);
        Assert.True(app.FocusVisible, "Show license… has the focus back without the keyboard's frame.");
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "Esc closed more than the text dialog.");
    }

    [AvaloniaFact]
    public async Task Enter_in_the_licenses_text_closes_the_dialog_as_Close_does_and_gives_the_focus_back()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data);
        app.PressKey(Key.Tab);
        Assert.Equal(A.ShowLicenseId, app.FocusedAutomationId());

        app.PressKey(Key.Enter);
        var dialog = await app.WaitForDialogAsync<TextDocumentDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedAutomationId() == A.TextId, "the focus in the license's text");
        var text = (DocumentTextView)app.FindByAutomationId(app.TopDialogView!, A.TextId);
        Assert.True(text.TextArea.IsFocused);

        // Enter with a modifier closes nothing: the dialog's Enter wants none, and its Mod+Enter is
        // only a box's of several lines.
        foreach (var modifiers in new[] { RawInputModifiers.Shift, RawInputModifiers.Control, RawInputModifiers.Alt, RawInputModifiers.Meta, RawInputModifiers.Control | RawInputModifiers.Shift })
        {
            app.PressKey(Key.Enter, modifiers);
            Assert.False(dialog.IsClosed, $"Enter with {modifiers} closed the dialog.");
            Assert.True(text.TextArea.IsFocused, $"Enter with {modifiers} moved the focus out of the text.");
        }

        // A plain Enter that the text does not take is the dialog's: its Close.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => dialog.IsClosed && app.FocusedAutomationId() == A.ShowLicenseId, "the dialog closed by Enter, the focus back on Show license…");
        Assert.True(await dialog.Result, "Enter in the text did not answer as Close does.");
        Assert.True(app.FocusVisible, "Show license… has the focus back without the keyboard's frame.");
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "Enter closed more than the text dialog.");
    }

    [AvaloniaFact]
    public async Task The_notices_switch_shows_Avalonias_file_from_its_top()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data);
        app.Click(InView(app, A.ShowNoticesId));
        var dialog = await app.WaitForDialogAsync<TextDocumentDialogViewModel>();
        await app.WaitForAsync(() => app.TopDialogView is TextDocumentDialogView, "the notices' dialog");
        var view = app.TopDialogView!;
        var text = (DocumentTextView)app.FindByAutomationId(view, A.TextId);
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices), text.Document.Text);

        // End first takes the main file to its end, so that "from its top" is the switch's doing.
        await app.WaitForAsync(() => text.TextArea.IsFocused, "the focus in the notices' text");
        app.PressKey(Key.End);
        await app.WaitForAsync(
            () => text.ScrollExtent.Height > text.ScrollViewport.Height
                && Math.Abs(text.ScrollOffset.Y - (text.ScrollExtent.Height - text.ScrollViewport.Height)) <= 1,
            "the notices scrolled to their end");

        app.Click(app.FindByAutomationId(view, "TextDocumentPartAvalonia"));
        await app.WaitForAsync(() => text.TextArea.TextView.VisualLinesValid, "Avalonia's file laid out");

        Assert.Equal("Avalonia", dialog.Shown.Label);
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.VendorNoticePrefix + "Avalonia-NOTICE.md"), text.Document.Text);
        Assert.Equal(0, text.ScrollOffset.Y);
        Assert.Contains("selected", app.FindByAutomationId(view, "TextDocumentPartAvalonia").Classes);
        Assert.DoesNotContain("selected", app.FindByAutomationId(view, "TextDocumentPartMain").Classes);
        Assert.Equal(A.NoticesSwitchIds, view.GetVisualDescendants().OfType<Button>().Select(AutomationProperties.GetAutomationId).Where(id => id is not null && id.StartsWith("TextDocumentPart", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public async Task The_links_reach_the_fake_launcher_and_nothing_else_is_launched()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data);

        app.Click(InView(app, A.RepositoryLinkId));
        app.Click(InView(app, A.GuideLinkId));
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "two requests");

        Assert.Equal(
            [new FileLaunch(FileLaunchKind.OpenUrl, A.RepositoryAddress), new FileLaunch(FileLaunchKind.OpenUrl, A.GuideAddress)],
            app.Launcher.Requests);
        Assert.Null(app.Clipboard.Text);
        Assert.True(app.SettingsDialog is { ShowsAbout: true }, "A link closed the settings screen.");
    }
}
