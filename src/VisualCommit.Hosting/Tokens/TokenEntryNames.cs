using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// The names a token's entry has in each platform's store (docs/phase-7a-design.md 3.3). Every
/// name starts with the application's name, never with <c>git:</c> or <c>gh:</c>, so the app never
/// touches an entry of Git Credential Manager or GitHub CLI. A key part that is empty or holds a
/// control character, <c>'</c>, <c>\</c> or white space is refused before any store call: such a
/// part would break the quoting of macOS' <c>security -i</c> line, or name another entry.
/// </summary>
public sealed class TokenEntryNames
{
    /// <summary>The application part of the app's own entries; real-store tests pass <c>VisualCommit-test:&lt;guid&gt;</c>.</summary>
    public const string Application = "VisualCommit";

    /// <summary>The Secret Service's <c>xdg:schema</c> attribute of the app's items.</summary>
    public const string SecretServiceSchema = "io.github.rahimpasha.VisualCommit.Token";

    // How a provider's id is shown in a label or a comment; 7b adds its own.
    private static readonly Dictionary<string, string> ProviderNames = new(StringComparer.Ordinal)
    {
        ["github"] = "GitHub",
    };

    private TokenEntryNames(TokenKey key, string application)
    {
        var provider = ProviderNames.GetValueOrDefault(key.Provider, key.Provider);
        Entry = $"{application}:{key.Provider}:{key.Host}:{key.Account}";
        WindowsUser = key.Account;
        WindowsComment = $"{provider} token for {key.Account}, kept by {application}";
        MacService = $"{application}:{key.Provider}:{key.Host}";
        MacAccount = key.Account;
        MacLabel = $"{application}: {provider} ({key.Account})";
        SecretServiceSearch =
        [
            new("application", application),
            new("provider", key.Provider),
            new("host", key.Host),
            new("account", key.Account),
        ];
        SecretServiceAttributes = [.. SecretServiceSearch, new("xdg:schema", SecretServiceSchema)];
        SecretServiceLabel = $"{application}: {provider} token for {key.Account}";
    }

    /// <summary>
    /// The entry's name, <c>VisualCommit:github:github.com:octocat</c>: Windows' target, and how
    /// the log names the entry for whoever removes it by hand.
    /// </summary>
    public string Entry { get; }

    /// <summary>Windows' target name: <see cref="Entry"/>.</summary>
    public string WindowsTarget => Entry;

    public string WindowsUser { get; }

    public string WindowsComment { get; }

    /// <summary>macOS' service (<c>-s</c>): <c>VisualCommit:github:github.com</c>.</summary>
    public string MacService { get; }

    /// <summary>macOS' account (<c>-a</c>).</summary>
    public string MacAccount { get; }

    /// <summary>macOS' label (<c>-l</c>).</summary>
    public string MacLabel { get; }

    /// <summary>The four attributes a Secret Service search names (without <c>xdg:schema</c>).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> SecretServiceSearch { get; }

    /// <summary>All five attributes of a Secret Service item the app creates.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> SecretServiceAttributes { get; }

    public string SecretServiceLabel { get; }

    /// <summary>The names of <paramref name="key"/>'s entry; <see cref="ArgumentException"/> for a refused key.</summary>
    public static TokenEntryNames For(TokenKey key, string application = Application) =>
        TryFor(key, application, out var names)
            ? names
            : throw new ArgumentException("A part of the token's key cannot be part of a keychain entry's name.", nameof(key));

    /// <summary>The names of <paramref name="key"/>'s entry, or false when a part of the key is refused.</summary>
    public static bool TryFor(TokenKey key, string application, [NotNullWhen(true)] out TokenEntryNames? names)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(application);
        names = null;
        if (!Accepts(application) || !Accepts(key.Provider) || !Accepts(key.Host) || !Accepts(key.Account)
            || application.StartsWith("git:", StringComparison.OrdinalIgnoreCase)
            || application.StartsWith("gh:", StringComparison.OrdinalIgnoreCase)
            || string.Equals(application, "git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(application, "gh", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        names = new TokenEntryNames(key, application);
        return true;
    }

    /// <summary>Whether a key part can be part of an entry's name.</summary>
    internal static bool Accepts(string part) =>
        !string.IsNullOrEmpty(part)
        && !part.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '\'' or '\\');

    /// <summary>
    /// How the log names <paramref name="key"/>: <c>{host}/{account}</c> (design 3.2). A key that
    /// <see cref="TryFor"/> refuses may hold a line feed or another control character, which would
    /// break the log's line or forge another: such a character, a line or paragraph separator, a
    /// format character and <c>\</c> are written as <c>\uXXXX</c>.
    /// </summary>
    internal static string ForLog(TokenKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return $"{Escaped(key.Host)}/{Escaped(key.Account)}";
    }

    private static string Escaped(string part)
    {
        if (!part.Any(NeedsEscape))
        {
            return part;
        }

        var text = new StringBuilder(part.Length + 16);
        foreach (var c in part)
        {
            if (NeedsEscape(c))
            {
                text.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}");
            }
            else
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }

    private static bool NeedsEscape(char c) =>
        char.IsControl(c)
        || c == '\\'
        || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Format;
}
