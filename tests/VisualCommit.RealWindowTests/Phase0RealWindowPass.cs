using System.Drawing;
using System.Text.Json;
using Xunit;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The real-window pass of phase 0: check 8 of docs/test-reports/phase-0.md. It repeats the key
/// steps of the scripted walk-through in the real window, saves the screenshots and a
/// <c>run.txt</c> with the facts of the run under artifacts/visual/phase-0/real-window/, and
/// compares each screenshot with the scripted one of the same step. Run the scripted
/// walk-through first (<c>dotnet test</c> at the repo root): this pass needs its screenshots.
/// </summary>
public class Phase0RealWindowPass
{
    private static readonly Color DarkGraph = ColorTranslator.FromHtml("#14161B");
    private static readonly Color DarkPanel = ColorTranslator.FromHtml("#1A1D24");
    private static readonly Color DarkChrome = ColorTranslator.FromHtml("#20242C");
    private static readonly Color LightGraph = ColorTranslator.FromHtml("#FFFFFF");
    private static readonly Color LightPanel = ColorTranslator.FromHtml("#F5F6F8");
    private static readonly Color LightChrome = ColorTranslator.FromHtml("#EBEDF1");

    [Fact]
    public void Check_8_the_real_window_matches_the_scripted_walk_through()
    {
        using var run = new RealWindowRun(phase: 0);
        var dataDirectory = run.NewDataDirectory();

        // Steps 2 to 4: first start, then a click on the Theme button with the real mouse.
        using (var app = RealApp.Launch(dataDirectory))
        {
            run.NoteDisplay(app);
            app.SetClientSize(1100, 700);
            app.MoveMouseTo(480, 250);
            Assert.Equal("VisualCommit", app.Window.Title);

            using (var frame = app.CaptureWindow())
            {
                run.Save(frame, "A-first-start-dark-with-frame");
            }

            using (var first = app.CaptureClient())
            {
                run.Save(first, "A-first-start-dark");
                AssertShell(app, first, DarkChrome, DarkPanel, DarkGraph);
                run.AssertMatchesScripted(first, "A-first-start-dark", "01-shell-dark-1100x700");
            }

            app.ClickWithMouse(app.Find("ThemeSwitch"));

            using (var frame = app.CaptureWindow())
            {
                run.Save(frame, "B-after-theme-click-light-with-frame");
            }

            using (var afterClick = app.CaptureClient())
            {
                run.Save(afterClick, "B-after-theme-click-light");
                AssertShell(app, afterClick, LightChrome, LightPanel, LightGraph);
                run.AssertMatchesScripted(afterClick, "B-after-theme-click-light", "05b-theme-switch-after-first-click");
            }

            Assert.Equal("Light", SavedTheme(dataDirectory));

            app.MoveMouseTo(480, 250);
            Assert.Equal(0, app.Close());
        }

        // Step 5: a new instance of the app on the same data folder.
        using (var app = RealApp.Launch(dataDirectory))
        {
            app.SetClientSize(1100, 700);
            app.MoveMouseTo(480, 250);

            using (var restarted = app.CaptureClient())
            {
                run.Save(restarted, "C-restarted-light");
                AssertShell(app, restarted, LightChrome, LightPanel, LightGraph);
                run.AssertMatchesScripted(restarted, "C-restarted-light", "06d-restarted-ready");
            }

            Assert.Equal("Light", SavedTheme(dataDirectory));
            Assert.Equal(0, app.Close());
        }

        // Step 6: the large size, on a new data folder so that it is the dark first start again.
        using (var app = RealApp.Launch(run.NewDataDirectory()))
        {
            if (!app.Fits(1920, 1080))
            {
                run.Note($"D (1920x1080): not run. A 1920x1080 window does not fit the {app.WorkAreaSize.Width:F0}x{app.WorkAreaSize.Height:F0} work area at {app.Scaling:P0} scaling.");
                app.Close();
                return;
            }

            app.SetClientSize(1920, 1080);
            app.MoveMouseTo(900, 300);
            using var large = app.CaptureClient();
            run.Save(large, "D-first-start-dark-1920x1080");
            AssertShell(app, large, DarkChrome, DarkPanel, DarkGraph);
            run.AssertMatchesScripted(large, "D-first-start-dark-1920x1080", "02-shell-dark-1920x1080");
            Assert.Equal(0, app.Close());
        }
    }

    /// <summary>
    /// Asserts what UI Automation and the screen capture can tell about the shell: the texts,
    /// the toolbar buttons with their names and which of them is enabled, and the background colours.
    /// </summary>
    private static void AssertShell(RealApp app, Bitmap capture, Color chrome, Color panel, Color graph)
    {
        var size = app.ClientSize;
        var scaling = app.Scaling;

        var texts = app.Texts();
        string[] expectedTexts =
        [
            "No repository", "Local branches", "Remotes", "Pull requests", "Tags", "Stashes",
            "Branch / Tag", "Graph", "Message", "Author", "Date", "SHA",
            "No repository open", "Open, clone or init a repository to see its history.",
            "Commit details", "Select a commit to see its details.", "Ready", "Activity log",
        ];
        Assert.All(expectedTexts, text => Assert.Contains(text, texts));
        Assert.Contains(texts, text => text.StartsWith("Git ", StringComparison.Ordinal) && char.IsDigit(text[4]));

        // Toolbar: every button is there under its label, in order, and only Theme is enabled.
        string[] labels = ["Undo", "Redo", "Fetch", "Pull", "Push", "Branch", "Stash", "Pop", "Search", "Theme"];
        var buttons = labels.Select(label => app.Find(label == "Theme" ? "ThemeSwitch" : label + "Button")).ToList();
        Assert.Equal(labels, buttons.Select(button => button.Name));
        Assert.Equal(["Theme"], buttons.Where(button => button.IsEnabled).Select(button => button.Name));
        var positions = buttons.Select(button => app.BoundsOf(button)).ToList();
        Assert.Equal(positions.OrderBy(bounds => bounds.X), positions);
        Assert.All(positions, bounds => Assert.InRange(bounds.Top, 36, 88));
        Assert.InRange(positions[^1].Right, size.Width - 20, size.Width);

        // The same empty spots as the scripted walk-through samples.
        RealWindowRun.AssertColour(chrome, capture.ColourAt(size.Width - 30, 18, scaling), "repo tabs");
        RealWindowRun.AssertColour(chrome, capture.ColourAt(size.Width / 2, 62, scaling), "toolbar");
        RealWindowRun.AssertColour(chrome, capture.ColourAt(size.Width / 2, size.Height - 13, scaling), "status bar");
        RealWindowRun.AssertColour(panel, capture.ColourAt(130, size.Height - 66, scaling), "left panel");
        RealWindowRun.AssertColour(panel, capture.ColourAt(size.Width - 200, size.Height - 66, scaling), "right panel");
        RealWindowRun.AssertColour(graph, capture.ColourAt(260 + ((size.Width - 660) / 2), 146, scaling), "commit graph");
    }

    private static string? SavedTheme(string dataDirectory)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDirectory, "settings.json")));
        return settings.RootElement.GetProperty("theme").GetString();
    }
}
