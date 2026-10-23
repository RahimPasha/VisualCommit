using System.Text;

namespace VisualCommit.Core;

/// <summary>
/// How the app saves its own files (D46: <c>settings.json</c>, <c>session.json</c>,
/// <c>workspaces.json</c>, <c>merge-tools.json</c>): the text is written beside the file, flushed
/// to the disk, and only then swapped in, so that a crash or a power cut leaves the old file or
/// the new one, never an empty or half-written one. A rename can reach the disk before the data
/// it names unless the data was flushed first (on NTFS, ext4 and APFS alike), which
/// <see cref="File.WriteAllText(string, string?)"/> does not do.
/// </summary>
public static class DurableFile
{
    /// <summary>The text's encoding: UTF-8 without a byte order mark, as <see cref="File.WriteAllText(string, string?)"/> writes.</summary>
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The temporary file beside <paramref name="path"/> that <see cref="Replace"/> writes first.</summary>
    public static string TemporaryOf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path + ".tmp";
    }

    /// <summary>
    /// Writes <paramref name="contents"/> to <paramref name="path"/>, replacing what is there, and
    /// flushes it to the disk before returning. Throws as <see cref="File.WriteAllText(string, string?)"/> does.
    /// <paramref name="open"/> opens the file for writing (tests give a stream that records the flush);
    /// null opens it as <see cref="File.WriteAllText(string, string?)"/> does.
    /// </summary>
    public static void Write(string path, string contents, Func<string, FileStream>? open = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        using var stream = (open ?? Open)(path);
        var bytes = Utf8.GetBytes(contents);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Writes <paramref name="contents"/> to <see cref="TemporaryOf"/>(<paramref name="path"/>),
    /// flushed to the disk (<see cref="Write"/>), then moves it over <paramref name="path"/>. Throws
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when either step fails;
    /// the file at <paramref name="path"/> is then as it was.
    /// </summary>
    public static void Replace(string path, string contents, Func<string, FileStream>? open = null)
    {
        var temporary = TemporaryOf(path);
        Write(temporary, contents, open);
        File.Move(temporary, path, overwrite: true);
    }

    private static FileStream Open(string path) => new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
}
