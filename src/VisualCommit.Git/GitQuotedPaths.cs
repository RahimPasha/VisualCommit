using System.Text;
using VisualCommit.Core.Diff;

namespace VisualCommit.Git;

/// <summary>
/// Paths as git writes them outside the <c>-z</c> forms (<c>blame --porcelain</c>'s
/// <c>filename</c> and <c>previous</c>, <c>log --name-status</c>): C-quoted when they hold a quote,
/// a backslash, a control character or (with <c>core.quotePath</c>, git's default) a byte above
/// 127, and as they are otherwise.
/// </summary>
internal static class GitQuotedPaths
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A path as git wrote it, its quoting undone, then its bytes read as UTF-8.</summary>
    /// <param name="latin1">The path read as Latin-1, one character per byte; plain ASCII, which a path under <c>core.quotePath</c> is, reads the same in UTF-8.</param>
    public static string Unquote(string latin1)
    {
        if (latin1.Length < 2 || latin1[0] != '"' || latin1[^1] != '"')
        {
            return DiffParser.DecodeUtf8(latin1);
        }

        var end = latin1.Length - 1;
        var bytes = new List<byte>(latin1.Length);
        for (var i = 1; i < end; i++)
        {
            var c = latin1[i];
            if (c != '\\' || i + 1 >= end)
            {
                bytes.Add((byte)c);
                continue;
            }

            var escaped = latin1[++i];
            if (escaped is >= '0' and <= '3' && i + 2 < end && IsOctal(latin1[i + 1]) && IsOctal(latin1[i + 2]))
            {
                // A byte in three octal digits: git writes every byte above 127 so under core.quotePath.
                bytes.Add((byte)(((escaped - '0') << 6) | ((latin1[i + 1] - '0') << 3) | (latin1[i + 2] - '0')));
                i += 2;
                continue;
            }

            bytes.Add(escaped switch
            {
                'a' => 7,
                'b' => 8,
                't' => 9,
                'n' => 10,
                'v' => 11,
                'f' => 12,
                'r' => 13,

                // \\ and \" stand for themselves, as does anything git does not write.
                _ => (byte)escaped,
            });
        }

        return Utf8.GetString([.. bytes]);

        static bool IsOctal(char c) => c is >= '0' and <= '7';
    }
}
