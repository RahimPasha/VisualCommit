// What checks C14 to C17 of phase 4's walk-through share (docs/test-reports/phase-4.md,
// "Conventions of checks C14 to C17 and D18"): opening the resolver from the stage panel, where
// it stands, its header, toolbar, panes, bars, result and tints as the report writes them, the
// stage panel's titles and rows, the questions, and "nothing written". They are in a nested
// class of their own (C14To17) so that they never clash with what checks C10 to C13 add to the
// same partial class Phase4ChecksC. The checks are in Phase4Checks.C.Editing.cs (C14),
// .C.Kinds.cs (C15), .C.MergeTool.cs (C16) and .C.Terminal.cs (C17); their expected values in
// Phase4Expectations.C2.cs (PartC2).
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

// Until checks C10 to C13's Phase4Checks.C.cs (which declares the class with its attributes) is
// merged, this part names the base class too, which C# accepts on several parts.
public sealed partial class Phase4ChecksC : Phase4Checks
{
    /// <summary>What checks C14 to C17 share; see the file's header.</summary>
    internal static class C14To17
    {
        /// <summary>The app's tint for the report's token name.</summary>
        public static ConflictTint AppTint(Tint tint) => Enum.Parse<ConflictTint>(tint.ToString());

        // ---- Opening the resolver, and where it stands -------------------------------------------

        /// <summary>
        /// Step 1 of C14, C15 and C16: clicks the working-changes row, waits for the stage panel
        /// with <paramref name="unstagedTitle"/>, clicks <paramref name="path"/> in the unstaged list
        /// and waits until the resolver shows it.
        /// </summary>
        public static async Task OpenResolverAsync(ShellDriver app, string path, string unstagedTitle)
        {
            app.Click(app.WorkingRowPoint());
            await WaitForStagePanelAsync(app, unstagedTitle, PartC2.StagedTitle(0));
            app.Click(await app.FileRowAsync("UnstagedFileList", path));
            await app.WaitForResolverAsync(WorkingChangesViewModel.FileName(path));
            await app.WaitForIdleAsync();
        }

        /// <summary>
        /// The resolver's place (11.2, the conventions' "The resolver"): over the graph and the
        /// right panel's columns, x 260 to 1100 at 1100×700 (to 1920 at 1920×1080), under the
        /// banner across its width; the right panel and <c>RightSplitter</c> hidden, the left panel
        /// and <c>LeftSplitter</c> shown; the header and the toolbar 36 high.
        /// </summary>
        public static void AssertResolverPlace(ShellDriver app)
        {
            var view = app.ResolverView;
            var bounds = app.BoundsOf(view);
            var width = app.Window.ClientSize.Width;
            Assert.Equal(PartC2.ResolverLeft, bounds.X, 0.5);
            Assert.Equal(width - PartC2.ResolverLeft, bounds.Width, 0.5);
            var tab = app.Session.Shell.ActiveTab;
            Assert.False(tab.ShowsRightPanel, "The right panel shows beside the resolver.");
            Assert.False(Splitter(app, "RightSplitter").IsEffectivelyVisible, "RightSplitter shows beside the resolver.");
            Assert.True(Splitter(app, "LeftSplitter").IsEffectivelyVisible, "LeftSplitter is hidden.");
            Assert.True(app.FindByAutomationId("RefList").IsEffectivelyVisible, "The left panel is hidden.");
            Assert.False(app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "The stage panel shows beside the resolver.");
            var banner = app.BannerBounds ?? throw new Xunit.Sdk.XunitException("No banner is shown over the resolver.");
            Assert.Equal(bounds.X, banner.X, 0.5);
            Assert.Equal(bounds.Width, banner.Width, 0.5);
            Assert.True(banner.Bottom <= bounds.Top + 0.5, $"The banner ({banner}) is not above the resolver ({bounds}).");
            Assert.Equal(PartC2.ResolverRowHeight, view.FindControl<Border>("ResolverHeader")!.Bounds.Height, 0.5);
            Assert.Equal(PartC2.ResolverRowHeight, view.FindControl<Border>("ResolverToolbar")!.Bounds.Height, 0.5);
        }

        /// <summary>The resolver's header (11.2): <c>U</c> in <c>VcDangerBrush</c>, the file's name, an empty folder, the kind, <c>File {i} of {n}</c>, and the close button.</summary>
        public static void AssertHeader(ShellDriver app, ResolverSnapshot resolver, string file, string kind, string position)
        {
            Assert.Equal((PartC2.Conflicted, file, string.Empty, kind, position), (resolver.Status, resolver.FileName, resolver.Folder ?? string.Empty, resolver.Kind, resolver.FilePosition));
            var status = (TextBlock)app.FindByAutomationId(app.ResolverView, "ResolverFileStatus");
            Assert.Equal(PaletteOf(app).Danger, (status.Foreground as ISolidColorBrush)?.Color);
            Assert.True(app.FindByAutomationId(app.ResolverView, "ResolverCloseButton").IsEffectivelyVisible, "The close button is hidden.");
            Assert.True(app.FindByAutomationId(app.ResolverView, "ResolverPreviousFile").IsEffectivelyVisible, "The previous file button is hidden.");
            Assert.True(app.FindByAutomationId(app.ResolverView, "ResolverNextFile").IsEffectivelyVisible, "The next file button is hidden.");
        }

        /// <summary>
        /// The toolbar of a text conflict (11.2): the conflict position with its arrows, Take ours
        /// and Take theirs enabled, <c>Merge tool…</c> enabled or disabled with its reason, and
        /// <c>Mark resolved</c>, a primary button, enabled.
        /// </summary>
        public static void AssertTextToolbar(ShellDriver app, ResolverSnapshot resolver, string position, bool mergeTool)
        {
            Assert.Equal(position, resolver.ConflictPosition);
            Assert.True(app.FindByAutomationId(app.ResolverView, "ResolverPreviousConflict").IsEffectivelyVisible, "The previous conflict button is hidden.");
            Assert.True(app.FindByAutomationId(app.ResolverView, "ResolverNextConflict").IsEffectivelyVisible, "The next conflict button is hidden.");
            Assert.Equal(new ResolverButton(PartC2.TakeOurs, true, null), resolver.TakeOurs);
            Assert.Equal(new ResolverButton(PartC2.TakeTheirs, true, null), resolver.TakeTheirs);
            Assert.Equal(mergeTool ? new ResolverButton(PartC2.MergeToolButton, true, null) : new ResolverButton(PartC2.MergeToolButton, false, PartC2.NoMergeTool), resolver.MergeTool);
            if (!mergeTool)
            {
                Assert.True(ToolTip.GetShowOnDisabled((Button)app.FindByAutomationId(app.ResolverView, "ResolverMergeTool")), "The disabled Merge tool… does not show its reason.");
            }

            Assert.Equal(new ResolverButton(PartC2.MarkResolved, true, null), resolver.MarkResolved);
            AssertPrimary(app, (Button)app.FindByAutomationId(app.ResolverView, "ResolverMarkResolved"));
        }

        /// <summary>A button in the primary style (<c>VcPrimaryButton</c>): the accent background.</summary>
        public static void AssertPrimary(ShellDriver app, Button button) =>
            Assert.Equal(PaletteOf(app).Accent, (button.Background as ISolidColorBrush)?.Color);

        /// <summary>The titles of the panes and the result, and the result's count (11.2, 11.4).</summary>
        public static void AssertTitles(ResolverSnapshot resolver, string ours, string theirs, int blocksLeft)
        {
            Assert.Equal((ours, theirs, PartC2.ResultTitle, PartC2.ConflictsLeft(blocksLeft)), (resolver.OursTitle, resolver.TheirsTitle, resolver.ResultTitle, resolver.ResultCount));
        }

        /// <summary>
        /// The result's text and the tint of each of its lines (<c>TintAt</c>). The text ends with
        /// LF, so the editor's document has one more, empty, line after the last; it has no tint.
        /// </summary>
        public static void AssertResult(ResolverSnapshot resolver, string text, IReadOnlyList<Tint> tints)
        {
            Assert.Equal(text, resolver.Result);
            Assert.Equal(tints.Select(AppTint), resolver.ResultTints.Take(tints.Count));
            Assert.Equal(tints.Count + 1, resolver.ResultTints.Count);
            Assert.Equal(ConflictTint.None, resolver.ResultTints[^1]);
        }

        /// <summary>Both panes' rows, top to bottom: each row's line number, text and tint, a bar's tint <c>Bar</c>.</summary>
        public static void AssertPanes(ResolverSnapshot resolver, IReadOnlyList<PartC2.PaneRow> ours, IReadOnlyList<PartC2.PaneRow> theirs)
        {
            AssertPane("ours", resolver.OursRows, ours);
            AssertPane("theirs", resolver.TheirsRows, theirs);
        }

        private static void AssertPane(string side, IReadOnlyList<ConflictPaneRow> actual, IReadOnlyList<PartC2.PaneRow> expected)
        {
            var got = actual.Select(row => row.IsBar ? PartC2.Bar.ToString() : new PartC2.PaneRow(row.Number, row.Text, Enum.Parse<Tint>(row.Tint.ToString())).ToString()).ToList();
            var want = expected.Select(row => row.ToString()).ToList();
            Assert.True(
                got.SequenceEqual(want) && actual.Where(row => row.IsBar).All(row => row.Tint == ConflictTint.Bar),
                $"The {side} pane is not the expected one.\nExpected:\n  " + string.Join("\n  ", want) + "\nActual:\n  " + string.Join("\n  ", got));
        }

        /// <summary>
        /// What block <paramref name="block"/>'s (from 0) bars show: their buttons (null) or their
        /// state's text, in each pane; and the buttons in view while they show (11.5).
        /// </summary>
        public static void AssertBars(ShellDriver app, int block, string? ours, string? theirs)
        {
            Assert.Equal((ours, theirs), (app.BarText(block, ConflictPaneSide.Ours), app.BarText(block, ConflictPaneSide.Theirs)));
            AssertBarButtons(app, block, ConflictPaneSide.Ours, ours is null, ConflictChoice.UseOurs, ConflictChoice.UseBothOursFirst);
            AssertBarButtons(app, block, ConflictPaneSide.Theirs, theirs is null, ConflictChoice.UseTheirs, ConflictChoice.UseBothTheirsFirst);
        }

        private static void AssertBarButtons(ShellDriver app, int block, ConflictPaneSide side, bool shown, params ConflictChoice[] choices)
        {
            var labels = choices.Select(choice => app.ResolverPanes.BarButton(block, side, choice)?.Content as string).ToList();
            if (shown)
            {
                var expected = side == ConflictPaneSide.Ours ? new[] { "Use ours", "Use both" } : ["Use theirs", "Use both"];
                Assert.Equal(expected, labels);
            }
            else
            {
                Assert.All(labels, Assert.Null);
            }
        }

        /// <summary>The keyboard focus is in the result, with no focus outline, and the caret at <paramref name="line"/>, <paramref name="column"/>.</summary>
        public static void AssertFocusAndCaret(ShellDriver app, int line, int column)
        {
            var area = app.ResultEditor.TextArea;
            Assert.True(area.IsKeyboardFocusWithin, $"The result does not have the keyboard focus: {Describe(app.Window.FocusManager?.GetFocusedElement())} has it.");
            Assert.DoesNotContain(":focus-visible", area.Classes);
            Assert.Equal(new TextLocation(line, column), app.ResultCaret);
            Assert.False(ConflictResultEditor.CaretBlinks, "The result's caret blinks in the scripted run (D151).");
        }

        // ---- The banner ---------------------------------------------------------------------------

        /// <summary>The banner's icon and text tooltip (10.3), read from the control that holds it.</summary>
        public static string? BannerToolTip(ShellDriver app) => ToolTip.GetTip(app.Find<Border>("BannerInfo")) as string;

        /// <summary>The banner's background (<c>VcBannerBackgroundBrush</c>) and its 1-px bottom line in <c>VcWarningBrush</c>, by brush.</summary>
        public static Border BannerBorder(ShellDriver app)
        {
            var banner = app.Window.GetVisualDescendants().OfType<Control>().Single(control => AutomationProperties.GetAutomationId(control) == "ConflictBanner");
            return banner.GetVisualDescendants().OfType<Border>().First();
        }

        // ---- The stage panel ----------------------------------------------------------------------

        /// <summary>Waits until the stage panel shows with these titles over its two lists.</summary>
        public static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
            app.WaitForAsync(
                () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                    && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                    && app.Find<TextBlock>("StagedTitle").Text == stagedTitle,
                $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

        /// <summary>A stage list's file rows as "&lt;letter&gt; &lt;name&gt;" (every file of these checks is at the root).</summary>
        public static List<string> FileRows(ShellDriver app, string list)
        {
            var rows = (IEnumerable<ChangedFileRow>)(app.Find<ItemsControl>(list).ItemsSource ?? Array.Empty<ChangedFileRow>());
            return [.. rows.Where(row => row.IsFile).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
        }

        /// <summary>A conflicted file's row: its <c>U</c> in the danger colour and the tooltip <c>Conflict: click to resolve it</c> (5.3).</summary>
        public static async Task AssertConflictRowAsync(ShellDriver app, string path)
        {
            var row = await app.FileRowAsync("UnstagedFileList", path);
            AssertTip(row, PartC2.ConflictRowTip);
            var letter = row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "U");
            Assert.Equal(PaletteOf(app).Danger, (letter.Foreground as ISolidColorBrush)?.Color);
        }

        /// <summary>The empty-list texts: "No unstaged changes" and "No staged changes" shown or not.</summary>
        public static void AssertEmptyLists(ShellDriver app, bool unstagedEmpty, bool stagedEmpty)
        {
            var noUnstaged = app.Find<TextBlock>("NoUnstaged");
            var noStaged = app.Find<TextBlock>("NoStaged");
            Assert.Equal(unstagedEmpty, noUnstaged.IsEffectivelyVisible);
            Assert.Equal(stagedEmpty, noStaged.IsEffectivelyVisible);
            if (unstagedEmpty)
            {
                Assert.Equal(PartC2.NoUnstaged, noUnstaged.Text);
            }

            if (stagedEmpty)
            {
                Assert.Equal(PartC2.NoStaged, noStaged.Text);
            }
        }

        /// <summary>
        /// The message box: its summary and description (5.3). The stage panel fills them from the
        /// operation's message after a read of its own, so this waits for the summary first.
        /// </summary>
        public static async Task AssertMessageAsync(ShellDriver app, string summary, string description)
        {
            var summaryBox = (TextBox)app.FindByAutomationId("CommitSummary");
            await app.WaitForAsync(() => (summaryBox.Text ?? string.Empty) == summary, $"the stage panel's summary \"{summary}\"", OutsideChangeTimeout);
            Assert.Equal(summary, summaryBox.Text ?? string.Empty);
            Assert.Equal(description, ((TextBox)app.FindByAutomationId("CommitDescription")).Text ?? string.Empty);
        }

        /// <summary>The stage panel's commit button: its label, whether it is enabled, its tooltip.</summary>
        public static void AssertCommitButton(ShellDriver app, string label, bool enabled, string? toolTip = null)
        {
            var button = app.Find<Button>("CommitButton");
            Assert.Equal(label, button.Content);
            Assert.Equal(enabled, button.IsEffectivelyEnabled);
            AssertTip(button, toolTip);
        }

        // ---- Questions ----------------------------------------------------------------------------

        /// <summary>Clicks a button of the dialog on top by its automation id (<c>CancelButton</c>, <c>ConfirmButton</c>).</summary>
        public static void ClickDialogButton(ShellDriver app, string automationId)
        {
            var view = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
            app.Click(app.FindByAutomationId(view, automationId));
        }

        /// <summary>The confirm button of the dialog on top is primary (the accent background), as design 7.2's "(primary)" says.</summary>
        public static void AssertConfirmPrimary(ShellDriver app)
        {
            var view = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
            var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
            Assert.Equal(PaletteOf(app).Accent, (confirm.Background as ISolidColorBrush)?.Color);
        }

        /// <summary>The confirm button of the dialog on top is drawn in <c>VcDangerBrush</c>.</summary>
        public static void AssertConfirmDanger(ShellDriver app)
        {
            var view = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
            var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
            Assert.Equal(PaletteOf(app).Danger, (confirm.Background as ISolidColorBrush)?.Color);
        }

        // ---- Git -----------------------------------------------------------------------------------

        /// <summary><c>git status --porcelain=v1 --untracked-files=all</c>, line by line.</summary>
        public static async Task<List<string>> PorcelainAsync(TempRepo repo) =>
            [.. Lines((await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all")).StandardOutput)];

        /// <summary><c>git ls-files -u</c>, line by line.</summary>
        public static async Task<List<string>> UnmergedAsync(TempRepo repo) =>
            [.. Lines((await repo.GitAsync("ls-files", "-u")).StandardOutput)];

        /// <summary>The blob the index holds for <paramref name="path"/> at stage 0 (<c>git rev-parse :path</c>).</summary>
        public static async Task<string> StagedBlobAsync(TempRepo repo, string path) =>
            (await repo.GitAsync("rev-parse", ":" + path)).StandardOutput.Trim();

        /// <summary>A file in the git folder (<c>MERGE_HEAD</c>, <c>rebase-merge/onto</c>), trimmed, or null when it is not there.</summary>
        public static string? GitFile(TempRepo repo, string name)
        {
            var path = Path.Combine(repo.Path, ".git", name);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }

        /// <summary>A working-tree file's bytes.</summary>
        public static byte[] WorkingFile(TempRepo repo, string path) => File.ReadAllBytes(Path.Combine(repo.Path, path));

        /// <summary>The bytes of a text as the scenario's files hold it: UTF-8, LF.</summary>
        public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        /// <summary>
        /// "Nothing written" (the conventions): the working-tree file is byte-identical to
        /// <paramref name="file"/>, git's status and unmerged entries are <paramref name="status"/>
        /// and <paramref name="unmerged"/>, and no <c>Resolve …</c> operation was logged since
        /// <paramref name="logMark"/>.
        /// </summary>
        public static async Task AssertNothingWrittenAsync(ShellDriver app, TempRepo repo, string path, string file, IReadOnlyList<string> status, IReadOnlyList<string> unmerged, int logMark)
        {
            Assert.Equal(Bytes(file), WorkingFile(repo, path));
            Assert.Equal(status, await PorcelainAsync(repo));
            Assert.Equal(unmerged, await UnmergedAsync(repo));
            var resolve = new Regex(@"\] " + Regex.Escape(NameOf(repo)) + @": Resolve [^:]+: \w+ after ", RegexOptions.CultureInvariant);
            var logged = app.LogLinesSince(logMark).Where(line => resolve.IsMatch(line)).ToList();
            Assert.True(logged.Count == 0, "A Resolve operation was logged:\n" + string.Join('\n', logged));
        }

        /// <summary>Whether a <paramref name="title"/> operation was logged since <paramref name="logMark"/>, whatever its outcome.</summary>
        public static bool Logged(ShellDriver app, TempRepo repo, string title, int logMark) =>
            app.LogLinesSince(logMark).Any(line => AppLogFile.OperationEnded(NameOf(repo), title).IsMatch(line));

        /// <summary>A git output's lines, without carriage returns and the empty last line.</summary>
        public static IEnumerable<string> Lines(string output) =>
            output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0);

        // ---- Pictures --------------------------------------------------------------------------------

        /// <summary>
        /// Samples the tints of the result's lines in <paramref name="picture"/> (the conventions:
        /// at the line's vertical centre, where no text of these files reaches, 820 px into the result
        /// at 1100×700), each against its token's colour; a line with no tint against the result's
        /// own background at line 1.
        /// </summary>
        public static void AssertResultColours(ShellDriver app, Screenshot picture, IReadOnlyList<Tint> tints, double x)
        {
            var palette = PaletteOf(app);
            var editor = app.ResultEditor;
            var background = picture.PixelAt(At(app, editor, x, 1));
            for (var line = 1; line <= tints.Count; line++)
            {
                if (editor.PointAt(line, 1) is null)
                {
                    continue;
                }

                var expected = tints[line - 1] == Tint.None ? background : ColourOf(palette, tints[line - 1]);
                AssertColour(expected, picture, At(app, editor, x, line), string.Create(CultureInfo.InvariantCulture, $"result's line {line} ({tints[line - 1]})"));
            }
        }

        /// <summary>The colour of a tint's token in <paramref name="palette"/>.</summary>
        public static Color ColourOf(Palette palette, Tint tint) => tint switch
        {
            Tint.Ours => palette.ConflictOurs,
            Tint.Theirs => palette.ConflictTheirs,
            Tint.Base => palette.ConflictBase,
            Tint.Marker => palette.ConflictMarker,
            Tint.Used => palette.ConflictUsed,
            _ => throw new ArgumentOutOfRangeException(nameof(tint), tint, "No token of design 7.3."),
        };

        private static Point At(ShellDriver app, ConflictResultEditor editor, double x, int line)
        {
            var y = editor.PointAt(line, 1)!.Value.Y;
            return editor.TranslatePoint(new Point(x, y), app.Window)!.Value;
        }

        /// <summary>An element as a failure message names it: its type, name, automation id and content.</summary>
        public static string Describe(object? element) => element switch
        {
            null => "nothing",
            Control control => $"{control.GetType().Name} \"{control.Name}\" ({AutomationProperties.GetAutomationId(control)}, {(control as ContentControl)?.Content})",
            _ => element.GetType().Name,
        };

        private static GridSplitter Splitter(ShellDriver app, string name) =>
            app.Window.GetVisualDescendants().OfType<GridSplitter>().Single(splitter => splitter.Name == name);
    }
}
