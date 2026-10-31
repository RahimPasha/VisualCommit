using Avalonia;
using VisualCommit.App.Views.Graph;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VisualCommit.Hosting;

namespace VisualCommit.App.Controls;

/// <summary>
/// The authors' avatars in the Author column (docs/phase-7a-design.md 11.3; D252, D310): an author
/// GitHub knows has a 16-px round image before the name, decoded off the UI thread; every other
/// author, and every row while no image is decoded, looks as before.
/// </summary>
public sealed partial class CommitGraphControl
{
    /// <summary>The avatar's drawn size, square, clipped to a circle.</summary>
    public const double AvatarSize = 16;

    /// <summary>The avatar's top below its row's top.</summary>
    public const double AvatarTop = 5;

    /// <summary>Space between the avatar and the author's name.</summary>
    public const double AvatarGap = 6;

    /// <summary>The local images of the authors GitHub knows, by lower-case e-mail: the repository's <c>Avatars</c>.</summary>
    public static readonly StyledProperty<AuthorAvatars> AvatarsProperty =
        AvaloniaProperty.Register<CommitGraphControl, AuthorAvatars>(nameof(Avatars), AuthorAvatars.None);

    private static readonly IReadOnlyDictionary<string, Bitmap> NoImages = new Dictionary<string, Bitmap>(StringComparer.Ordinal);

    /// <summary>
    /// The decoded images by file, for the control's life (a few dozen 32-px images). A file that did
    /// not decode is not kept: the next change decodes it again, since it may have been written since.
    /// </summary>
    private readonly Dictionary<string, Bitmap> _decoded = new(StringComparer.Ordinal);

    /// <summary>The images drawn, by lower-case e-mail: those of <see cref="Avatars"/> that decoded, once all of them have.</summary>
    private IReadOnlyDictionary<string, Bitmap> _images = NoImages;

    /// <summary>Counts the changes of <see cref="Avatars"/>: a decode that a newer change overtook applies nothing.</summary>
    private int _avatarsVersion;

    /// <summary>
    /// Raised on the UI thread once a change of <see cref="Avatars"/> is applied, its images decoded,
    /// with the number of authors whose image decoded, whether the Author column shows or not
    /// (the report's open point 42); a change to none gives 0. The repository's view logs it (D322).
    /// </summary>
    public event EventHandler<int>? AvatarsApplied;

    /// <summary>The authors' avatars (11.3); <see cref="AuthorAvatars.None"/> without hosting.</summary>
    public AuthorAvatars Avatars
    {
        get => GetValue(AvatarsProperty);
        set => SetValue(AvatarsProperty, value);
    }

    /// <summary>
    /// For tests (11.4: true after decoding): <paramref name="email"/>'s rows (case ignored) have an
    /// image, decoded and applied, which they draw wherever the Author column shows; with the column
    /// hidden none is drawn, which only the pixels tell.
    /// </summary>
    public bool ShowsAvatarFor(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return _images.ContainsKey(email.ToLowerInvariant());
    }

    /// <summary>
    /// A new <see cref="Avatars"/>: the files not decoded yet are decoded on the thread pool, all
    /// together; then, unless a newer change came meanwhile, the rows are laid out again with the
    /// images and <see cref="AvatarsApplied"/> is raised. Until then the rows draw as they did.
    /// </summary>
    private async void OnAvatarsChanged(AuthorAvatars? avatars)
    {
        var version = ++_avatarsVersion;
        var byEmail = (avatars ?? AuthorAvatars.None).FileByEmail;
        var files = byEmail.Values.Distinct(StringComparer.Ordinal).Where(file => !_decoded.ContainsKey(file)).ToList();
        if (files.Count > 0)
        {
            // DecodeAsync never throws: a file that does not decode is null. An overlapping change
            // may have decoded the same file first; its image is kept and this one disposed.
            var bitmaps = await Task.WhenAll(files.Select(file => AvatarBitmaps.DecodeAsync(file, CancellationToken.None)));
            for (var i = 0; i < files.Count; i++)
            {
                if (bitmaps[i] is { } bitmap && !_decoded.TryAdd(files[i], bitmap))
                {
                    bitmap.Dispose();
                }
            }
        }

        if (version != _avatarsVersion)
        {
            return;
        }

        var images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        foreach (var (email, file) in byEmail)
        {
            if (_decoded.TryGetValue(file, out var bitmap))
            {
                images[email.ToLowerInvariant()] = bitmap;
            }
        }

        _images = images.Count > 0 ? images : NoImages;
        _rowCache.Clear();
        _authorCache.Clear();
        InvalidateVisual();
        UpdateHover();
        AvatarsApplied?.Invoke(this, images.Count);
    }

    /// <summary>The image drawn for an author's rows, or null: none, or the Author column hidden.</summary>
    private Bitmap? AvatarOf(string email, GraphColumn author) =>
        author.IsVisible && _images.Count > 0 && _images.TryGetValue(email.ToLowerInvariant(), out var bitmap) ? bitmap : null;

    /// <summary>Draws a row's avatar at its <see cref="AvatarPlace"/>, clipped to a circle, smoothly scaled.</summary>
    private static void DrawAvatar(DrawingContext context, Bitmap image, double columnX, double top)
    {
        var place = AvatarPlace(columnX, top);
        using (context.PushGeometryClip(new EllipseGeometry(place)))
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            context.DrawImage(image, place);
        }
    }

    /// <summary>Where a row's avatar is drawn: 16×16 at (Author's x + 8, the row's top + 5).</summary>
    private static Rect AvatarPlace(double columnX, double top) => new(columnX + GraphColumns.TextInset, top + AvatarTop, AvatarSize, AvatarSize);

    /// <summary>Where the Author cell's text starts, as <see cref="DrawCell"/> takes it: at the column's x, beside an avatar 16 + 6 further (11.3).</summary>
    private static double AuthorX(GraphColumn author, bool withAvatar) => withAvatar ? author.X + AvatarSize + AvatarGap : author.X;

    /// <summary>
    /// For tests (11.3): where row <paramref name="index"/>'s author is drawn, in the control's
    /// coordinates, from the places the drawing uses: its avatar (null without one) and its name's
    /// laid-out text; null while the Author column is hidden, and for a row that is not there.
    /// </summary>
    public PlacedAuthor? AuthorInRow(int index)
    {
        var data = Data;
        if (data is null || index < 0 || index >= data.Count || GetRowVisual(data, index) is not { Author: { } name } visual)
        {
            return null;
        }

        var (column, top) = (Columns.Author, RowBounds(index).Y);
        return new PlacedAuthor(
            visual.Avatar is null ? null : AvatarPlace(column.X, top),
            new Rect(CellOrigin(name, AuthorX(column, visual.Avatar is not null), top), new Size(name.WidthIncludingTrailingWhitespace, name.Height)));
    }
}

/// <summary>Where a row draws its author (<see cref="CommitGraphControl.AuthorInRow"/>), in the control's coordinates.</summary>
/// <param name="Avatar">The avatar, <see cref="CommitGraphControl.AvatarSize"/> square and clipped to a circle; null for a row without one.</param>
/// <param name="Name">The name's laid-out text, its height centred in the row.</param>
public sealed record PlacedAuthor(Rect? Avatar, Rect Name);
