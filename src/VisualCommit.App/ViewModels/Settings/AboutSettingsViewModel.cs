using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AboutTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The settings screen's About section (D398, D409, D431): the app's name, its creator, its
/// version with the short commit, its copyright line, its license and the third-party notices,
/// each shown whole in the text dialog (D432), and the links to the repository and the user guide,
/// which open in the browser through the launcher. It saves nothing. The texts are read from the
/// app's own assembly off the UI thread, and the dialog is shown without waiting for it to close.
/// </summary>
public sealed class AboutSettingsViewModel
{
    private readonly AppIdentity _identity;
    private readonly DialogHostViewModel _dialogs;
    private readonly IFileLauncher? _launcher;
    private readonly IClipboardService? _clipboard;
    private readonly IAppLog _log;
    private readonly Func<string, string> _readText;
    private readonly Func<IReadOnlyList<string>> _vendorNotices;
    private readonly CancellationToken _lifetime;

    /// <summary>A Show button's texts are being read: a second press meanwhile opens nothing more.</summary>
    private bool _reading;

    /// <param name="identity">Who and what the app is.</param>
    /// <param name="dialogs">The window's dialog layer, where the text dialog stacks over the screen (D104).</param>
    /// <param name="launcher">Opens the links in the browser; null disables them.</param>
    /// <param name="clipboard">Where the text dialog's "Copy all" copies to; null copies nothing.</param>
    /// <param name="log">Where a text that cannot be read and a link that cannot be opened are logged.</param>
    /// <param name="readText">Reads an embedded text by its resource's name; null reads the app's (<see cref="EmbeddedTexts.Read"/>).</param>
    /// <param name="vendorNotices">The vendors' notice files' resource names, in order; null takes the app's (<see cref="EmbeddedTexts.VendorNotices"/>).</param>
    /// <param name="lifetime">The settings screen's token, cancelled when Cancel, Esc, Save or the session's end closes it: texts read after that show no dialog.</param>
    public AboutSettingsViewModel(
        AppIdentity identity,
        DialogHostViewModel dialogs,
        IFileLauncher? launcher,
        IClipboardService? clipboard,
        IAppLog log,
        Func<string, string>? readText = null,
        Func<IReadOnlyList<string>>? vendorNotices = null,
        CancellationToken lifetime = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(log);
        _identity = identity;
        _dialogs = dialogs;
        _launcher = launcher;
        _clipboard = clipboard;
        _log = log;
        _readText = readText ?? EmbeddedTexts.Read;
        _vendorNotices = vendorNotices ?? EmbeddedTexts.VendorNotices;
        _lifetime = lifetime;

        // A command that runs keeps its button enabled: a disabled button would lose the keyboard
        // focus, which the dialog gives back to it when it closes (and a link keeps after Enter).
        const AsyncRelayCommandOptions Enabled = AsyncRelayCommandOptions.AllowConcurrentExecutions;
        ShowLicenseCommand = new AsyncRelayCommand(ShowLicenseAsync, Enabled);
        ShowNoticesCommand = new AsyncRelayCommand(ShowNoticesAsync, Enabled);
        OpenRepositoryCommand = new AsyncRelayCommand(() => OpenAsync(AppLinks.Repository, Texts.RepositoryFailed), () => _launcher is not null, Enabled);
        OpenGuideCommand = new AsyncRelayCommand(() => OpenAsync(AppLinks.Guide(GuidePage.Start), Texts.GuideFailed), () => _launcher is not null, Enabled);
    }

    /// <summary>The app's name (<c>AboutName</c>).</summary>
    public string Name => _identity.Name;

    /// <summary><c>Created by {creator}</c> (<c>AboutCreator</c>).</summary>
    public string CreatedBy => Texts.CreatedBy(_identity.Creator);

    /// <summary>The version with the short commit, <c>0.1.0 (3dc0887)</c> (<c>AboutVersion</c>).</summary>
    public string Version => _identity.Version.Display;

    /// <summary>The copyright line as <c>LICENSE</c> has it (<c>AboutCopyright</c>).</summary>
    public string Copyright => _identity.Copyright;

    /// <summary>The repository link's text: its address's host and path.</summary>
    public string RepositoryLinkText => Texts.LinkText(AppLinks.Repository);

    /// <summary>"Show license…": <c>LICENSE</c> in the text dialog.</summary>
    public IAsyncRelayCommand ShowLicenseCommand { get; }

    /// <summary>"Show third-party notices…": <c>THIRD-PARTY-NOTICES.md</c>, then each vendor's file, in the text dialog.</summary>
    public IAsyncRelayCommand ShowNoticesCommand { get; }

    /// <summary>The repository link; disabled without a launcher.</summary>
    public IAsyncRelayCommand OpenRepositoryCommand { get; }

    /// <summary>The user guide's link, its start page; disabled without a launcher.</summary>
    public IAsyncRelayCommand OpenGuideCommand { get; }

    /// <summary>The automation id of a vendor file's switch button: <c>TextDocumentPart</c> and the file's name before its first '-', without dots.</summary>
    public static string PartId(string file) =>
        "TextDocumentPart" + Texts.NoticeFileKey(file).Replace(".", string.Empty, StringComparison.Ordinal);

    private Task ShowLicenseAsync() =>
        ShowAsync(Texts.LicenseTitle, [(EmbeddedTexts.License, Texts.MitLicense, "TextDocumentPartMain")]);

    private Task ShowNoticesAsync() =>
        ShowAsync(
            Texts.NoticesTitle,
            [
                (EmbeddedTexts.ThirdPartyNotices, Texts.MainNotices, "TextDocumentPartMain"),
                .. _vendorNotices().Select(name => (name, Texts.NoticeFileLabel(EmbeddedTexts.FileNameOf(name)), PartId(EmbeddedTexts.FileNameOf(name)))),
            ]);

    /// <summary>
    /// Reads the texts off the UI thread and shows them in the text dialog, without waiting for it to
    /// close. A text that cannot be read is logged as an Error, and then no dialog shows; nor does one
    /// when the settings screen closed while the texts were read.
    /// </summary>
    private async Task ShowAsync(string title, IReadOnlyList<(string Resource, string Label, string AutomationId)> texts)
    {
        if (_reading)
        {
            return;
        }

        _reading = true;
        List<((string Resource, string Label, string AutomationId) text, string? Text)> read;
        try
        {
            read = await Task.Run(() => texts.Select(text => (text, Text: TryRead(text.Resource))).ToList());
        }
        finally
        {
            _reading = false;
        }

        if (_lifetime.IsCancellationRequested || read.Any(text => text.Text is null))
        {
            return;
        }

        var parts = read.Select(text => new TextDocumentPart(text.text.Label, text.Text!, text.text.AutomationId)).ToList();
        _ = _dialogs.ShowAsync(new TextDocumentDialogViewModel(title, parts, _clipboard, _log));
    }

    private string? TryRead(string resource)
    {
        try
        {
            return _readText(resource);
        }
        catch (Exception ex)
        {
            _log.Error(Texts.TextNotRead(resource), ex);
            return null;
        }
    }

    private async Task OpenAsync(Uri address, string failure)
    {
        if (_launcher is null)
        {
            return;
        }

        try
        {
            await _launcher.OpenUrlAsync(address);
        }
        catch (Exception ex)
        {
            _log.Warning(failure, ex);
        }
    }
}
