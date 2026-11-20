namespace VisualCommit.App.Services;

/// <summary>
/// The app's links to the web: its repository and the pages of its user guide. Every link opens
/// through <see cref="IFileLauncher.OpenUrlAsync"/>.
/// </summary>
public static class AppLinks
{
    /// <summary>The user guide's address: GitHub Pages of the repository's owner, under the repository's name.</summary>
    public const string GuideBase = "https://rahimpasha.github.io/VisualCommit/";

    /// <summary>The app's repository on GitHub.</summary>
    public static Uri Repository { get; } = new("https://github.com/RahimPasha/VisualCommit");

    /// <summary>The address of <paramref name="page"/> of the user guide: <see cref="GuideBase"/> and the page's path, with no query string.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is no page of the guide.</exception>
    public static Uri Guide(GuidePage page) => new(GuideBase + PathOf(page));

    /// <summary>A page's path under <see cref="GuideBase"/>, its anchor included.</summary>
    private static string PathOf(GuidePage page) => page switch
    {
        GuidePage.Start => string.Empty,
        GuidePage.FirstRun => "first-run.html",
        GuidePage.GitMissing => "troubleshooting/git.html#git-missing",
        GuidePage.GitChosenMissing => "troubleshooting/git.html#git-chosen-missing",
        GuidePage.GitTooOld => "troubleshooting/git.html#git-too-old",
        GuidePage.GitNotWorking => "troubleshooting/git.html#git-not-working",
        GuidePage.HelperMissing => "troubleshooting/git.html#helper-missing",
        GuidePage.Shortcuts => "reference/shortcuts.html",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, "No page of the user guide."),
    };
}

/// <summary>The pages of the user guide the app links to.</summary>
public enum GuidePage
{
    /// <summary>The guide's start page.</summary>
    Start,

    /// <summary>The first start: finding git.</summary>
    FirstRun,

    /// <summary>Git is not found.</summary>
    GitMissing,

    /// <summary>The git chosen in Settings is gone.</summary>
    GitChosenMissing,

    /// <summary>Git is too old.</summary>
    GitTooOld,

    /// <summary>Git does not work.</summary>
    GitNotWorking,

    /// <summary>The app's helper for git's questions is missing.</summary>
    HelperMissing,

    /// <summary>The keyboard shortcuts.</summary>
    Shortcuts,
}
