using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>The editors on this computer (docs/phase-6-design.md 12.3): what the settings screen offers.</summary>
public interface IEditorCatalog
{
    /// <summary>The known editors whose program is found, in the catalogue's order; off the UI thread.</summary>
    Task<IReadOnlyList<FoundEditor>> FindAsync(CancellationToken cancellationToken);

    /// <summary>The catalogue's entry <paramref name="id"/>, or null.</summary>
    KnownEditor? Known(string id);
}

/// <summary>
/// The editors on this computer (docs/phase-6-design.md 12.3): each known editor of the platform
/// (<see cref="KnownEditors"/>) whose program exists at one of its candidates, about 40 to 60 file
/// checks and three folder listings, never on the UI thread; no registry and no <c>PATH</c> on
/// Windows. A known editor whose stored program is gone and that is found under its id elsewhere
/// (a JetBrains update renames its folder) is updated in the setting, with a log line. It backs the
/// machine's <see cref="ToolCatalogues"/> (<see cref="MachineCatalogues"/>); the harnesses give
/// <see cref="ToolCatalogues.Fixed"/> instead.
/// </summary>
public sealed class EditorCatalog : IEditorCatalog
{
    private readonly EditorSearchContext _context;
    private readonly ISettingsStore? _settings;
    private readonly IAppLog _log;

    /// <param name="context">The platform, its folders and its file system.</param>
    /// <param name="settings">The settings whose editor follows a known editor that moved; null for none.</param>
    /// <param name="log">The app's log.</param>
    public EditorCatalog(EditorSearchContext context, ISettingsStore? settings = null, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _settings = settings;
        _log = log ?? NullAppLog.Instance;
    }

    public Task<IReadOnlyList<FoundEditor>> FindAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Find(cancellationToken), cancellationToken);

    public KnownEditor? Known(string id) => KnownEditors.Find(id, _context.Platform);

    /// <summary>
    /// The machine's catalogues (12.9): this computer's editors, the merge tools
    /// <paramref name="git"/> knows (8.10; none, as a failed lookup, without it) and the process's
    /// <c>PATHEXT</c> on Windows. What the app uses when the session was given none.
    /// </summary>
    public static ToolCatalogues MachineCatalogues(IGitConfiguration? git, ISettingsStore? settings, IAppLog? log)
    {
        var editors = new EditorCatalog(EditorSearchContext.FromSystem(), settings, log);
        return new ToolCatalogues(
            editors.FindAsync,
            cancellationToken => git?.ReadToolHelpAsync(cancellationToken) ?? Task.FromResult(MergeToolHelp.FailedLookup),
            () => OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("PATHEXT") : null);
    }

    /// <summary>
    /// The found editor git's editor names (12.3), or null: <c>core.editor</c>'s first token,
    /// unquoted, when it is a found editor's program (ignoring case on Windows). git's editor is a
    /// mark only, never run.
    /// </summary>
    public static FoundEditor? GitEditor(string? coreEditor, IReadOnlyList<FoundEditor> found, EditorPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(found);
        if (FirstToken(coreEditor) is not { Length: > 0 } program)
        {
            return null;
        }

        var comparison = platform == EditorPlatform.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalized = Normalize(program, platform);
        return found.FirstOrDefault(editor => string.Equals(Normalize(editor.Program, platform), normalized, comparison));
    }

    private IReadOnlyList<FoundEditor> Find(CancellationToken cancellationToken)
    {
        var found = new List<FoundEditor>();
        foreach (var editor in KnownEditors.For(_context.Platform))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (KnownEditors.FindProgram(editor, _context) is { } program)
            {
                found.Add(new FoundEditor(editor.Id, editor.Name, program));
            }
        }

        FollowMovedEditor(found);
        return found;
    }

    /// <summary>A known editor whose stored program is gone and that is found elsewhere under its id: the setting follows it (12.3).</summary>
    private void FollowMovedEditor(IReadOnlyList<FoundEditor> found)
    {
        if (_settings?.Current.Editor is not { } stored || stored.Id == KnownEditors.CustomId)
        {
            return;
        }

        if (found.FirstOrDefault(editor => editor.Id == stored.Id) is not { } now
            || string.Equals(now.Program, stored.Program, StringComparison.Ordinal)
            || _context.FileExists(stored.Program))
        {
            return;
        }

        _settings.Update(settings => settings.Editor == stored ? settings with { Editor = stored with { Program = now.Program } } : settings);
        _log.Info($"{now.Name} moved to {now.Program}; the editor setting follows it.");
    }

    /// <summary>A shell command line's first word without its quotes (<c>"C:\Program Files (x86)\Notepad++\notepad++.exe" -multiInst</c>).</summary>
    private static string? FirstToken(string? commandLine)
    {
        var text = commandLine?.TrimStart();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text[0] is '"' or '\'')
        {
            var close = text.IndexOf(text[0], 1);
            return close < 0 ? text[1..] : text[1..close];
        }

        var end = text.IndexOfAny([' ', '\t']);
        return end < 0 ? text : text[..end];
    }

    private static string Normalize(string path, EditorPlatform platform) =>
        platform == EditorPlatform.Windows ? path.Replace('/', '\\') : path;
}
