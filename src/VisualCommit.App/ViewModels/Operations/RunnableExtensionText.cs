using System.Text.RegularExpressions;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The extensions Open file also asks about (docs/phase-6-design.md 7.3, 12.8), as the settings
/// screen's <c>RunnableExtraBox</c> and <c>settings.json</c>'s <c>extraRunnableExtensions</c> hold
/// them: one reading for the screen, which checks and stores them, and for Open file, which reads
/// the setting at each call, a hand-edited one included. Entries are split at spaces, commas,
/// semicolons and tabs; an entry is an extension when it matches
/// <c>^\.?[A-Za-z0-9_+~-]{1,16}$</c>, and is taken lower-cased with its dot.
/// </summary>
public static class RunnableExtensionText
{
    private static readonly Regex ExtensionPattern = new(@"^\.?[A-Za-z0-9_+~-]{1,16}$", RegexOptions.CultureInvariant);

    private static readonly char[] Separators = [' ', ',', ';', '\t'];

    /// <summary>The entries of <paramref name="text"/> as typed, in order; none for null.</summary>
    public static IReadOnlyList<string> Entries(string? text) =>
        (text ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The first entry of <paramref name="text"/> that is not an extension, as typed; null when all are.</summary>
    public static string? FirstInvalid(string? text) => Entries(text).FirstOrDefault(entry => !ExtensionPattern.IsMatch(entry));

    /// <summary>
    /// Each entry of <paramref name="text"/> that is an extension, lower-cased with its dot, in
    /// order and without duplicates; an entry that is not one is left out. <c>py,pl</c> and
    /// <c>.PY .pl</c> both give <c>.py</c>, <c>.pl</c>.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? text)
    {
        var extensions = new List<string>();
        foreach (var entry in Entries(text))
        {
            if (!ExtensionPattern.IsMatch(entry))
            {
                continue;
            }

            var extension = (entry.StartsWith('.') ? entry : "." + entry).ToLowerInvariant();
            if (!extensions.Contains(extension))
            {
                extensions.Add(extension);
            }
        }

        return extensions;
    }
}
