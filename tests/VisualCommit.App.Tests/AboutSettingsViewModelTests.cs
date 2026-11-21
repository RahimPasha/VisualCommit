using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AboutTexts;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen's About section as a view model (D409, D431, D432): the pinned identity's
/// texts, the license and the notices in the text dialog (read from the app's own assembly), the
/// links handed to the launcher and nothing else, disabled without one, and the failures logged:
/// a link the launcher cannot open as a Warning, a text that cannot be read as an Error. No window.
/// </summary>
public sealed class AboutSettingsViewModelTests
{
    [Fact]
    public void The_section_shows_the_pinned_identity_and_the_repositorys_host_and_path()
    {
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, new DialogHostViewModel(), new FakeFileLauncher(), new FakeClipboard(), new RecordingLog());

        Assert.Equal("VisualCommit", about.Name);
        Assert.Equal("Created by Rahim Khajei", about.CreatedBy);
        Assert.Equal("0.1.0 (1a2b3c4)", about.Version);
        Assert.Equal("Copyright (c) 2026 Rahim Khajei", about.Copyright);
        Assert.Equal("github.com/RahimPasha/VisualCommit", about.RepositoryLinkText);
    }

    [Fact]
    public void The_texts_are_the_designs_word_for_word_and_name_no_address()
    {
        Assert.Equal(
            [
                "About", "Created by Rahim Khajei", "Version", "Copyright", "License", "Notices", "Source code", "User guide", "MIT License",
                "Show license…", "VisualCommit is built on open-source components, each under its own license.", "Show third-party notices…",
                "Opens the repository's page in your browser.", "Open the user guide", "Opens the user guide in your browser.", "MIT License",
                "Third-party notices", "Components and licenses", "Copy all", "Close", "About VisualCommit…", "About VisualCommit",
            ],
            [
                Texts.Section, Texts.CreatedBy("Rahim Khajei"), Texts.VersionLabel, Texts.CopyrightLabel, Texts.LicenseLabel, Texts.NoticesLabel,
                Texts.SourceCodeLabel, Texts.UserGuideLabel, Texts.MitLicense, Texts.ShowLicense, Texts.NoticesNote, Texts.ShowNotices,
                Texts.RepositoryTip, Texts.OpenUserGuide, Texts.GuideTip, Texts.LicenseTitle, Texts.NoticesTitle, Texts.MainNotices,
                Texts.CopyAll, Texts.Close, Texts.PaletteCommand, Texts.MenuItem,
            ]);
        Assert.Equal(
            ["Avalonia", ".NET Community Toolkit", "Onigwrap", "SkiaSharp and HarfBuzzSharp", "Other-NOTICE.txt"],
            new[] { "Avalonia-NOTICE.md", "CommunityToolkit.Mvvm-ThirdPartyNotices.txt", "Onigwrap-THIRD-PARTY-NOTICES.txt", "SkiaSharp-THIRD-PARTY-NOTICES.txt", "Other-NOTICE.txt" }
                .Select(Texts.NoticeFileLabel));
        Assert.Equal("The text VisualCommit.License.txt could not be read.", Texts.TextNotRead(EmbeddedTexts.License));
        Assert.Equal(
            ["Opening the repository's page failed.", "Opening the user guide failed.", "Copying the text to the clipboard failed."],
            [Texts.RepositoryFailed, Texts.GuideFailed, Texts.CopyFailed]);
    }

    [Fact]
    public async Task Show_license_shows_one_dialog_with_the_license_alone()
    {
        var dialogs = new DialogHostViewModel();
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, dialogs, new FakeFileLauncher(), new FakeClipboard(), new RecordingLog());

        await about.ShowLicenseCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<TextDocumentDialogViewModel>(Assert.Single(dialogs.Open));
        Assert.Equal("MIT License", dialog.Title);
        var part = Assert.Single(dialog.Parts);
        Assert.Equal(("MIT License", "TextDocumentPartMain"), (part.Label, part.AutomationId));
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.License), part.Text);
        Assert.StartsWith("MIT License\n", part.Text, StringComparison.Ordinal);
        Assert.False(dialog.ShowsSwitch);
        Assert.Same(part, dialog.Shown);
    }

    [Fact]
    public async Task Show_notices_shows_five_texts_in_order_the_main_file_first()
    {
        var dialogs = new DialogHostViewModel();
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, dialogs, new FakeFileLauncher(), new FakeClipboard(), new RecordingLog());

        await about.ShowNoticesCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<TextDocumentDialogViewModel>(Assert.Single(dialogs.Open));
        Assert.Equal("Third-party notices", dialog.Title);
        Assert.True(dialog.ShowsSwitch);
        Assert.Equal(
            ["Components and licenses", "Avalonia", ".NET Community Toolkit", "Onigwrap", "SkiaSharp and HarfBuzzSharp"],
            dialog.Parts.Select(part => part.Label));
        Assert.Equal(
            ["TextDocumentPartMain", "TextDocumentPartAvalonia", "TextDocumentPartCommunityToolkitMvvm", "TextDocumentPartOnigwrap", "TextDocumentPartSkiaSharp"],
            dialog.Parts.Select(part => part.AutomationId));
        Assert.Equal(
            [EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices), .. EmbeddedTexts.VendorNotices().Select(EmbeddedTexts.Read)],
            dialog.Parts.Select(part => part.Text));
        Assert.Same(dialog.Parts[0], dialog.Shown);
    }

    [Fact]
    public async Task The_links_hand_the_launcher_the_repository_and_the_guide_and_nothing_else()
    {
        var launcher = new FakeFileLauncher();
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, new DialogHostViewModel(), launcher, new FakeClipboard(), new RecordingLog());
        Assert.True(about.OpenRepositoryCommand.CanExecute(null));
        Assert.True(about.OpenGuideCommand.CanExecute(null));

        await about.OpenRepositoryCommand.ExecuteAsync(null);
        await about.OpenGuideCommand.ExecuteAsync(null);

        Assert.Equal(
            [new FileLaunch(FileLaunchKind.OpenUrl, "https://github.com/RahimPasha/VisualCommit"), new FileLaunch(FileLaunchKind.OpenUrl, AppLinks.GuideBase)],
            launcher.Requests);
    }

    [Fact]
    public void Without_a_launcher_both_links_are_disabled()
    {
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, new DialogHostViewModel(), null, null, new RecordingLog());

        Assert.False(about.OpenRepositoryCommand.CanExecute(null));
        Assert.False(about.OpenGuideCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_launcher_that_fails_is_a_warning_for_each_link()
    {
        var log = new RecordingLog();
        var about = new AboutSettingsViewModel(TestIdentity.Pinned, new DialogHostViewModel(), new RefusingLauncher(), null, log);

        await about.OpenRepositoryCommand.ExecuteAsync(null);
        await about.OpenGuideCommand.ExecuteAsync(null);

        Assert.Equal(
            [(LogLevel.Warning, "Opening the repository's page failed."), (LogLevel.Warning, "Opening the user guide failed.")],
            log.Entries.Select(entry => (entry.Level, entry.Message)));
        Assert.All(log.Entries, entry => Assert.IsType<InvalidOperationException>(entry.Exception));
    }

    [Fact]
    public async Task A_text_that_cannot_be_read_is_an_error_and_shows_no_dialog()
    {
        var log = new RecordingLog();
        var dialogs = new DialogHostViewModel();
        var about = new AboutSettingsViewModel(
            TestIdentity.Pinned,
            dialogs,
            new FakeFileLauncher(),
            null,
            log,
            name => name == EmbeddedTexts.License ? throw new FileNotFoundException("gone", name) : EmbeddedTexts.Read(name));

        await about.ShowLicenseCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Open);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Error, "The text VisualCommit.License.txt could not be read."), (entry.Level, entry.Message));
        Assert.IsType<FileNotFoundException>(entry.Exception);

        // The notices do not need the license: they still show.
        await about.ShowNoticesCommand.ExecuteAsync(null);
        Assert.IsType<TextDocumentDialogViewModel>(Assert.Single(dialogs.Open));
    }

    /// <summary>A launcher whose browser cannot open an address.</summary>
    private sealed class RefusingLauncher : IFileLauncher
    {
        public Task OpenAsync(string fullPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenInEditorAsync(LaunchCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenUrlAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("No browser."));
    }
}
