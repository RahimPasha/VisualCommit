using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.Testing.Headless;

namespace VisualCommit.GuideShots;

/// <summary>
/// The machine's facts that no guide picture may show (<see cref="GuideSettings.Capture"/>).
/// </summary>
/// <param name="UserName">The user's name.</param>
/// <param name="MachineName">The machine's name.</param>
/// <param name="TempPath">The temp folder.</param>
/// <param name="HomeFolder">The user's real home folder.</param>
/// <param name="RepositoryPath">The repository's own folder.</param>
public sealed record LeakFacts(string? UserName, string? MachineName, string TempPath, string? HomeFolder, string RepositoryPath)
{
    /// <summary>No facts: before <see cref="GuideSettings.Capture"/>.</summary>
    public static LeakFacts None { get; } = new(null, null, string.Empty, null, string.Empty);
}

/// <summary>
/// Keeps the machine out of the guide's pictures (ruling 63, D451; docs/phase-8a-design.md
/// 11.3.5): a shot fails, saving nothing, when a visible text of the window (a text block's or a
/// text box's) holds the user's name (three characters or more), the machine's name (likewise),
/// the temp folder, the real home folder, the repository's path, <c>VisualCommit.Tests</c>,
/// <c>Test Author</c>, an e-mail address not at <c>example.com</c>, or a <c>github.com/</c> address
/// whose owner is not the demo's, <see cref="GuideScenario.Owner"/>, other than the app's own
/// repository, <c>github.com/RahimPasha/VisualCommit</c>, which About shows. It cannot read what a
/// control draws itself (the graph, the diff): that text is the demo's, and the gate looks.
/// </summary>
public static partial class LeakGuard
{
    /// <summary>The shortest user or machine name the guard looks for: a shorter one is found in ordinary words.</summary>
    public const int ShortestName = 3;

    /// <summary>The only e-mail domain a picture may show.</summary>
    public const string DemoDomain = "example.com";

    /// <summary>The app's own repository as About shows it, the one <c>github.com/</c> address of another owner a picture may show.</summary>
    public static string AppRepository { get; } = AppLinks.Repository.Host + AppLinks.Repository.AbsolutePath;

    /// <summary>
    /// Every finding over <paramref name="texts"/>, each "<c>{rule}: "{text}"</c>", in the texts'
    /// order; empty when the texts are clean.
    /// </summary>
    public static IReadOnlyList<string> Findings(IEnumerable<string> texts, LeakFacts facts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(facts);
        var findings = new List<string>();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (var rule in RulesBrokenBy(text, facts))
            {
                findings.Add($"{rule}: \"{text}\"");
            }
        }

        return findings;
    }

    /// <summary>
    /// Fails the shot <paramref name="shot"/> when the window's visible texts break a rule
    /// (<see cref="Findings"/>), naming the shot and each text.
    /// </summary>
    /// <exception cref="InvalidOperationException">A visible text shows something of the machine.</exception>
    public static void Check(Window window, string shot)
    {
        ArgumentNullException.ThrowIfNull(window);
        var findings = Findings(TextsIn(window), GuideSettings.Machine);
        if (findings.Count > 0)
        {
            throw new InvalidOperationException($"The shot {shot} would show what is not the demo's:\n" + string.Join('\n', findings));
        }
    }

    /// <summary>The window's visible texts: every text block's in reading order, then every visible text box's.</summary>
    public static IEnumerable<string> TextsIn(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        foreach (var text in VisibleTexts.In(window))
        {
            yield return text;
        }

        foreach (var box in window.GetVisualDescendants().OfType<TextBox>())
        {
            if (box.IsEffectivelyVisible && !string.IsNullOrEmpty(box.Text))
            {
                yield return box.Text;
            }
        }
    }

    private static IEnumerable<string> RulesBrokenBy(string text, LeakFacts facts)
    {
        if (facts.UserName is { Length: >= ShortestName } user && text.Contains(user, StringComparison.OrdinalIgnoreCase))
        {
            yield return "the user's name";
        }

        if (facts.MachineName is { Length: >= ShortestName } machine && text.Contains(machine, StringComparison.OrdinalIgnoreCase))
        {
            yield return "the machine's name";
        }

        if (HoldsPath(text, facts.TempPath))
        {
            yield return "the temp folder";
        }

        if (HoldsPath(text, facts.HomeFolder))
        {
            yield return "the home folder";
        }

        if (HoldsPath(text, facts.RepositoryPath))
        {
            yield return "the repository's path";
        }

        if (text.Contains("VisualCommit.Tests", StringComparison.OrdinalIgnoreCase))
        {
            yield return "the tests' folder";
        }

        if (text.Contains("Test Author", StringComparison.OrdinalIgnoreCase))
        {
            yield return "the tests' author";
        }

        foreach (Match address in EmailAddress().Matches(text))
        {
            if (!address.Groups["domain"].Value.Equals(DemoDomain, StringComparison.OrdinalIgnoreCase))
            {
                yield return "an e-mail address not at " + DemoDomain;
                break;
            }
        }

        foreach (Match address in GitHubAddress().Matches(text))
        {
            var owner = address.Groups["owner"].Value;
            if (owner.Equals(GuideScenario.Owner, StringComparison.OrdinalIgnoreCase) || IsAppRepository(text, address.Index))
            {
                continue;
            }

            yield return "a github.com address of another owner than " + GuideScenario.Owner;
            break;
        }
    }

    /// <summary>Whether <paramref name="text"/> holds <paramref name="path"/> in either kind of separator, ignoring case and a trailing separator.</summary>
    private static bool HoldsPath(string text, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.TrimEnd('\\', '/');
        if (trimmed.Length < ShortestName)
        {
            return false;
        }

        return text.Contains(trimmed.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            || text.Contains(trimmed.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the <c>github.com/</c> address at <paramref name="index"/> is the app's own
    /// repository: <see cref="AppRepository"/> not followed by more of a name (a letter, a digit,
    /// <c>-</c> or <c>_</c>), so that <c>VisualCommit-fork</c> is another.
    /// </summary>
    private static bool IsAppRepository(string text, int index)
    {
        if (string.Compare(text, index, AppRepository, 0, AppRepository.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var end = index + AppRepository.Length;
        return end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] is '-' or '_');
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@(?<domain>[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)+)")]
    private static partial Regex EmailAddress();

    [GeneratedRegex(@"github\.com/(?<owner>[A-Za-z0-9\-_.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubAddress();
}
