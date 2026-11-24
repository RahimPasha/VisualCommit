using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The notice shown when another VisualCommit holds the data folder and did not answer
/// (docs/phase-8a-design.md 4.3.4, 4.5; D421, D422, D448): its texts word for word, "Try again" and
/// its three results, "Close" also during a try, the theme read from <c>settings.json</c> without
/// writing, and a try over real guards on temporary data folders. No session.
/// </summary>
public sealed class InstanceNoticeViewModelTests
{
    private const string Body = "Another VisualCommit uses the same data folder but did not answer. Switch to its window, or close it and start VisualCommit again.";

    [Theory]
    [InlineData(GuidancePlatform.Windows, "If no VisualCommit window is open, end VisualCommit in Task Manager, then choose Try again.")]
    [InlineData(GuidancePlatform.MacOS, "If no VisualCommit window is open, quit VisualCommit in Activity Monitor, then choose Try again.")]
    [InlineData(GuidancePlatform.Linux, "If no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.")]
    public void The_texts_are_the_designs_word_for_word_with_the_platforms_sentence(GuidancePlatform platform, string sentence)
    {
        var notice = new InstanceNoticeViewModel(platform, "/home/demo/.config/VisualCommit", () => Task.FromResult(NoticeRetry.NoAnswer));

        Assert.Equal("VisualCommit", notice.Title);
        Assert.Equal("VisualCommit is already running", notice.Heading);
        Assert.Equal(Body, notice.Body);
        Assert.Equal(sentence, notice.IfNoWindow);
        Assert.Equal(Body + " " + sentence, notice.HelpText);
        Assert.Equal("Try again", notice.TryAgainText);
        Assert.Equal("Close", notice.CloseText);
        Assert.Equal("It still did not answer.", notice.Status);
        Assert.False(notice.ShowsStatus);
        Assert.False(notice.IsTrying);
        Assert.Null(notice.Outcome);
        Assert.True(notice.TryAgainCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(@"C:\Users\demo\AppData\Roaming\VisualCommit")]
    [InlineData("/home/demo/Library/Application Support/VisualCommit")]
    public void The_folder_line_is_the_folder_given(string folder)
    {
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, folder, () => Task.FromResult(NoticeRetry.NoAnswer));

        Assert.Equal(folder, notice.Folder);
    }

    [Fact]
    public async Task While_a_try_runs_the_button_reads_trying_and_is_disabled_and_no_answer_shows_the_status()
    {
        var held = new HeldTry();
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", held.Run);

        var run = notice.TryAgainCommand.ExecuteAsync(null);

        Assert.Equal(1, held.Asked);
        Assert.True(notice.IsTrying);
        Assert.Equal("Trying…", notice.TryAgainText);
        Assert.False(notice.TryAgainCommand.CanExecute(null));
        Assert.True(notice.CloseCommand.CanExecute(null));
        Assert.False(notice.ShowsStatus);
        Assert.Equal(string.Empty, notice.StatusLine);
        var changed = new List<string?>();
        notice.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        held.Answer(NoticeRetry.NoAnswer);
        await run;

        // The status line's text comes with it, in the same step.
        Assert.True(notice.ShowsStatus);
        Assert.Equal("It still did not answer.", notice.StatusLine);
        Assert.Contains(nameof(InstanceNoticeViewModel.StatusLine), changed);
        Assert.False(notice.IsTrying);
        Assert.Equal("Try again", notice.TryAgainText);
        Assert.True(notice.TryAgainCommand.CanExecute(null));
        Assert.Null(notice.Outcome);
    }

    [Theory]
    [InlineData(NoticeRetry.HandedOver, NoticeOutcome.HandedOver)]
    [InlineData(NoticeRetry.Free, NoticeOutcome.StartHere)]
    public async Task A_try_that_hands_over_or_finds_the_folder_free_ends_the_notice(NoticeRetry answer, NoticeOutcome outcome)
    {
        var held = new HeldTry();
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Linux, "/home/demo/.config/VisualCommit", held.Run);
        var finished = new List<NoticeOutcome>();
        notice.Finished += (_, ended) => finished.Add(ended);

        var run = notice.TryAgainCommand.ExecuteAsync(null);
        held.Answer(answer);
        await run;

        Assert.Equal(outcome, notice.Outcome);
        Assert.Equal([outcome], finished);
        Assert.False(notice.TryAgainCommand.CanExecute(null));
        Assert.False(notice.ShowsStatus);
    }

    [Fact]
    public void Close_ends_the_notice_once()
    {
        var held = new HeldTry();
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", held.Run);
        var finished = new List<NoticeOutcome>();
        notice.Finished += (_, ended) => finished.Add(ended);

        notice.CloseCommand.Execute(null);
        notice.CloseCommand.Execute(null);

        Assert.Equal(NoticeOutcome.Closed, notice.Outcome);
        Assert.Equal([NoticeOutcome.Closed], finished);
        Assert.False(notice.TryAgainCommand.CanExecute(null));
        Assert.Equal(0, held.Asked);
    }

    [Theory]
    [InlineData(NoticeRetry.HandedOver)]
    [InlineData(NoticeRetry.Free)]
    [InlineData(NoticeRetry.NoAnswer)]
    public async Task Close_during_a_try_ends_the_notice_at_once_and_the_tries_later_result_changes_nothing(NoticeRetry later)
    {
        var held = new HeldTry();
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", held.Run);
        var finished = new List<NoticeOutcome>();
        notice.Finished += (_, ended) => finished.Add(ended);

        var run = notice.TryAgainCommand.ExecuteAsync(null);
        notice.CloseCommand.Execute(null);

        Assert.Equal(NoticeOutcome.Closed, notice.Outcome);
        Assert.Equal([NoticeOutcome.Closed], finished);

        held.Answer(later);
        await run;

        Assert.Equal(NoticeOutcome.Closed, notice.Outcome);
        Assert.Equal([NoticeOutcome.Closed], finished);
        Assert.False(notice.ShowsStatus);
    }

    [Fact]
    public async Task A_try_that_fails_counts_as_unanswered()
    {
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", () => Task.FromException<NoticeRetry>(new IOException("The pipe broke.")));

        await notice.TryAgainCommand.ExecuteAsync(null);

        Assert.True(notice.ShowsStatus);
        Assert.Null(notice.Outcome);
        Assert.True(notice.TryAgainCommand.CanExecute(null));
    }

    [Fact]
    public void The_theme_is_read_from_settings_without_writing_and_is_dark_when_missing_or_unreadable()
    {
        using var data = new TempDirectory("notice theme");
        var file = data.Combine("settings.json");

        Assert.Equal(AppTheme.Dark, InstanceNoticeViewModel.ReadTheme(file));
        Assert.False(File.Exists(file));

        File.WriteAllText(file, "{\n  \"theme\": \"Light\"\n}");
        Assert.Equal(AppTheme.Light, InstanceNoticeViewModel.ReadTheme(file));

        File.WriteAllText(file, "{\n  \"theme\": \"System\"\n}");
        Assert.Equal(AppTheme.System, InstanceNoticeViewModel.ReadTheme(file));

        const string Unreadable = "{ \"theme\": \"Light\", this is not JSON";
        File.WriteAllText(file, Unreadable);
        Assert.Equal(AppTheme.Dark, InstanceNoticeViewModel.ReadTheme(file));
        Assert.Equal(Unreadable, File.ReadAllText(file));
        Assert.Equal(["settings.json"], Directory.EnumerateFileSystemEntries(data.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void A_try_on_a_free_folder_takes_it_and_on_an_answering_holder_hands_over()
    {
        using var root = new TempDirectory("notice try");
        var paths = new AppPaths(root.Combine("data folder"));
        var options = new SingleInstanceOptions(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), options);
        try
        {
            holder.ComeForward = () => ForwardAnswer.Activated;
            using var second = SingleInstanceGuard.TryTake(paths, new RecordingLog(), options);
            Assert.Equal(GuardOutcome.Held, second.Outcome);

            Assert.Equal(NoticeRetry.HandedOver, InstanceNoticeViewModel.RetryOn(second));
            Assert.Equal(GuardOutcome.Held, second.Outcome);

            holder.Dispose();
            Assert.Equal(NoticeRetry.Free, InstanceNoticeViewModel.RetryOn(second));
            Assert.Equal(GuardOutcome.Taken, second.Outcome);
        }
        finally
        {
            holder.Dispose();
        }
    }

    /// <summary>A try that a test holds and answers, and that counts how often it was asked.</summary>
    private sealed class HeldTry
    {
        private readonly TaskCompletionSource<NoticeRetry> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public Task<NoticeRetry> Run()
        {
            Interlocked.Increment(ref _asked);
            return _answer.Task;
        }

        public void Answer(NoticeRetry answer) => _answer.SetResult(answer);
    }
}
