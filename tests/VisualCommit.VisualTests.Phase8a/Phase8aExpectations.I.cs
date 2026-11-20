using Avalonia.Media;
using VisualCommit.Core.Git;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// Part I, one instance per data folder (docs/test-reports/phase-8a.md, checks I1 to I3;
    /// docs/phase-8a-design.md 1.11, 4.3.4, 4.3.5, 4.6, 12.3.4 launches 4 to 6): the notice's texts,
    /// automation ids, colours and picture names, the made-up data folders, and the log lines'
    /// fragments the real window reads, written from the report and not from the app's texts. The
    /// real-window pass links this file (design 0.3): it uses the BCL, Avalonia and Core only.
    /// </summary>
    public static class I
    {
        // ---- The notice's texts (4.3.4), word for word ---------------------------------------

        public const string Title = "VisualCommit";
        public const string Heading = "VisualCommit is already running";
        public const string Body = "Another VisualCommit uses the same data folder but did not answer. Switch to its window, or close it and start VisualCommit again.";
        public const string IfNoWindowOnWindows = "If no VisualCommit window is open, end VisualCommit in Task Manager, then choose Try again.";
        public const string IfNoWindowOnMacOS = "If no VisualCommit window is open, quit VisualCommit in Activity Monitor, then choose Try again.";
        public const string IfNoWindowOnLinux = "If no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.";
        public const string TryAgain = "Try again";
        public const string Trying = "Trying…";
        public const string Close = "Close";
        public const string StillNoAnswer = "It still did not answer.";

        // ---- The notice's automation ids (1.11) ----------------------------------------------

        public const string NoticeId = "InstanceNotice";
        public const string HeadingId = "InstanceNoticeHeading";

        /// <summary>The data folder's line, which the real window's masked comparison paints over (D448).</summary>
        public const string FolderId = "InstanceNoticeFolder";

        public const string BodyId = "InstanceNoticeBody";
        public const string IfNoWindowId = "InstanceNoticeIfNoWindow";
        public const string StatusId = "InstanceNoticeStatus";
        public const string TryAgainId = "InstanceNoticeTryAgain";
        public const string CloseId = "InstanceNoticeClose";

        // ---- The notice's frame (4.3.4) ------------------------------------------------------

        /// <summary>The notice's width; its height is its content's.</summary>
        public const int Width = 480;

        // ---- The made-up data folders (ruling 63) --------------------------------------------

        /// <summary>I2's folder, on Windows.</summary>
        public const string WindowsFolder = @"C:\Users\demo\AppData\Roaming\VisualCommit";

        /// <summary>I3's folder, on Linux.</summary>
        public const string LinuxFolder = "/home/demo/.config/VisualCommit";

        // ---- The colours: tokens the contrast round keeps, dark then light -------------------

        /// <summary><c>VcPanelBackgroundBrush</c>, the notice's background.</summary>
        public static readonly Color BackgroundDark = Color.FromRgb(0x1A, 0x1D, 0x24);

        public static readonly Color BackgroundLight = Color.FromRgb(0xF5, 0xF6, 0xF8);

        /// <summary><c>VcTextPrimaryBrush</c>: the heading, the body and the sentence.</summary>
        public static readonly Color TextDark = Color.FromRgb(0xE4, 0xE7, 0xEC);

        public static readonly Color TextLight = Color.FromRgb(0x1B, 0x1F, 0x27);

        /// <summary><c>VcTextSecondaryBrush</c>: the folder's line.</summary>
        public static readonly Color FolderDark = Color.FromRgb(0x9B, 0xA3, 0xB0);

        public static readonly Color FolderLight = Color.FromRgb(0x5C, 0x65, 0x72);

        /// <summary><c>VcWarningBrush</c>: the status.</summary>
        public static readonly Color StatusDark = Color.FromRgb(0xE0, 0xA2, 0x3B);

        public static readonly Color StatusLight = Color.FromRgb(0x89, 0x5A, 0x17);

        // ---- The pictures (the report's Screenshots column) ----------------------------------

        public const string I2aNoticeDark = "i2a-notice-dark";
        public const string I2bNoticeTrying = "i2b-notice-trying";
        public const string I2cNoticeNoAnswer = "i2c-notice-no-answer";
        public const string I3aNoticeLight = "i3a-notice-light";

        /// <summary>Every picture of part I, scripted; the real window's I2 captures take the same three names.</summary>
        public static IReadOnlyList<string> Pictures { get; } = [I2aNoticeDark, I2bNoticeTrying, I2cNoticeNoAnswer, I3aNoticeLight];

        /// <summary>The pictures the real window repeats, masked (launches 5 and 6).</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [I2aNoticeDark, I2bNoticeTrying, I2cNoticeNoAnswer];

        // ---- The log lines the real window reads (4.3.5); {0} the data folder, {1} a process id

        public const string HoldsTheFolderLine = "This VisualCommit holds the data folder {0}: a second start on it is sent here.";
        public const string AskedToComeForwardLine = "VisualCommit already runs on the data folder {0} (process {1}); it was asked to come to the front, and this start ends.";

        /// <summary>The holder's line; {0} the second start's process id.</summary>
        public const string AnsweredActivatedLine = "A second VisualCommit (process {0}) started on this data folder: answered activated.";

        public const string NoInstanceFileLine = "VisualCommit holds the data folder {0} but did not answer (no instance.json).";

        // ---- The real window's launches (the report's "The real window (part I)"; 12.3.4 launches 4 to 6)

        /// <summary>I1's first instance: a session of one empty tab, dark, at <see cref="DefaultSize"/>, written before the start.</summary>
        public const int FirstInstanceEmptyTabs = 1;

        /// <summary>I1's second process, which hands over before any window.</summary>
        public const int HandOverExitCode = 0;

        /// <summary>Launch 5, after "Close" on the notice.</summary>
        public const int ClosedExitCode = 2;

        /// <summary>Launch 6, after "Try again" started the app in the same process and its window closed.</summary>
        public const int StartedHereExitCode = 0;

        /// <summary>I1: the second process exits within this, having shown no window.</summary>
        public static readonly TimeSpan SecondStartExitsWithin = TimeSpan.FromSeconds(10);

        /// <summary>I1: the minimised first instance is not iconic and visible within this.</summary>
        public static readonly TimeSpan FirstComesBackWithin = TimeSpan.FromSeconds(2);

        /// <summary>Launches 5 and 6: the process's main window is the notice within this.</summary>
        public static readonly TimeSpan NoticeShowsWithin = TimeSpan.FromSeconds(15);

        /// <summary>Launch 5: the status reads <see cref="StillNoAnswer"/> within this after Enter.</summary>
        public static readonly TimeSpan StatusShowsWithin = TimeSpan.FromSeconds(10);

        /// <summary>Launch 5: the process exits with <see cref="ClosedExitCode"/> within this after "Close".</summary>
        public static readonly TimeSpan ClosedExitWithin = TimeSpan.FromSeconds(5);

        /// <summary>Launch 6: the main window, with <c>GitStatus</c>, within this after Enter, and the exit after its close.</summary>
        public static readonly TimeSpan StartedHereWithin = TimeSpan.FromSeconds(15);

        /// <summary>Launches 5 and 6: the title-bar click, when needed, and the foreground and focus asserted again within this.</summary>
        public static readonly TimeSpan ForegroundAgainWithin = TimeSpan.FromSeconds(5);

        /// <summary>Launch 5: what the data folder holds after "Close", by name; no session started, nothing written.</summary>
        public static IReadOnlyList<string> NoticeDataFolderEntries { get; } = ["harness", "instance.lock", "logs"];

        /// <summary>Launches 5 and 6 fail with this before any key when the notice is not the foreground window with Try again focused.</summary>
        public const string NotForegroundMessage = "The notice is not the foreground window with Try again focused: a real Enter would reach another window.";

        /// <summary>The masked comparison's failure when the heights differ; {0} the capture's height divided by the scaling, {1} the twin's.</summary>
        public const string HeightsDifferMessage = "The notice is {0} high and its scripted twin {1}: their layouts differ, so the masked comparison cannot hold.";

        /// <summary>The platform's sentence, as the notice shows it on each platform.</summary>
        public static string IfNoWindow(GuidancePlatform platform) => platform switch
        {
            GuidancePlatform.Windows => IfNoWindowOnWindows,
            GuidancePlatform.MacOS => IfNoWindowOnMacOS,
            _ => IfNoWindowOnLinux,
        };

        /// <summary>The window's help text: the body, one space and the platform's sentence (the lead's ruling of 2026-11-20).</summary>
        public static string HelpText(GuidancePlatform platform) => Body + " " + IfNoWindow(platform);
    }
}
