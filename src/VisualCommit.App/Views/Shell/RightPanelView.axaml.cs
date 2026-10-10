using Avalonia.Controls;

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

    public RightPanelView()
    {
        InitializeComponent();
        DetailsContent.SizeChanged += (_, e) =>
            DetailsHeaderScroller.MaxHeight = Math.Max(MinimumHeaderHeight, e.NewSize.Height * HeaderShare);
    }
}
