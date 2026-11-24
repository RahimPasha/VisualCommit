using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>What one press of "Try again" found.</summary>
public enum NoticeRetry
{
    /// <summary>The other instance answered: it came to the front, or is starting.</summary>
    HandedOver,

    /// <summary>The data folder is free now: the app starts here (D422).</summary>
    Free,

    /// <summary>The other instance still did not answer.</summary>
    NoAnswer,
}

/// <summary>How the notice ended.</summary>
public enum NoticeOutcome
{
    /// <summary>The other instance answered; this process ends with exit code 0.</summary>
    HandedOver,

    /// <summary>The folder was free: the session starts in this process (D422).</summary>
    StartHere,

    /// <summary>"Close", Esc, the title bar's close or Alt+F4; this process ends with exit code 2 (D421).</summary>
    Closed,
}

/// <summary>
/// The notice shown, with no session, when another VisualCommit holds the data folder and did not
/// answer a second start (D421): its texts (<see cref="InstanceNoticeTexts"/>), the data folder, and
/// "Try again", whose try runs off the UI thread and is answered here. "Close" ends it at once, also
/// during a try, whose later result then changes nothing. <see cref="Finished"/> says once how it
/// ended; the window and the app act on it.
/// </summary>
public sealed partial class InstanceNoticeViewModel : ObservableObject
{
    private readonly Func<Task<NoticeRetry>> _tryAgain;

    /// <param name="platform">Whose sentence on what to do when no window of the other is to be seen.</param>
    /// <param name="folder">The data folder's full path, shown on a line of its own.</param>
    /// <param name="tryAgain">One try, run when "Try again" is pressed: the app's goes to the guard on a pool thread.</param>
    public InstanceNoticeViewModel(GuidancePlatform platform, string folder, Func<Task<NoticeRetry>> tryAgain)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(tryAgain);
        Folder = folder;
        IfNoWindow = InstanceNoticeTexts.IfNoWindow(platform);
        _tryAgain = tryAgain;
    }

    /// <summary>Raised once, when the notice ends, with how.</summary>
    public event EventHandler<NoticeOutcome>? Finished;

    public string Title => InstanceNoticeTexts.Title;

    public string Heading => InstanceNoticeTexts.Heading;

    /// <summary>The data folder's full path (D448): on a line of its own, its start cut first when it is too long.</summary>
    public string Folder { get; }

    public string Body => InstanceNoticeTexts.Body;

    /// <summary>The platform's sentence.</summary>
    public string IfNoWindow { get; }

    /// <summary>What a screen reader speaks after the heading: the body, one space, the platform's sentence.</summary>
    public string HelpText => Body + " " + IfNoWindow;

    public string Status => InstanceNoticeTexts.StillNoAnswer;

    /// <summary>
    /// The status line's text: <see cref="Status"/> once a try went unanswered, empty before. The line
    /// is a polite live region (D435), but it shows with this text in one step, before any screen
    /// reader can have reached it: a reader finds the words when it reads the notice, and is not told
    /// of them as they come.
    /// </summary>
    public string StatusLine => ShowsStatus ? Status : string.Empty;

    public string CloseText => InstanceNoticeTexts.Close;

    /// <summary>The status shows after a try that the other instance did not answer either.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial bool ShowsStatus { get; private set; }

    /// <summary>A try runs: "Try again" reads "Trying…" and is disabled.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TryAgainText))]
    [NotifyCanExecuteChangedFor(nameof(TryAgainCommand))]
    public partial bool IsTrying { get; private set; }

    public string TryAgainText => IsTrying ? InstanceNoticeTexts.Trying : InstanceNoticeTexts.TryAgain;

    /// <summary>How the notice ended; null while it shows.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TryAgainCommand))]
    public partial NoticeOutcome? Outcome { get; private set; }

    /// <summary>
    /// The settings file's theme, read without writing anything (a file that cannot be read is left
    /// where it is, not set aside as the settings store would): Dark when the file is missing or
    /// cannot be read, as the settings store would start. System is the caller's to resolve.
    /// </summary>
    public static AppTheme ReadTheme(string settingsFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);
        try
        {
            if (!File.Exists(settingsFile))
            {
                return AppTheme.Dark;
            }

            var json = File.ReadAllText(settingsFile);
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings)?.Theme ?? AppTheme.Dark;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return AppTheme.Dark;
        }
    }

    /// <summary>
    /// One try over the guard, off the UI thread: the lock if it is free now; else the other is
    /// asked again, and an answer hands over, while a closing one is waited for.
    /// </summary>
    public static NoticeRetry RetryOn(SingleInstanceGuard guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        if (guard.TryTakeNow())
        {
            return NoticeRetry.Free;
        }

        return guard.AskToComeForward() switch
        {
            ForwardAnswer.Activated or ForwardAnswer.Starting => NoticeRetry.HandedOver,
            ForwardAnswer.Closing => guard.WaitToTake() ? NoticeRetry.Free : NoticeRetry.NoAnswer,
            _ => NoticeRetry.NoAnswer,
        };
    }

    private bool CanTryAgain() => !IsTrying && Outcome is null;

    [RelayCommand(CanExecute = nameof(CanTryAgain))]
    private async Task TryAgainAsync()
    {
        IsTrying = true;
        NoticeRetry result;
        try
        {
            result = await _tryAgain();
        }
        catch (Exception)
        {
            // A try that fails is one the other instance did not answer: the notice stays.
            result = NoticeRetry.NoAnswer;
        }

        if (Outcome is not null)
        {
            // Closed while the try ran: its result changes nothing.
            return;
        }

        switch (result)
        {
            case NoticeRetry.HandedOver:
                Finish(NoticeOutcome.HandedOver);
                break;
            case NoticeRetry.Free:
                Finish(NoticeOutcome.StartHere);
                break;
            default:
                ShowsStatus = true;
                IsTrying = false;
                break;
        }
    }

    /// <summary>"Close", Esc, the title bar's close and Alt+F4; at once, also while a try runs.</summary>
    [RelayCommand]
    private void Close() => Finish(NoticeOutcome.Closed);

    private void Finish(NoticeOutcome outcome)
    {
        if (Outcome is not null)
        {
            return;
        }

        Outcome = outcome;
        Finished?.Invoke(this, outcome);
    }
}
