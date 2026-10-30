using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VisualCommit.App.Controls;
using Avalonia.Media.TextFormatting;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

public partial class RightPanelView : UserControl
{
    /// <summary>
    /// The part of the details area that the subject, body and rows may take at most before they
    /// scroll: the file list keeps the rest, so a long message cannot push it out of sight.
    /// </summary>
    private const double HeaderShare = 0.6;

    /// <summary>The header part may always be this tall, however small the panel, so that the subject stays readable.</summary>
    private const double MinimumHeaderHeight = 120;

    /// <summary>The authors' images decoded so far, by file, for the view's life; null for one that did not decode.</summary>
    private readonly Dictionary<string, Bitmap?> _avatars = new(StringComparer.Ordinal);

    private CommitDetailsViewModel? _details;

    /// <summary>Counts the author images asked for: a decode that a newer one overtook shows nothing.</summary>
    private int _avatarVersion;

    public RightPanelView()
    {
        InitializeComponent();
        DetailsContent.SizeChanged += (_, e) =>
            DetailsHeaderScroller.MaxHeight = Math.Max(MinimumHeaderHeight, e.NewSize.Height * HeaderShare);

        DetailsSha.SizeChanged += (_, _) => FitShaOnOneLine();
        DetailsSha.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                FitShaOnOneLine();
            }
        };

        // A commit's changed file has a menu of its own (docs/phase-3-design.md 4b): open file,
        // show in folder, copy path. It opens the menu only; the file's diff opens on a click.
        FileList.AddHandler(ContextRequestedEvent, OnFileContextRequested);

        // The author's avatar (docs/phase-7a-design.md 11.3) follows the shown commit's author.
        DataContextChanged += (_, _) => FollowDetails();
    }

    private void FollowDetails()
    {
        if (_details is not null)
        {
            _details.PropertyChanged -= OnDetailsPropertyChanged;
        }

        _details = DataContext as CommitDetailsViewModel;
        if (_details is not null)
        {
            _details.PropertyChanged += OnDetailsPropertyChanged;
        }

        ShowAvatar(_details?.AuthorAvatarFile);
    }

    private void OnDetailsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommitDetailsViewModel.AuthorAvatarFile) && ReferenceEquals(sender, _details))
        {
            ShowAvatar(_details?.AuthorAvatarFile);
        }
    }

    /// <summary>
    /// Shows <paramref name="file"/>'s image in the round <c>DetailsAuthorAvatar</c> before the author:
    /// none hides it at once; a file is decoded off the UI thread (D59) and, unless a newer one came
    /// meanwhile, fills the ellipse, which shows. Until then the ellipse keeps what it showed. A file
    /// that does not decode hides it: that author looks as without an avatar (D310).
    /// </summary>
    private async void ShowAvatar(string? file)
    {
        var version = ++_avatarVersion;
        if (file is null)
        {
            HideAvatar();
            return;
        }

        if (!_avatars.TryGetValue(file, out var bitmap))
        {
            bitmap = await AvatarBitmaps.DecodeAsync(file, CancellationToken.None);
            _avatars.TryAdd(file, bitmap);
        }

        if (version != _avatarVersion)
        {
            return;
        }

        if (bitmap is null)
        {
            HideAvatar();
            return;
        }

        DetailsAuthorAvatar.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        DetailsAuthorAvatar.IsVisible = true;
    }

    private void HideAvatar()
    {
        DetailsAuthorAvatar.IsVisible = false;
        DetailsAuthorAvatar.Fill = null;
    }

    private void OnFileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled
            || ContextMenuRequests.Find<Button>(e.Source, button => button.Classes.Contains("fileRow")) is not { DataContext: ChangedFileRow { File: { } file } } row
            || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository)
            || !ReferenceEquals(repository.Details, DataContext))
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, row);
        e.Handled = repository.OpenFileMenu(layer, file, FileMenuPlace.CommitFiles, position, openedBy);
    }

    /// <summary>
    /// Keeps the SHA's 40 characters on one line. A SHA that fits its column at the panel's font
    /// size is drawn at that size, as before. One that would wrap (at the default width of 400 a
    /// SHA of wide digits does once the "Commit date" row widens the label column) is drawn just
    /// small enough to fit: a few hundredths of a point for most, never cut.
    /// </summary>
    private void FitShaOnOneLine()
    {
        var sha = DetailsSha;
        var room = sha.Bounds.Width - sha.Padding.Left - sha.Padding.Right;
        var size = sha.Parent?.GetValue(TextElement.FontSizeProperty) ?? sha.FontSize;
        if (string.IsNullOrEmpty(sha.Text) || room <= 0 || FitsOnOneLine(sha, size, room))
        {
            sha.ClearValue(TextElement.FontSizeProperty);
            return;
        }

        // Text grows in proportion to its size; the steps down make up for rounding.
        var smaller = Math.Floor(size * room / OneLineWidth(sha, size) * 100) / 100;
        while (smaller > 1 && !FitsOnOneLine(sha, smaller, room))
        {
            smaller -= 0.01;
        }

        sha.FontSize = smaller;
    }

    private static bool FitsOnOneLine(TextBlock text, double size, double room)
    {
        using var layout = new TextLayout(text.Text, Typeface(text), size, null, textWrapping: TextWrapping.Wrap, maxWidth: room);
        return layout.TextLines.Count <= 1;
    }

    private static double OneLineWidth(TextBlock text, double size)
    {
        using var layout = new TextLayout(text.Text, Typeface(text), size, null);
        return layout.WidthIncludingTrailingWhitespace;
    }

    private static Typeface Typeface(TextBlock text) => new(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
}
