using System.Text;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The token's tripwire (docs/phase-7a-design.md 1, 2.5; ruling 7): a check proves that a token
/// reached no file and no git call's record, in any form git, a URL, a keychain or a redaction
/// that failed could have written it.
/// </summary>
public static class TokenTripwire
{
    /// <summary>
    /// Searches every file under <paramref name="folders"/> (<c>.git</c> included) for
    /// <paramref name="token"/> as UTF-8, UTF-16LE, <see cref="Uri.EscapeDataString"/>, base64 of
    /// <c>x-access-token:&lt;token&gt;</c> (git's Basic header) and base64 of the token's UTF-8
    /// (the macOS store's form, 3.5), and each record of <paramref name="calls"/> (its command,
    /// output and error) for the text forms. A find fails naming the file or the command and the
    /// form, never the token.
    /// </summary>
    /// <exception cref="TokenTripwireException">The token was found.</exception>
    public static void AssertAbsent(string token, IEnumerable<string> folders, IGitCallLog? calls)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(folders);
        var forms = FormsOf(token);
        var finds = new List<string>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true }))
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(file);
                }
                catch (IOException)
                {
                    // A file another process holds open (a lock file) is not read.
                    continue;
                }

                foreach (var (form, pattern) in forms)
                {
                    if (bytes.AsSpan().IndexOf(pattern) >= 0)
                    {
                        finds.Add($"{file} ({form})");
                    }
                }
            }
        }

        foreach (var record in calls?.Snapshot() ?? [])
        {
            foreach (var (form, text) in TextFormsOf(token))
            {
                foreach (var (part, value) in new[] { ("command", record.CommandText), ("output", record.StandardOutput), ("error", record.StandardError) })
                {
                    if (value.Contains(text, StringComparison.Ordinal))
                    {
                        finds.Add($"the {part} of the git call {record.CommandText} ({form})");
                    }
                }
            }
        }

        if (finds.Count > 0)
        {
            throw new TokenTripwireException($"The token was found in {finds.Count} places:\n" + string.Join('\n', finds));
        }
    }

    /// <summary>The forms searched in a file: each as bytes.</summary>
    private static IReadOnlyList<(string Form, byte[] Pattern)> FormsOf(string token) =>
    [
        .. TextFormsOf(token).Select(form => (form.Form, Encoding.UTF8.GetBytes(form.Text))),
        ("UTF-16LE", Encoding.Unicode.GetBytes(token)),
    ];

    /// <summary>The forms searched in a call's text.</summary>
    private static IReadOnlyList<(string Form, string Text)> TextFormsOf(string token)
    {
        var forms = new List<(string Form, string Text)>
        {
            ("UTF-8", token),
            ("base64 of x-access-token:<token>", Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token))),
            ("base64 of the token", Convert.ToBase64String(Encoding.UTF8.GetBytes(token))),
        };

        var escaped = Uri.EscapeDataString(token);
        if (escaped != token)
        {
            forms.Add(("URL-escaped", escaped));
        }

        return forms;
    }
}

/// <summary>A token was found where it must never be (<see cref="TokenTripwire.AssertAbsent"/>). Its message never holds the token.</summary>
public sealed class TokenTripwireException(string message) : Exception(message);
