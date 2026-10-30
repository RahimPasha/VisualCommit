using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The details panel's avatar (docs/phase-7a-design.md 11.3, 11.4; D310): the view model's file
/// follows the shown commit's author (case ignored), a later change of the avatars and a cleared
/// panel; the view without a file lays the author out as before, and with one draws the round
/// image 16×16 at the value column's left and the author 22 further.
/// </summary>
public sealed class CommitDetailsAvatarTests : IAsyncLifetime
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The tests need git."));

    private readonly TempDirectory _data = new("phase7a details avatar");
    private TempRepo? _repo;
    private string _octo = string.Empty;
    private string _author = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _repo = await TempRepo.CreateAsync("details");
        _octo = await _repo.CommitFileAsAsync("a.txt", "1\n", "Update the readme", "Octo Cat", "Octo@Example.com");
        _author = await _repo.CommitFileAsync("b.txt", "2\n", "Add the docs folder");
    }

    public ValueTask DisposeAsync()
    {
        _repo?.Dispose();
        _data.Dispose();
        return ValueTask.CompletedTask;
    }

    private string Image(string name)
    {
        var path = System.IO.Path.Combine(_data.Path, name);
        File.WriteAllBytes(path, TestImages.SquareOn(64, 64, TestImages.Rgb.Parse("#2F81F7"), TestImages.Rgb.Parse("#FFFFFF"), 24));
        return path;
    }

    private async Task<CommitDetailsViewModel> DetailsAsync() =>
        new(await GitRepository.OpenAsync(Runner, _repo!.Path, TestContext.Current.CancellationToken), new JsonSettingsStore(System.IO.Path.Combine(_data.Path, "settings.json")), new DateDisplay(TimeZoneInfo.Utc));

    private static AuthorAvatars Of(params (string Email, string File)[] entries) =>
        new(entries.ToDictionary(entry => entry.Email, entry => entry.File, StringComparer.Ordinal));

    [Fact]
    public async Task The_file_follows_the_author_s_e_mail_a_later_change_and_a_cleared_panel()
    {
        var octo = Image("1001.png");
        var author = Image("9.png");
        var details = await DetailsAsync();
        var token = TestContext.Current.CancellationToken;
        Assert.Null(details.AuthorAvatarFile);

        details.Avatars = Of(("octo@example.com", octo));
        Assert.Null(details.AuthorAvatarFile);
        await details.ShowAsync(_octo, cancellationToken: token);
        Assert.Equal(octo, details.AuthorAvatarFile);
        await details.ShowAsync(_author, cancellationToken: token);
        Assert.Null(details.AuthorAvatarFile);

        // A later change finds the shown commit's author again.
        details.Avatars = Of(("octo@example.com", octo), ("author@example.com", author));
        Assert.Equal(author, details.AuthorAvatarFile);
        details.Avatars = AuthorAvatars.None;
        Assert.Null(details.AuthorAvatarFile);

        details.Avatars = Of(("octo@example.com", octo));
        await details.ShowAsync(_octo, cancellationToken: token);
        Assert.Equal(octo, details.AuthorAvatarFile);
        await details.ShowAsync(null, cancellationToken: token);
        Assert.Null(details.AuthorAvatarFile);
    }

    [AvaloniaFact]
    public async Task Without_a_file_the_author_is_laid_out_as_before_and_with_one_the_image_is_before_it()
    {
        var token = TestContext.Current.CancellationToken;
        var plain = await DetailsAsync();
        var other = await DetailsAsync();
        var octo = await DetailsAsync();
        var file = Image("1001.png");
        other.Avatars = Of(("ada@example.com", file));
        octo.Avatars = Of(("octo@example.com", file));
        using var plainHost = Show(plain);
        using var otherHost = Show(other);
        using var octoHost = Show(octo);
        foreach (var details in new[] { plain, other, octo })
        {
            await details.ShowAsync(_octo, cancellationToken: token);
        }

        await WaitAsync(() => octoHost.Avatar.IsVisible, "the decoded avatar");
        foreach (var host in new[] { plainHost, otherHost, octoHost })
        {
            Dispatcher.UIThread.RunJobs();
        }

        // No file: the ellipse collapsed and the author where it was.
        Assert.False(plainHost.Avatar.IsVisible);
        Assert.False(otherHost.Avatar.IsVisible);
        Assert.Equal(plainHost.BoundsOf(plainHost.Author), otherHost.BoundsOf(otherHost.Author));
        Assert.Equal(plainHost.BoundsOf(plainHost.Date).X, plainHost.BoundsOf(plainHost.Author).X);

        // A file: the ellipse 16×16 at the value column's left, its top the author's, the text 22 further.
        var date = octoHost.BoundsOf(octoHost.Date);
        var avatar = octoHost.BoundsOf(octoHost.Avatar);
        var text = octoHost.BoundsOf(octoHost.Author);
        Assert.Equal((date.X, 16d, 16d), (avatar.X, avatar.Width, avatar.Height));
        Assert.Equal(text.Y, avatar.Y);
        Assert.Equal(date.X + 22, text.X);
        Assert.IsType<ImageBrush>(octoHost.Avatar.Fill);
        Assert.Equal("Author's avatar", Avalonia.Automation.AutomationProperties.GetName(octoHost.Avatar));

        // Back to a commit without one: hidden at once, the author where it was.
        await octo.ShowAsync(_author, cancellationToken: token);
        Dispatcher.UIThread.RunJobs();
        Assert.False(octoHost.Avatar.IsVisible);
        // (Its label column is narrower: the commit has no Committer row.)
        Assert.Equal(octoHost.BoundsOf(octoHost.Date).X, octoHost.BoundsOf(octoHost.Author).X);
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"Timed out waiting for {what}.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static PanelHost Show(CommitDetailsViewModel details)
    {
        var window = new Window { Width = 400, Height = 700, Content = new RightPanelView { DataContext = details }, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new PanelHost(window);
    }

    private sealed class PanelHost(Window window) : IDisposable
    {
        public Ellipse Avatar => Find<Ellipse>("DetailsAuthorAvatar");

        public Control Author => Find<Control>("DetailsAuthor");

        public Control Date => Find<Control>("DetailsDate");

        public Rect BoundsOf(Visual visual)
        {
            Dispatcher.UIThread.RunJobs();
            return new Rect(visual.TranslatePoint(default, window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);
        }

        public void Dispose() => window.Close();

        private T Find<T>(string name)
            where T : Control =>
            ((RightPanelView)window.Content!).FindControl<T>(name) ?? throw new InvalidOperationException($"The panel has no {name}.");
    }
}
