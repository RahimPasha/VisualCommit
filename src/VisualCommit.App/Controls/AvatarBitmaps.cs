using Avalonia.Media.Imaging;

namespace VisualCommit.App.Controls;

/// <summary>
/// Decodes the authors' avatars (docs/phase-7a-design.md 11.3, D310): a local PNG or JPEG that the
/// hosting fetched into the data folder's <c>avatars/</c>, read and decoded on the thread pool, never
/// on the UI thread (D59), to 32 pixels wide, twice the 16 it is drawn at.
/// </summary>
public static class AvatarBitmaps
{
    /// <summary>The width an avatar is decoded to: twice its drawn size, sharp at 200 percent.</summary>
    public const int DecodeWidth = 32;

    /// <summary>
    /// The image in <paramref name="file"/> decoded to <see cref="DecodeWidth"/> pixels wide, on the
    /// thread pool; null for a file that is missing, unreadable or no image. A failure is not logged
    /// (D310): that author looks as without an avatar.
    /// </summary>
    public static Task<Bitmap?> DecodeAsync(string file, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        return Task.Run(() => Decode(file), ct);
    }

    private static Bitmap? Decode(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
#pragma warning disable CA1031 // Whatever the file holds, the author is shown without an image (11.3).
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
