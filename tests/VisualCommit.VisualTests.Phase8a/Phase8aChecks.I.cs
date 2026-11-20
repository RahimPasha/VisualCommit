using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part I, one instance per data folder (docs/test-reports/phase-8a.md, checks I2 and I3; design
/// 4.3.4, 4.6): the notice shown when another VisualCommit holds the data folder and did not answer,
/// through <see cref="InstanceNoticeHost"/> with a fake try that a step holds and answers. I1 runs in
/// the real window alone, since no harness runs <c>Program.Main</c>.
/// </summary>
public sealed partial class Phase8aChecksI : Phase8aChecks
{
    /// <summary>The point of the frame where the background is read: inside the padding, clear of text.</summary>
    private static readonly PixelPoint ClearOfText = new(8, 8);

    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>I2: the notice, dark, on Windows; Enter runs a try that is held, then left unanswered; Esc closes it.</summary>
    [AvaloniaFact]
    public async Task Check_I2_notice_dark_try_unanswered()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Windows, I.WindowsFolder, ThemeVariant.Dark);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        var close = host.Find<Button>(I.CloseId);
        var status = host.Find<TextBlock>(I.StatusId);

        // Step 1: shown, Try again focused.
        await host.WaitForAsync(() => ReferenceEquals(host.Focused, tryAgain), "Try again to take the keyboard focus");
        var opened = ShotNotice(host, I.I2aNoticeDark);
        AssertNoticeAsOpened(host, opened, GuidancePlatform.Windows, I.WindowsFolder, dark: true);
        AssertNoticeInTheTree(host);
        var buttonsAtRest = (Close: host.BoundsOf(close), TryAgain: host.BoundsOf(tryAgain));

        // Step 2: Enter; the try is held.
        host.PressKey(Key.Enter);
        await host.WaitForAsync(() => tryAgain.Content as string == I.Trying, "Try again to read \"Trying…\"");
        var trying = ShotNotice(host, I.I2bNoticeTrying);
        Assert.Equal(1, host.Try.Asked);
        Assert.True(host.Try.IsHeld);
        Assert.False(tryAgain.IsEffectivelyEnabled, "Try again is enabled while its try runs.");
        Assert.Equal(I.Trying, AutomationProperties.GetName(tryAgain));
        Assert.True(close.IsEffectivelyEnabled, "Close is disabled while the try runs.");
        Assert.False(status.IsEffectivelyVisible, "The status shows before the try ended.");
        Assert.Equal(opened.Height, trying.Height);
        Assert.Equal(I.Width, trying.Width);

        // The buttons stay where they were: "Try again" keeps its width while it reads "Trying…".
        Assert.Equal(buttonsAtRest, (Close: host.BoundsOf(close), TryAgain: host.BoundsOf(tryAgain)));
        AssertNoClippedNoticeText(host);

        // Step 3: the held try gets no answer.
        host.Try.Answer(NoticeRetry.NoAnswer);
        await host.WaitForAsync(() => status.IsEffectivelyVisible && ReferenceEquals(host.Focused, tryAgain), "the status to show and Try again to have the focus again");
        var unanswered = ShotNotice(host, I.I2cNoticeNoAnswer);
        Assert.Equal(I.StillNoAnswer, status.Text);
        var sentence = host.BoundsOf(host.Find<TextBlock>(I.IfNoWindowId));
        var statusBounds = host.BoundsOf(status);
        Assert.True(statusBounds.Top >= sentence.Bottom, $"The status at {statusBounds} is not below the sentence at {sentence}.");
        Assert.True(statusBounds.Bottom <= host.BoundsOf(close).Top, $"The status at {statusBounds} is not above the buttons.");
        AssertInk(unanswered, host, status, I.StatusDark, I.BackgroundDark, "the status");
        Assert.Equal(I.StatusDark, (status.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(I.TryAgain, tryAgain.Content);
        Assert.Equal(I.TryAgain, AutomationProperties.GetName(tryAgain));
        Assert.True(tryAgain.IsEffectivelyEnabled, "Try again is disabled after the unanswered try.");
        Assert.Equal(I.Width, unanswered.Width);
        Assert.True(
            Math.Abs(unanswered.Height - opened.Height - (status.Bounds.Height + status.Margin.Top)) <= 1,
            $"The notice is {unanswered.Height} high after the try and {opened.Height} before; the status adds {status.Bounds.Height} and its margin {status.Margin.Top}.");
        Assert.Null(host.Notice.Outcome);
        AssertNoClippedNoticeText(host);

        // Step 4: Esc closes.
        host.PressKey(Key.Escape);
        await host.WaitForAsync(() => !host.IsOpen, "the notice to close");
        Assert.Equal(NoticeOutcome.Closed, host.Notice.Outcome);
        Assert.Equal(1, host.Try.Asked);
    }

    /// <summary>I3: the notice, light, on Linux; Tab and Shift+Tab go round its two buttons, Space on Close closes it.</summary>
    [AvaloniaFact]
    public async Task Check_I3_notice_light_by_keys()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Linux, I.LinuxFolder, ThemeVariant.Light);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        var close = host.Find<Button>(I.CloseId);

        // Step 1: shown, Try again focused.
        await host.WaitForAsync(() => ReferenceEquals(host.Focused, tryAgain), "Try again to take the keyboard focus");
        var opened = ShotNotice(host, I.I3aNoticeLight);
        AssertNoticeAsOpened(host, opened, GuidancePlatform.Linux, I.LinuxFolder, dark: false);
        AssertNoticeInTheTree(host);

        // Steps 2 to 4: Tab, Tab, Shift+Tab.
        host.PressKey(Key.Tab);
        Assert.Same(close, host.Focused);
        host.PressKey(Key.Tab);
        Assert.Same(tryAgain, host.Focused);
        host.PressKey(Key.Tab, RawInputModifiers.Shift);
        Assert.Same(close, host.Focused);
        Assert.Equal(0, host.Try.Asked);
        Assert.Equal(I.TryAgain, tryAgain.Content);

        // Step 5: Space on Close.
        host.PressKey(Key.Space);
        await host.WaitForAsync(() => !host.IsOpen, "the notice to close");
        Assert.Equal(NoticeOutcome.Closed, host.Notice.Outcome);
        Assert.Equal(0, host.Try.Asked);
    }

    /// <summary>
    /// The notice as it opens (I2 and I3 after step 1): 480 wide on its background; top to bottom the
    /// heading, the folder on one whole line, the body and the platform's sentence, in their inks; no
    /// status; Close then Try again at the right, both enabled, Try again focused; the window's and
    /// the buttons' automation names; no text cut; no outcome yet.
    /// </summary>
    private static void AssertNoticeAsOpened(InstanceNoticeHost host, Screenshot frame, GuidancePlatform platform, string folder, bool dark)
    {
        var (background, text, folderInk) = dark ? (I.BackgroundDark, I.TextDark, I.FolderDark) : (I.BackgroundLight, I.TextLight, I.FolderLight);
        Assert.Equal(I.Width, frame.Width);
        Assert.Equal(background, Opaque(frame.PixelAt(ClearOfText.X, ClearOfText.Y)));

        var heading = host.Find<TextBlock>(I.HeadingId);
        var folderLine = host.Find<TrimmedTextBlock>(I.FolderId);
        var body = host.Find<TextBlock>(I.BodyId);
        var sentence = host.Find<TextBlock>(I.IfNoWindowId);
        var status = host.Find<TextBlock>(I.StatusId);
        var close = host.Find<Button>(I.CloseId);
        var tryAgain = host.Find<Button>(I.TryAgainId);

        Assert.Equal(I.Heading, heading.Text);
        Assert.Equal(folder, folderLine.Text);
        Assert.Equal(I.Body, body.Text);
        Assert.Equal(I.IfNoWindow(platform), sentence.Text);
        var order = new Control[] { heading, folderLine, body, sentence, close }.Select(host.BoundsOf).ToList();
        for (var at = 1; at < order.Count; at++)
        {
            Assert.True(order[at].Top >= order[at - 1].Bottom, $"The notice's parts are not top to bottom: {order[at - 1]} then {order[at]}.");
        }

        // The folder: one line, whole, its tooltip and automation name the path (D448).
        Assert.Single(folderLine.TextLayout.TextLines);
        Assert.False(folderLine.IsTrimmed, $"{folder} is cut.");
        Assert.Equal(folder, ToolTip.GetTip(folderLine) as string);
        Assert.Equal(folder, AutomationProperties.GetName(folderLine));

        AssertInk(frame, host, heading, text, background, "the heading");
        AssertInk(frame, host, folderLine, folderInk, background, "the folder");
        AssertInk(frame, host, body, text, background, "the body");
        AssertInk(frame, host, sentence, text, background, "the sentence");
        Assert.Equal(text, (heading.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(folderInk, (folderLine.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(text, (body.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(text, (sentence.Foreground as ISolidColorBrush)?.Color);

        Assert.False(status.IsEffectivelyVisible, "The status shows before any try.");

        var closeBounds = host.BoundsOf(close);
        var tryAgainBounds = host.BoundsOf(tryAgain);
        Assert.Equal(I.Close, close.Content);
        Assert.Equal(I.TryAgain, tryAgain.Content);
        Assert.True(closeBounds.Right <= tryAgainBounds.Left, $"Close at {closeBounds} is not left of Try again at {tryAgainBounds}.");
        Assert.True(Math.Abs(closeBounds.Top - tryAgainBounds.Top) < 0.5, "Close and Try again are not on one row.");
        Assert.True(Math.Abs(tryAgainBounds.Right - (I.Width - 20)) <= 1, $"Try again ends at {tryAgainBounds.Right}, not at the padding's edge, {I.Width - 20}.");
        Assert.True(close.IsEffectivelyEnabled && tryAgain.IsEffectivelyEnabled, "A button is disabled.");
        Assert.Same(tryAgain, host.Focused);

        Assert.Equal(I.Heading, AutomationProperties.GetName(host.Window));
        Assert.Equal(I.HelpText(platform), AutomationProperties.GetHelpText(host.Window));
        Assert.Equal(I.NoticeId, AutomationProperties.GetAutomationId(host.Window));
        Assert.Equal(I.Close, AutomationProperties.GetName(close));
        Assert.Equal(I.TryAgain, AutomationProperties.GetName(tryAgain));

        AssertNoClippedNoticeText(host);
        Assert.Null(host.Notice.Outcome);
        Assert.Equal(0, host.Try.Asked);
    }

    /// <summary>What neither the frame nor the real capture shows, read in the tree (I2 and I3 after step 1).</summary>
    private static void AssertNoticeInTheTree(InstanceNoticeHost host)
    {
        var window = host.Window;
        Assert.Equal(I.Title, window.Title);
        Assert.False(window.CanResize);
        Assert.Equal(SizeToContent.Height, window.SizeToContent);
        Assert.Equal(I.Width, window.Width);

        var close = host.Find<Button>(I.CloseId);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        Assert.Same(window.FindResource("VcActionButton"), close.Theme);
        Assert.Same(window.FindResource("VcPrimaryButton"), tryAgain.Theme);
        Assert.True(tryAgain.IsDefault);

        var folder = host.Find<TrimmedTextBlock>(I.FolderId);
        Assert.Equal(TextTrimming.LeadingCharacterEllipsis, folder.TextTrimming);
        Assert.Equal(TextWrapping.NoWrap, folder.TextWrapping);
        Assert.Equal(HorizontalAlignment.Right, ((Control)close.GetVisualParent()!).HorizontalAlignment);
    }

    /// <summary>A text's ink in the frame (TextInk's rule) within its control's bounds.</summary>
    private static void AssertInk(Screenshot frame, InstanceNoticeHost host, Control text, Color ink, Color background, string what) =>
        Assert.Empty(TextInk.Problems(frame, host.BoundsOf(text), ink, background, what: what));

    private static void AssertNoClippedNoticeText(InstanceNoticeHost host)
    {
        var problems = LayoutAudit.FindClippedText(host.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(problems.Count == 0, "Clipped text:\n" + string.Join("\n", problems));
    }

    private static Color Opaque(Color colour) => Color.FromRgb(colour.R, colour.G, colour.B);

    /// <summary>
    /// Saves the notice's frame as artifacts/visual/phase-8a/scripted/<paramref name="name"/>.png, as
    /// <see cref="Phase8aChecks.Shot"/> saves a window's: the name claimed once in the run, the step's
    /// time noted under artifacts/visual/phase-8a/timings/. No pointer moves: the notice is driven by
    /// keys alone.
    /// </summary>
    private static Screenshot ShotNotice(InstanceNoticeHost host, string name)
    {
        ClaimPictureName(name);
        var frame = host.Capture();
        frame.Save(Phase, name);
        NoteTime(host.MarkStep(name));
        return frame;
    }

    private static void NoteTime(StepTime step)
    {
        var test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
        var name = string.Concat(test.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_'));
        var file = Path.Combine(Path.GetDirectoryName(VisualRun.FolderFor(Phase))!, "timings", name + ".txt");
        lock (TimingGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            if (TimingsStarted.Add(test))
            {
                File.WriteAllText(file, test + "\n");
            }

            File.AppendAllText(file, step + "\n");
        }
    }
}
