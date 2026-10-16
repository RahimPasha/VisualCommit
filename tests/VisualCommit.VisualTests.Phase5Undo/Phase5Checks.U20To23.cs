// What checks U20 to U23 of part U share (docs/test-reports/phase-5.md, "Conventions of part U"):
// the toolbar's Undo and Redo as T0, T+ and T−, a step that ran ("Undo ran", "Redo ran"), the
// question before a step that changes a remote, and git's state read back with plain git. In a
// nested class of their own, U20To23, so that the first half's helpers (U16 to U19) and these never
// clash in the one class Phase5ChecksU; the checks call them through the alias Steps.
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

/// <summary>
/// The undo and redo checks of part U (docs/test-reports/phase-5.md, checks U16 to U23), one test
/// method per action with its undo and redo, each on a fresh copy of its scenario (design 15.4,
/// D181).
/// </summary>
public sealed partial class Phase5ChecksU : Phase5Checks
{
    /// <summary>The helpers of checks U20 to U23.</summary>
    internal static class U20To23
    {
        /// <summary>A step's direction, as the report writes its two buttons.</summary>
        public enum Direction
        {
            Undo,
            Redo,
        }

        /// <summary>
        /// The toolbar's Undo and Redo as the report writes them (T0, T+ x, T− x), each button's
        /// enabled state and tooltip; and a disabled one shows its tooltip while disabled
        /// (<c>ToolTip.ShowOnDisabled</c>, 7.4).
        /// </summary>
        public static void AssertButtons(ShellDriver app, UndoRedoButtons expected)
        {
            Assert.Equal(expected, app.UndoRedoState());
            foreach (var id in new[] { ShellDriver.UndoButtonId, ShellDriver.RedoButtonId })
            {
                var button = (Control)app.FindByAutomationId(id);
                if (!button.IsEffectivelyEnabled)
                {
                    Assert.True(ToolTip.GetShowOnDisabled(button), $"The disabled {id} does not show its reason.");
                }
            }
        }

        /// <summary>
        /// Clicks Undo or Redo for a step that changes no remote and waits until it ran, as the
        /// conventions' "Undo ran" says: one operation titled <c>Undo x</c> (or <c>Redo x</c>), no
        /// question, the queue's log line <c>&lt;repo&gt;: Undo x: Succeeded after &lt;N&gt; ms:
        /// Undid x</c>, then idle, no dialog, and the status bar <c>Undid x</c> in the secondary
        /// colour. Returns the git calls' count before the click, to read the step's calls with.
        /// </summary>
        public static async Task<int> StepAsync(ShellDriver app, TempRepo repo, Direction direction, string text)
        {
            var calls = app.GitCalls().Count;
            var mark = app.LogMark();
            using var texts = app.RecordStatusTexts();
            await ClickAsync(app, direction);
            Assert.True(app.Session.Services.DialogHost.Current is not ConfirmationViewModel, $"{direction} {text} asked a question; only a step that changes a remote asks.");
            await AssertRanAsync(app, repo, direction, text, mark, texts, OperationKind.Local);
            return calls;
        }

        /// <summary>
        /// Clicks Undo or Redo for a step that changes a remote: its question opens (7.5, 13.2),
        /// <c>ConfirmTitle</c> and <c>ConfirmQuestion</c> as given, no detail line, "Cancel" with
        /// the focus and the confirm button "Undo" or "Redo" in the primary style; no operation has
        /// started and git pushed nothing. Returns the log's mark before the click.
        /// </summary>
        public static async Task<int> AskAsync(ShellDriver app, TempRepo repo, Direction direction, string text, PartU2.Question question)
        {
            var calls = app.GitCalls().Count;
            var mark = app.LogMark();
            await ClickAsync(app, direction);
            await AssertConfirmationAsync(app, question.Title, question.Text, string.Empty, direction.ToString(), destructive: false);
            Assert.DoesNotContain(app.LogLinesSince(mark), line => AppLogFile.OperationEnded(NameOf(repo), Title(direction, text)).IsMatch(line));
            Assert.Null(app.Session.Shell.ActiveTab.Repository!.Queue.Current);
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            return mark;
        }

        /// <summary>
        /// Clicks the question's confirm button and waits until the step ran (<see cref="StepAsync"/>'s
        /// "Undo ran"), as a network operation may take. Returns the git calls' count before the click.
        /// </summary>
        public static async Task<int> ConfirmAsync(ShellDriver app, TempRepo repo, Direction direction, string text)
        {
            var calls = app.GitCalls().Count;
            var mark = app.LogMark();
            using var texts = app.RecordStatusTexts();
            ClickDialogButton(app, "ConfirmButton");
            await AssertRanAsync(app, repo, direction, text, mark, texts, OperationKind.Network);
            return calls;
        }

        /// <summary>
        /// "Undo ran" after <paramref name="mark"/>: the running text the status bar showed, the log
        /// line, idle, no dialog, the status, and the operation's record: a network operation for a step
        /// that changes a remote (7.5), a local one otherwise.
        /// </summary>
        private static async Task AssertRanAsync(ShellDriver app, TempRepo repo, Direction direction, string text, int mark, StatusTextRecorder texts, OperationKind kind)
        {
            var result = (direction == Direction.Undo ? "Undid " : "Redid ") + text;
            var ended = AppLogFile.OperationEnded(NameOf(repo), Title(direction, text));
            var line = await app.WaitForLogLineAsync(ended, mark: mark, timeout: NetworkTimeout);
            Assert.Matches(AppLogFile.OperationEnded(NameOf(repo), Title(direction, text), OperationOutcome.Succeeded, result), line);
            await app.WaitForIdleAsync(timeout: NetworkTimeout);
            Assert.Equal(1, app.CountLogLines(ended, mark));
            await AssertNoDialogAsync(app);
            AssertStatus(app, result);
            Assert.Contains(Running(direction, text), texts.Texts);
            var record = app.Session.Operations.Snapshot().Last(operation => operation.Title == Title(direction, text));
            Assert.Equal((OperationOutcome.Succeeded, result, kind), (record.Outcome, record.Message, record.Kind));
        }

        /// <summary>The status bar's text while a step runs (12.1): <c>Undoing x…</c> or <c>Redoing x…</c>.</summary>
        public static string Running(Direction direction, string text) => $"{(direction == Direction.Undo ? "Undoing" : "Redoing")} {text}…";

        /// <summary>The operation's title of a step: <c>Undo x</c> or <c>Redo x</c> (12.1).</summary>
        public static string Title(Direction direction, string text) => $"{direction} {text}";

        private static Task ClickAsync(ShellDriver app, Direction direction) =>
            direction == Direction.Undo ? app.ClickUndoAsync(NetworkTimeout) : app.ClickRedoAsync(NetworkTimeout);

        /// <summary>Clicks a button of the dialog on top by its automation id.</summary>
        public static void ClickDialogButton(ShellDriver app, string automationId) =>
            app.Click(app.FindByAutomationId(app.TopDialogView ?? throw new InvalidOperationException("No dialog is open."), automationId));

        /// <summary>
        /// Starts an action as its menu item does (the item clicked in the menu open now) and waits
        /// until its operation <paramref name="title"/> ended and the tab is idle.
        /// </summary>
        public static async Task<int> RunMenuItemAsync(ShellDriver app, TempRepo repo, string item, string title)
        {
            var calls = app.GitCalls().Count;
            var mark = app.LogMark();
            app.ClickMenuItem(item);
            await WaitForOperationAsync(app, repo, title, mark, NetworkTimeout);
            return calls;
        }

        /// <summary>Right-clicks a left-panel row by its path and waits for its menu.</summary>
        public static async Task OpenRowMenuAsync(ShellDriver app, params string[] path)
        {
            app.RightClick(await app.RefRowAsync(path));
            app.Menu();
        }

        /// <summary>Right-clicks a graph row in its Message column and waits for its menu.</summary>
        public static void OpenCommitMenu(ShellDriver app, int row)
        {
            app.RightClick(app.GraphRowPoint(row));
            app.Menu();
        }

        /// <summary>Right-clicks a graph row's label at its centre and waits for its menu.</summary>
        public static void OpenLabelMenu(ShellDriver app, int row, string label)
        {
            app.RightClick(app.GraphLabelBounds(row, label).Center);
            app.Menu();
        }

        /// <summary>The step's calls: the git calls the app made since <paramref name="mark"/> that belong to an operation (not reads or refreshes).</summary>
        public static List<string> OperationCallsSince(ShellDriver app, int mark) =>
            [.. app.GitCalls().Skip(mark).Where(call => call.OperationId is not null).Select(call => call.CommandText)];

        /// <summary>The last call since <paramref name="mark"/> whose command line is <paramref name="command"/>, which must have succeeded.</summary>
        public static GitCallRecord AssertSucceeded(ShellDriver app, int mark, string command)
        {
            var call = AssertCalled(app, mark, command);
            Assert.True(call.Succeeded, $"\"{command}\" failed (exit {call.ExitCode}):\n{call.StandardError}");
            return call;
        }

        /// <summary>Asserts that a call's standard error holds <paramref name="line"/> (git's push summary, TU3) on the report's git.</summary>
        public static async Task AssertPrintedAsync(GitCallRecord call, string line)
        {
            if (await IsReportGitAsync())
            {
                Assert.Contains(line, Lines(call.StandardError));
            }
        }

        // ---- Git, read back with plain git ----

        /// <summary>The lines of git's output, without empty ones and line ends.</summary>
        public static List<string> Lines(string output) =>
            [.. output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0)];

        /// <summary>The lines git prints for these arguments.</summary>
        public static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
            Lines((await repo.GitAsync(arguments)).StandardOutput);

        /// <summary><c>git status --porcelain=v1 -b --untracked-files=all</c>'s lines (the conventions' status read).</summary>
        public static Task<List<string>> StatusAsync(TempRepo repo) =>
            GitLinesAsync(repo, "status", "--porcelain=v1", "-b", "--untracked-files=all");

        /// <summary><c>git stash list --format=%gd %H %gs</c>'s lines.</summary>
        public static Task<List<string>> StashListAsync(TempRepo repo) =>
            GitLinesAsync(repo, "stash", "list", "--format=%gd %H %gs");

        /// <summary>What a ref points at, in full, or null when it does not exist.</summary>
        public static Task<string?> RefAsync(TempRepo repo, string fullRef) => repo.RefTargetAsync(fullRef);

        /// <summary>The tags (<c>git tag --list</c>).</summary>
        public static Task<List<string>> TagsAsync(TempRepo repo) => GitLinesAsync(repo, "tag", "--list");

        /// <summary><c>git config --local --get-regexp &lt;pattern&gt;</c>'s lines, empty when no key matches.</summary>
        public static async Task<List<string>> ConfigAsync(TempRepo repo, string pattern)
        {
            var result = await repo.GitAllowingFailureAsync("config", "--local", "--get-regexp", pattern);
            return result.Succeeded ? Lines(result.StandardOutput) : [];
        }

        /// <summary><c>git ls-remote &lt;option&gt; origin</c>'s lines as "&lt;sha&gt; &lt;ref&gt;".</summary>
        public static async Task<List<string>> LsRemoteAsync(TempRepo repo, string option) =>
            [.. (await GitLinesAsync(repo, "ls-remote", option, "origin")).Select(line => line.Replace('\t', ' '))];

        /// <summary>The remote-tracking or other refs under <paramref name="prefix"/>, as "&lt;ref&gt; &lt;sha&gt;".</summary>
        public static Task<List<string>> RefsUnderAsync(TempRepo repo, string prefix) =>
            GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)", prefix);

        /// <summary>The backup refs, as "&lt;ref&gt; &lt;sha&gt;", in git's order of their names.</summary>
        public static async Task<List<string>> BackupLinesAsync(TempRepo repo) =>
            [.. (await repo.BackupRefsAsync()).Select(entry => $"{entry.Name} {entry.Sha}")];

        /// <summary>A working-tree file's text as git wrote it (line feeds), or null when it does not exist.</summary>
        public static string? FileText(TempRepo repo, string path)
        {
            var full = Path.Combine(repo.Path, path);
            return File.Exists(full) ? File.ReadAllText(full).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
        }

        /// <summary>The copy's origin.git as <c>git remote get-url origin</c> prints it.</summary>
        public static async Task<string> OriginUrlAsync(TempRepo repo) =>
            (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();

        // ---- The left panel ----

        /// <summary>A section's count, as its header shows it ("Stashes 1", "Remotes 2").</summary>
        public static int SectionCount(ShellDriver app, string section)
        {
            app.Settle();
            return ((RefSectionRow)app.RefRowModel(section)).Count;
        }

        /// <summary>The rows of a section, as "  name" indented two spaces per level below the section, " (open)" after an open remote.</summary>
        public static List<string> SectionLines(ShellDriver app, string section)
        {
            app.Settle();
            var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
            var start = rows.FindIndex(row => row is RefSectionRow && row.Text == section);
            Assert.True(start >= 0, $"The left panel has no section {section}.");
            return [.. rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).Cast<RefItemRow>()
                .Select(row => new string(' ', 2 * (row.Depth - 1)) + row.Text + (row.IsContainer && row.IsOpen ? " (open)" : string.Empty))];
        }

        /// <summary>A local branch's arrows (ahead, behind) as the left panel shows them; the branch by its row's path under Local branches ("feature", "topic").</summary>
        public static (int Ahead, int Behind) Arrows(ShellDriver app, params string[] branch)
        {
            app.Settle();
            var row = (RefItemRow)app.RefRowModel(["Local branches", .. branch]);
            return (row.Ahead, row.Behind);
        }

        // ---- The stage panel ----

        /// <summary>Waits until the stage panel shows with this title of its unstaged list.</summary>
        public static Task WaitForUnstagedAsync(ShellDriver app, string title) =>
            app.WaitForAsync(
                () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible && app.Find<TextBlock>("UnstagedTitle").Text == title && app.StagePanelSettled,
                $"the stage panel with \"{title}\"");

        /// <summary>A stage list's file rows as "&lt;letter&gt; &lt;name&gt; &lt;folder&gt;" (the folder left out when the file has none).</summary>
        public static List<string> FileRows(ShellDriver app, string list)
        {
            app.Settle();
            var rows = (IEnumerable<ChangedFileRow>)app.Find<ItemsControl>(list).ItemsSource!;
            return [.. rows.Where(row => row.IsFile).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
        }

        // ---- The Remotes dialog (phase 3's check 22) ----

        /// <summary>Opens the Remotes dialog from the Remotes section header's menu, "Manage remotes…", and waits for its list.</summary>
        public static async Task<RemotesDialogViewModel> OpenRemotesAsync(ShellDriver app, params string[] names)
        {
            await OpenRowMenuAsync(app, "Remotes");
            app.ClickMenuItem("Manage remotes…");
            var remotes = await app.WaitForDialogAsync<RemotesDialogViewModel>();
            await WaitForRemoteRowsAsync(app, remotes, names);
            return remotes;
        }

        /// <summary>The Remotes dialog's rows' names, top to bottom.</summary>
        public static List<string> RemoteRows(ShellDriver app) =>
            [.. app.TopDialogView!.GetVisualDescendants().OfType<Control>()
                .Where(control => AutomationProperties.GetAutomationId(control) == "RemoteRow" && control.IsEffectivelyVisible)
                .OrderBy(control => app.BoundsOf(control).Y)
                .Select(control => AutomationProperties.GetName(control) ?? string.Empty)];

        /// <summary>Waits until the Remotes dialog is on top and shows its list with exactly these remotes.</summary>
        public static Task WaitForRemoteRowsAsync(ShellDriver app, RemotesDialogViewModel remotes, params string[] names) =>
            app.WaitForAsync(
                () => app.Session.Services.DialogHost.Current == remotes && !remotes.IsFormShown && RemoteRows(app).SequenceEqual(names),
                "the Remotes dialog to list " + string.Join(", ", names),
                NetworkTimeout);

        /// <summary>Clicks one of a remote row's buttons (<c>RemoteEditButton</c>, <c>RemoteRemoveButton</c>).</summary>
        public static void ClickRemoteRowButton(ShellDriver app, string name, string automationId)
        {
            var row = app.Window.GetVisualDescendants().OfType<Control>()
                .Single(control => AutomationProperties.GetAutomationId(control) == "RemoteRow" && AutomationProperties.GetName(control) == name);
            app.Click(row.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == automationId));
        }

        // ---- Tooltips ----

        /// <summary>
        /// The open tooltip with <paramref name="text"/> lies under the button with the automation
        /// id <paramref name="button"/>, on which the pointer rests at its centre
        /// (<see cref="ShellDriver.RestPointerOnAsync"/>): below the pointer, as phase 4's
        /// conventions have a tooltip open, and starting within the button across; and inside the
        /// window.
        /// </summary>
        public static void AssertToolTipUnder(ShellDriver app, string text, string button)
        {
            var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
            var tip = Assert.Single(tips, candidate => candidate.Content is string content ? content == text : TextsIn(candidate).Contains(text));
            var bounds = app.BoundsOf(tip);
            var under = app.BoundsOf(app.FindByAutomationId(button));
            Assert.True(
                bounds.Top >= under.Center.Y && bounds.Left >= under.Left && bounds.Left < under.Right,
                $"The tooltip at {bounds} is not under {button} at {under}, below the pointer at its centre.");
            var size = app.Window.ClientSize;
            Assert.True(
                bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height,
                $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
        }
    }
}
