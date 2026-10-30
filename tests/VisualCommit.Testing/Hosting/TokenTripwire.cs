using System.Globalization;
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
    /// recorded standard input, output and error) for the text forms. A find fails naming the file
    /// or the git call (<see cref="NameOf"/>) and the form, never the token. A file is read sharing
    /// read, write and delete, so one another process
    /// holds open for writing is read too; one that still cannot be read after
    /// <see cref="ReadAttempts"/> tries, 100 ms apart, fails as unreadable, since it may hold the
    /// token. A file deleted before it is read holds nothing any more and is passed.
    /// </summary>
    /// <exception cref="TokenTripwireException">The token was found, or a file could not be read.</exception>
    public static void AssertAbsent(string token, IEnumerable<string> folders, IGitCallLog? calls)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(folders);
        var forms = FormsOf(token);
        var finds = new List<string>();
        var unreadable = new List<string>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true }))
            {
                var (bytes, failure) = Read(file);
                if (failure is not null)
                {
                    unreadable.Add($"{file} ({failure})");
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

        var textForms = TextFormsOf(token);
        foreach (var record in calls?.Snapshot() ?? [])
        {
            var call = NameOf(record, textForms);
            foreach (var (form, text) in textForms)
            {
                foreach (var (part, value) in new[] { ("command", record.CommandText), ("input", record.StandardInput ?? string.Empty), ("output", record.StandardOutput), ("error", record.StandardError) })
                {
                    if (value.Contains(text, StringComparison.Ordinal))
                    {
                        finds.Add($"the {part} of the git call {call} ({form})");
                    }
                }
            }
        }

        if (finds.Count > 0 || unreadable.Count > 0)
        {
            var found = finds.Count > 0 ? $"The token was found in {finds.Count} places:\n" + string.Join('\n', finds) : null;
            var unread = unreadable.Count > 0 ? "Files not read, so the token's absence is not proved there:\n" + string.Join('\n', unreadable) : null;
            throw new TokenTripwireException(string.Join('\n', new[] { found, unread }.OfType<string>()));
        }
    }

    /// <summary>
    /// How a find names its git call: the command as recorded, or, when the command holds one of
    /// <paramref name="forms"/>, its words before the first that holds one, then "…" and the call's
    /// sequence number, so that the message never holds the token.
    /// </summary>
    private static string NameOf(GitCallRecord record, IReadOnlyList<(string Form, string Text)> forms)
    {
        if (!forms.Any(form => record.CommandText.Contains(form.Text, StringComparison.Ordinal)))
        {
            return record.CommandText;
        }

        var words = record.CommandText.Split(' ');
        var first = Array.FindIndex(words, word => forms.Any(form => word.Contains(form.Text, StringComparison.Ordinal)));
        var before = first > 0 ? string.Join(' ', words[..first]) + " " : string.Empty;
        return $"{before}… (number {record.Sequence.ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>How many times a file is tried before it fails as unreadable.</summary>
    public const int ReadAttempts = 5;

    /// <summary>
    /// A file's bytes, read sharing read, write and delete; empty when it was deleted first. Else,
    /// after <see cref="ReadAttempts"/> tries, the last failure's type and message.
    /// </summary>
    private static (byte[] Bytes, string? Failure) Read(string file)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return (memory.ToArray(), null);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return ([], null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == ReadAttempts)
                {
                    return ([], $"{exception.GetType().Name}: {exception.Message}");
                }
            }

            Thread.Sleep(100);
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

/// <summary>A token was found where it must never be, or a file could not be read to prove it absent (<see cref="TokenTripwire.AssertAbsent"/>). Its message never holds the token.</summary>
public sealed class TokenTripwireException(string message) : Exception(message);
