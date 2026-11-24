using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The leak guard's rules (docs/phase-8a-design.md 11.3.5, 11.4; ruling 63, D451), each caught in a
/// made-up text, and the demo's texts and About's link to the app's repository left clean. The
/// machine's facts are made up too, so that no rule depends on this machine.
/// </summary>
public sealed class LeakGuardTests
{
    private static readonly LeakFacts Facts = new(
        UserName: "quill",
        MachineName: "BUILD-BOX-7",
        TempPath: "/var/tmp/run-41/",
        HomeFolder: "/Users/ann.example",
        RepositoryPath: @"D:\checkouts\vc\");

    public static TheoryData<string, string> Leaks => new()
    {
        { "Signed in to the computer as QUILL", "the user's name" },
        { "Built on build-box-7", "the machine's name" },
        { "/var/tmp/run-41/data/settings.json", "the temp folder" },
        { @"\var\tmp\run-41\data", "the temp folder" },
        { "/Users/ann.example/Projects/weather-station", "the home folder" },
        { @"D:\checkouts\vc\README.md", "the repository's path" },
        { "D:/checkouts/vc/docs", "the repository's path" },
        { @"C:\Temp\VisualCommit.Tests\shared\repo", "the tests' folder" },
        { "Test Author <author@example.com>", "the tests' author" },
        { "Kim Sample <kim@mail.example.org>", "an e-mail address not at example.com" },
        { "https://github.com/someone/weather-station.git", "a github.com address of another owner than demo--alex" },
        { "github.com/RahimPasha/VisualCommit-fork", "a github.com address of another owner than demo--alex" },
    };

    [Theory]
    [MemberData(nameof(Leaks))]
    public void A_text_that_shows_the_machine_is_caught(string text, string rule)
    {
        var findings = LeakGuard.Findings([text], Facts);
        Assert.Contains($"{rule}: \"{text}\"", findings);
    }

    [Fact]
    public void The_demos_texts_and_the_apps_own_repository_are_clean()
    {
        string[] texts =
        [
            "Alex Demo <alex@example.com>",
            "Sam Example",
            "https://github.com/demo--alex/weather-station.git",
            "github.com/demo--alex/recipe-box",
            "github.com/RahimPasha/VisualCommit",
            "https://github.com/RahimPasha/VisualCommit",
            "/home/demo/Projects/weather-station",
            @"C:\demo\Projects\weather-station",
            "On main: Try a larger font",
            "Git 2.43.0",
        ];

        Assert.Empty(LeakGuard.Findings(texts, Facts));
    }

    [Fact]
    public void A_name_shorter_than_three_characters_is_not_looked_for()
    {
        var facts = Facts with { UserName = "al", MachineName = "pc" };
        Assert.Empty(LeakGuard.Findings(["always", "a pc of its own"], facts));
    }

    [Fact]
    public void Each_finding_names_its_rule_and_its_text()
    {
        var findings = LeakGuard.Findings(["fine", "quill <quill@mail.example.net>"], Facts);
        Assert.Equal(
            [
                "the user's name: \"quill <quill@mail.example.net>\"",
                "an e-mail address not at example.com: \"quill <quill@mail.example.net>\"",
            ],
            findings);
    }
}
