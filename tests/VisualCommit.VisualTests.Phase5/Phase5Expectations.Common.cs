// What is where in phase 5's expectations (docs/test-reports/phase-5.md, docs/phase-5-design.md 15.3):
// - Phase5Expectations.Common.cs (this file): what every part uses: the colours of both themes,
//   phase 5's new token among them (design 13.3); the window sizes; the menu height formula; the
//   graph's rows as the report writes them (CommitRow, WorkingChangesRow); the banner's buttons
//   and their placement (phase 4 design 5.2); backup ref names, with phase 5's tag-undone kind
//   (design 7.8); which git the report's quoted texts come from. The undo checks' project,
//   VisualCommit.VisualTests.Phase5Undo, compiles it too.
// - Phase5Expectations.<part>.cs (the report's writers): each part's own expected results, each in
//   its nested class (PartA, PartB, PartF, PartP here; the undo project's PartU in its own files),
//   so the parts never edit one file and their names never clash.
// These files are compiled by the real-window pass too (VisualCommit.RealWindowTests.csproj links
// ..\VisualCommit.VisualTests.Phase5*\Phase5Expectations*.cs from both projects, leaving out
// Phase5ExpectationTests.<part>.cs and Phase5SupportTests.cs), so that both harnesses use the same
// numbers and texts. Use only the BCL, Avalonia, Xunit, VisualCommit.Core, VisualCommit.Git and
// VisualCommit.Testing here: no other file of the walk-through (the pass does not compile them).
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>
/// What phase 5's checks compare against, copied from docs/test-reports/phase-5.md, the token
/// table of docs/architecture.md and docs/phase-5-design.md (13.3's new token, 7.8's backups), and
/// from phase 4's expectations, not read from the app, so that the two are checked against each
/// other. Each part adds its own in its nested class (<c>PartA</c>, <c>PartB</c>, <c>PartF</c>,
/// <c>PartP</c> and the undo project's <c>PartU</c>, each in Phase5Expectations.&lt;part&gt;.cs).
/// </summary>
public static partial class Phase5Expectations
{
    /// <summary>The phase whose report these are, and whose picture folder the checks fill.</summary>
    public const int Phase = 5;

    /// <summary>The window size the checks run at unless a check says otherwise (design 15.4).</summary>
    public static readonly (int Width, int Height) DefaultSize = (1100, 700);

    /// <summary>A larger window, for a check whose graph or list needs the room (phase 3's 1400×900).</summary>
    public static readonly (int Width, int Height) LargeSize = (1400, 900);

    /// <summary>The size of check B5's side-by-side layout (the light theme), scripted only: it does not fit the development machine's screen.</summary>
    public static readonly (int Width, int Height) FullSize = (1920, 1080);

    /// <summary>The label of the banner's Continue button (phase 4 design 5.2).</summary>
    public const string ContinueText = "Continue";

    /// <summary>The label of the banner's Skip button (phase 4 design 5.2).</summary>
    public const string SkipText = "Skip";

    /// <summary>The colours of one theme, from the token table, with phase 3's tokens (phase 3 design 6.4), phase 4's (phase 4 design 7.3) and phase 5's (design 13.3).</summary>
    public sealed record Palette(
        Color Window,
        Color Panel,
        Color Chrome,
        Color Control,
        Color Hover,
        Color Border,
        Color TextPrimary,
        Color TextSecondary,
        Color TextDisabled,
        Color Accent,
        Color Success,
        Color Warning,
        Color Danger,
        Color Selection,
        Color RowHover,
        Color LabelText,
        Color Backdrop,
        Color BannerBackground,
        Color DragTarget,
        Color DiffAdded,
        Color DiffRemoved,
        Color ConflictOurs,
        Color ConflictTheirs,
        Color ConflictBase,
        Color ConflictMarker,
        Color ConflictUsed,
        Color SearchMatch,
        string MenuShadow,
        IReadOnlyList<Color> Lanes)
    {
        /// <summary>The colour of lane colour number <paramref name="color"/>: palette entry <c>color % 8</c> (D48).</summary>
        public Color Lane(int color) => Lanes[color % Lanes.Count];
    }

    /// <summary>The dark theme's colours.</summary>
    public static Palette Dark { get; } = new(
        Window: Color.Parse("#14161B"),
        Panel: Color.Parse("#1A1D24"),
        Chrome: Color.Parse("#20242C"),
        Control: Color.Parse("#272C36"),
        Hover: Color.Parse("#2F3542"),
        Border: Color.Parse("#323845"),
        TextPrimary: Color.Parse("#E4E7EC"),
        TextSecondary: Color.Parse("#9BA3B0"),
        TextDisabled: Color.Parse("#5F6775"),
        Accent: Color.Parse("#7C8CFF"),
        Success: Color.Parse("#3FB97F"),
        Warning: Color.Parse("#E0A23B"),
        Danger: Color.Parse("#E5606B"),
        Selection: Color.Parse("#2A3150"),
        RowHover: Color.Parse("#1C1F27"),
        LabelText: Color.Parse("#10121A"),
        Backdrop: Color.Parse("#99000000"),
        BannerBackground: Color.Parse("#33291A"),
        DragTarget: Color.Parse("#7C8CFF"),
        DiffAdded: Color.Parse("#1A2E24"),
        DiffRemoved: Color.Parse("#331D23"),
        ConflictOurs: Color.Parse("#15323A"),
        ConflictTheirs: Color.Parse("#2C2140"),
        ConflictBase: Color.Parse("#24272F"),
        ConflictMarker: Color.Parse("#3A2E16"),
        ConflictUsed: Color.Parse("#1A2E24"),
        SearchMatch: Color.Parse("#F2C94C"),
        MenuShadow: "0 6 16 0 #80000000",
        Lanes: [.. new[] { "#7C8CFF", "#3FB97F", "#E0A23B", "#E5606B", "#4FB3D9", "#B07CE8", "#D97EB6", "#8FB84A" }.Select(Color.Parse)]);

    /// <summary>The light theme's colours.</summary>
    public static Palette Light { get; } = new(
        Window: Color.Parse("#FFFFFF"),
        Panel: Color.Parse("#F5F6F8"),
        Chrome: Color.Parse("#EBEDF1"),
        Control: Color.Parse("#FFFFFF"),
        Hover: Color.Parse("#DEE2E9"),
        Border: Color.Parse("#D2D6DE"),
        TextPrimary: Color.Parse("#1B1F27"),
        TextSecondary: Color.Parse("#5C6572"),
        TextDisabled: Color.Parse("#A1A8B3"),
        Accent: Color.Parse("#4353D8"),
        Success: Color.Parse("#187248"),
        Warning: Color.Parse("#895A17"),
        Danger: Color.Parse("#B7323D"),
        Selection: Color.Parse("#DDE1FA"),
        RowHover: Color.Parse("#F3F4F7"),
        LabelText: Color.Parse("#FFFFFF"),
        Backdrop: Color.Parse("#66000000"),
        BannerBackground: Color.Parse("#FFF4DE"),
        DragTarget: Color.Parse("#4353D8"),
        DiffAdded: Color.Parse("#E6F6EC"),
        DiffRemoved: Color.Parse("#FBE9EB"),
        ConflictOurs: Color.Parse("#DCF1F5"),
        ConflictTheirs: Color.Parse("#F0E6FA"),
        ConflictBase: Color.Parse("#EEF0F3"),
        ConflictMarker: Color.Parse("#FCEFD2"),
        ConflictUsed: Color.Parse("#E6F6EC"),
        SearchMatch: Color.Parse("#B98900"),
        MenuShadow: "0 6 16 0 #33000000",
        Lanes: [.. new[] { "#4353D8", "#1C8655", "#A16A1B", "#C93A46", "#1D7EA6", "#8048C7", "#B54A8C", "#59811C" }.Select(Color.Parse)]);

    /// <summary>
    /// The height of a menu that fits the window (phase 3's 4a): 8 + 24 + 26 × items + 9 ×
    /// separators (<see cref="WindowLayout.MenuHeight"/>), for example 232 for 7 items and 2 separators.
    /// </summary>
    public static double MenuHeight(int items, int separators) => WindowLayout.MenuHeight(items, separators);

    /// <summary>
    /// One commit row of the graph as the report's tables write it: its subject, the lane of its
    /// node and the node's lane colour number, its labels in drawing order (" ✓" after the branch
    /// HEAD is on, "HEAD" for a detached HEAD), and what the report states besides: its short SHA,
    /// whether it is a merge commit, whether it is a stash (a ring), its date as the Date column
    /// shows it, and the other lanes that end in its node.
    /// </summary>
    public sealed record CommitRow(
        string Message,
        int Lane,
        int Colour,
        IReadOnlyList<string> Labels,
        string? ShortSha = null,
        bool? Merge = null,
        bool Stash = false,
        string? Date = null,
        IReadOnlyList<int>? Ends = null)
    {
        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"{(ShortSha is null ? string.Empty : ShortSha + " ")}\"{Message}\" lane {Lane} colour {Colour}{(Merge == true ? " merge" : string.Empty)}{(Stash ? " stash" : string.Empty)} [{string.Join(", ", Labels)}]{(Date is null ? string.Empty : " " + Date)}{(Ends is null ? string.Empty : " ends [" + string.Join(", ", Ends) + "]")}");
    }

    /// <summary>A commit row (<see cref="CommitRow"/>) with only what every table states.</summary>
    public static CommitRow Row(string message, int lane, int colour, params string[] labels) => new(message, lane, colour, labels);

    /// <summary>The label of the branch HEAD is on, as the report writes it: its name and " ✓".</summary>
    public static string Current(string branch) => branch + CurrentMark;

    /// <summary>What the report writes after the branch HEAD is on (drawn as a check mark).</summary>
    public const string CurrentMark = " ✓";

    /// <summary>The working-changes row as a graph shows it: its text ("Working changes", "Working changes · 1 conflict") and its ring's lane and colour.</summary>
    public sealed record WorkingChangesRow(string Text, int Lane, int Colour);

    /// <summary>
    /// One of the banner's Continue and Skip buttons as a check expects it (phase 4 design 5.1, 5.2): its
    /// label, whether it is enabled, and its tooltip (a disabled one's reason; null when the check
    /// states none).
    /// </summary>
    public sealed record BannerButton(string Text, bool Enabled = true, string? ToolTip = null)
    {
        /// <summary>Continue, enabled.</summary>
        public static BannerButton Continue { get; } = new(ContinueText);

        /// <summary>Skip, enabled.</summary>
        public static BannerButton Skip { get; } = new(SkipText);

        /// <summary>Continue, disabled with <paramref name="reason"/> as its tooltip.</summary>
        public static BannerButton ContinueDisabled(string reason) => new(ContinueText, false, reason);

        /// <summary>Skip, disabled with <paramref name="reason"/> as its tooltip.</summary>
        public static BannerButton SkipDisabled(string reason) => new(SkipText, false, reason);
    }

    /// <summary>
    /// Where the banner's buttons stand (phase 4 design 5.2, amending D122): beside the text, right of it,
    /// while the text column keeps at least 240 px; otherwise on a row of their own under the text,
    /// right-aligned.
    /// </summary>
    public enum BannerButtonPlacement
    {
        BesideText,
        OwnRow,
    }

    /// <summary>The least width the banner's text keeps while its buttons stand beside it (phase 4 design 5.2).</summary>
    public const double BannerTextMinWidth = 240;

    /// <summary>The gap between two of the banner's buttons (phase 4 design 5.2).</summary>
    public const double BannerButtonGap = 6;

    /// <summary>The gap between the banner's last button and its right edge (phase 4 design 5.2).</summary>
    public const double BannerButtonRightInset = 8;

    /// <summary>The height of the banner's buttons (phase 4 design 5.2).</summary>
    public const double BannerButtonHeight = 26;

    /// <summary>
    /// Whether the git on this machine is 2.36, the one the report's quoted git texts come from
    /// (phase 3's "Git's own texts"): only then do the checks assert them word for word. On any
    /// other git (CI runs 2.55) they assert the classifying "key" line and that the dialog shows
    /// <c>GitOutputCleaner.Clean</c> of the failed call's own output.
    /// </summary>
    public static async Task<bool> IsReportGitAsync() => await MachineGitVersionAsync().ConfigureAwait(false) is { Major: 2, Minor: 36 };

    /// <summary>The version of the git on this machine, read once with <c>git --version</c>; null when its output does not parse.</summary>
    public static Task<GitVersion?> MachineGitVersionAsync() => MachineGit.Value;

    private static readonly Lazy<Task<GitVersion?>> MachineGit = new(async () =>
    {
        var path = GitLocator.FindExecutable(GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("Git was not found on this machine.");
        var result = await new GitRunner(path).RunAsync(new GitCommand("--version"), CancellationToken.None).ConfigureAwait(false);
        return GitVersion.TryParse(result.StandardOutput, out var version) ? version : null;
    });

    /// <summary>
    /// The kinds of backup the app saves (phase 4 design section 9): D83's and D127's (<c>reset</c>,
    /// <c>detached</c>, <c>branch</c>, <c>remote-branch</c>, <c>remote</c>, <c>force-push</c>,
    /// <c>tag</c>, <c>remote-tag</c>, <c>stash</c>, <c>abort</c>), D67's discard snapshots
    /// (<c>discard</c>), phase 4's <c>rebase</c> (a rebase's old tip) and <c>skip</c>, and phase
    /// 5's <c>tag-undone</c> (the object of a tag whose creation was undone, design 7.8, D171).
    /// </summary>
    public static IReadOnlySet<string> BackupKinds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "reset", "detached", "branch", "remote-branch", "remote", "force-push", "tag", "tag-undone", "remote-tag", "stash", "abort", "discard", "rebase", "skip",
    };

    /// <summary>
    /// A backup ref's name taken apart (phase 4 design section 9, D67, D83):
    /// <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;UTC time&gt;[-&lt;n&gt;][-changes | /&lt;name&gt;]</c>,
    /// for example <c>rebase-20260102T090000000Z/feature/topic</c> or
    /// <c>skip-20260102T090000000Z-changes</c>. The refs with the same kind, time and number are
    /// one backup (<see cref="Stem"/>). The harnesses' backup clock names the backups a check makes
    /// one second apart from 2026-01-02 09:00:01 UTC (design 2.3), the same in every run.
    /// </summary>
    /// <param name="Kind">One of <see cref="BackupKinds"/>: "rebase", "skip", "abort", "reset", …</param>
    /// <param name="Time">The operation's UTC time.</param>
    /// <param name="Number">The number git's ref names needed to tell two backups of one millisecond apart, or null.</param>
    /// <param name="IsChanges">Whether it is the snapshot of changed files that goes with the backup of the same stem.</param>
    /// <param name="Name">What comes after the stem's "/" (a branch's or tag's name), or null.</param>
    public sealed record BackupRef(string Kind, DateTimeOffset Time, int? Number, bool IsChanges, string? Name)
    {
        /// <summary>The prefix of every backup ref.</summary>
        public const string Prefix = "refs/visualcommit/backup/";

        /// <summary>What names the backup this ref belongs to: <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>, as the clean-up's log line names it (phase 4 design 9).</summary>
        public string Stem => Kind + "-" + Time.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture)
            + (Number is { } number ? "-" + number.ToString(CultureInfo.InvariantCulture) : string.Empty);

        /// <summary>Takes a full backup ref name apart; throws when it is not one, or its kind is none of <see cref="BackupKinds"/>.</summary>
        public static BackupRef Parse(string fullName)
        {
            ArgumentNullException.ThrowIfNull(fullName);
            var match = BackupRefPattern().Match(fullName);
            if (!match.Success)
            {
                throw new FormatException($"\"{fullName}\" is not a backup ref name ({Prefix}<kind>-<UTC time>...).");
            }

            var kind = match.Groups["kind"].Value;
            if (!BackupKinds.Contains(kind))
            {
                throw new FormatException($"\"{fullName}\" is a backup of the kind \"{kind}\", which the app does not save (phase 4 design section 9, phase 5 design 7.8).");
            }

            var time = DateTimeOffset.ParseExact(match.Groups["time"].Value, "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            return new BackupRef(
                kind,
                time,
                match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : null,
                match.Groups["changes"].Success,
                match.Groups["name"].Success ? match.Groups["name"].Value : null);
        }
    }

    /// <summary>Phase 4 design section 9's pattern, after the prefix: the kind (words joined by hyphens), the time, an optional number, then "-changes" or "/name".</summary>
    [GeneratedRegex(@"^refs/visualcommit/backup/(?<kind>[a-z]+(?:-[a-z]+)*)-(?<time>\d{8}T\d{9}Z)(?:-(?<n>\d+))?(?:(?<changes>-changes)|/(?<name>.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupRefPattern();
}
