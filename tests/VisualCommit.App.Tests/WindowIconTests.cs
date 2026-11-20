using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The app's icon in its windows and resources (docs/phase-8a-design.md 7.2, 7.4; D407): the main
/// window and the single-instance notice carry the embedded <c>.ico</c> as their icon, and the three
/// files <see cref="AppIcon"/> names are resources of the app that load. No session; the windows are
/// made bare, as <c>PanelViewTests</c> and the notice's own checks make them.
/// </summary>
public sealed class WindowIconTests
{
    [AvaloniaFact]
    public void The_main_window_has_an_icon()
    {
        var window = new MainWindow();
        try
        {
            Assert.NotNull(window.Icon);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_single_instance_notice_has_an_icon()
    {
        var notice = new InstanceNoticeViewModel(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", () => Task.FromResult(NoticeRetry.NoAnswer));
        var window = new InstanceNoticeWindow { DataContext = notice };
        try
        {
            Assert.NotNull(window.Icon);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_icon_s_three_resources_exist_and_load()
    {
        foreach (var uri in new[] { AppIcon.WindowIconUri, AppIcon.Image48Uri, AppIcon.Image96Uri })
        {
            Assert.True(AssetLoader.Exists(new Uri(uri)), $"{uri} is no resource of the app.");
        }

        using (var image48 = new Bitmap(AssetLoader.Open(new Uri(AppIcon.Image48Uri))))
        {
            Assert.Equal(new PixelSize(48, 48), image48.PixelSize);
        }

        using (var image96 = new Bitmap(AssetLoader.Open(new Uri(AppIcon.Image96Uri))))
        {
            Assert.Equal(new PixelSize(96, 96), image96.PixelSize);
        }

        using var ico = AssetLoader.Open(new Uri(AppIcon.WindowIconUri));
        Assert.NotNull(new WindowIcon(ico));
    }
}
