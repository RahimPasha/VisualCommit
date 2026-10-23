using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The repository whose own settings the settings screen shows in "This repository"
/// (docs/phase-6-design.md 7.4, 8.3): the active tab's, fixed while the screen is open.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>The repository's name, as its tab is titled.</summary>
    string Name { get; }

    /// <summary>Its working tree's folder, where the repository read and the identity read run (8.1, 8.6).</summary>
    string Path { get; }

    /// <summary>Its <c>.git/config</c> (the common folder's <c>config</c>): the file of local scope the app writes (8.2).</summary>
    string ConfigFile { get; }

    /// <summary>The branch HEAD is on, or null while it is detached or unborn (no in-effect line then, 8.7).</summary>
    string? Branch { get; }

    /// <summary>Pull's own reader of its settings for <paramref name="branch"/> (8.9): what <c>PullInEffect</c> says.</summary>
    Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>This repository's writes, one operation of its queue (8.3); <paramref name="cancellation"/> drops it while it waits.</summary>
    Task<OperationResult> SaveAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellation);

    /// <summary>The operation the repository's queue runs now, or null.</summary>
    OperationSnapshot? Running { get; }

    /// <summary>Raised on any thread when the queue's running or waiting operations change.</summary>
    event EventHandler? QueueChanged;
}

/// <summary>One line under a field of the Git section (8.4, 8.6): its text, its tooltip and help text (the full path of the file it names), and whether it warns.</summary>
/// <param name="Text">The line.</param>
/// <param name="ToolTip">The full path of the file the line names, or null.</param>
/// <param name="IsWarning">A "Still" notice, in <c>VcWarningBrush</c>: the dialog stays open (7.8 step 8).</param>
public sealed record GitNotice(string Text, string? ToolTip = null, bool IsWarning = false);

/// <summary>
/// The settings screen's Git section (docs/phase-6-design.md 7.4, section 8): the identity, the
/// pull mode and the merge tool, global and "This repository" (D190, D200). The global scope's
/// fields come from the outside read, the repository scope's from the repository read (8.1, 8.2),
/// which run with the identity read and Pull's reader when the screen opens and again after every
/// Save (SD29); a scope switch reads nothing. Only changed keys are written (8.3), and the read back
/// compares each within the scope written (8.4, SD30).
/// </summary>
public sealed partial class GitSettingsViewModel : ObservableObject
{
    /// <summary>The tools that need a terminal, which the app cannot run (8.10, D149).</summary>
    public static IReadOnlySet<string> TerminalTools { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "vimdiff", "vimdiff1", "vimdiff2", "vimdiff3", "nvimdiff", "nvimdiff1", "nvimdiff2", "nvimdiff3", "emerge",
    };

    private readonly IGitConfiguration? _git;
    private readonly ISettingsRepository? _repository;
    private readonly Func<CancellationToken, Task<MergeToolHelp>> _mergeTools;
    private readonly Func<DialogViewModel<string?>, Task<string?>> _showPick;
    private readonly Func<string, bool> _fileExists;
    private readonly IAppLog _log;
    private GitConfigRead? _outside;
    private GitConfigRead? _inRepository;
    private MergeToolHelp? _tools;
    private IReadOnlySet<string> _pathFoundTools = new HashSet<string>(StringComparer.Ordinal);

    /// <param name="git">Git's configuration (8.1); null when there is none, which shows as no usable git.</param>
    /// <param name="repository">The active tab's repository, or null on an empty tab, a dashboard and while its refs are unread.</param>
    /// <param name="mergeTools">The session's catalogue of merge tools (8.10, 12.9).</param>
    /// <param name="showPick">Shows a Pick dialog over the screen and returns its answer.</param>
    /// <param name="log">The app's log.</param>
    /// <param name="fileExists">Whether a file exists, called off the UI thread only (a merge tool's <c>path</c>, 8.10); null for the disk.</param>
    public GitSettingsViewModel(
        IGitConfiguration? git,
        ISettingsRepository? repository,
        Func<CancellationToken, Task<MergeToolHelp>> mergeTools,
        Func<DialogViewModel<string?>, Task<string?>> showPick,
        IAppLog? log = null,
        Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(mergeTools);
        ArgumentNullException.ThrowIfNull(showPick);
        _git = git;
        _repository = repository;
        _mergeTools = mergeTools;
        _showPick = showPick;
        _fileExists = fileExists ?? File.Exists;
        _log = log ?? NullAppLog.Instance;
        Global = new GitScopeViewModel(this, GitConfigScope.Global);
        Repository = new GitScopeViewModel(this, GitConfigScope.Local);
        IsReading = true;
    }

    /// <summary>The global scope's fields.</summary>
    public GitScopeViewModel Global { get; }

    /// <summary>"This repository"'s fields.</summary>
    public GitScopeViewModel Repository { get; }

    /// <summary>The repository's name, or null without one.</summary>
    public string? RepositoryName => _repository?.Name;

    /// <summary>The branch whose pull the repository scope speaks of, or null.</summary>
    public string? Branch => _repository?.Branch;

    /// <summary><c>GitReading</c>: 8.1's reads run (about 0.1 s); nothing editable meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFields))]
    public partial bool IsReading { get; private set; }

    /// <summary><c>GitReadError</c>, or null: <c>Git's configuration could not be read:</c> and git's first <c>fatal:</c> or <c>error:</c> line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFields))]
    public partial string? ReadError { get; private set; }

    /// <summary><c>GitUnavailable</c>, or null: <c>GitAccess</c>'s sentence and <c> Git's own settings need it.</c></summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFields))]
    public partial string? Unavailable { get; private set; }

    /// <summary>The fields show: the reads have ended and succeeded.</summary>
    public bool ShowsFields => !IsReading && ReadError is null && Unavailable is null;

    /// <summary>The scope switch shows "This repository" (D200: the section always opens on the global scope).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown), nameof(IsGlobalScope), nameof(Caption), nameof(CaptionToolTip))]
    public partial bool IsRepositoryScope { get; private set; }

    public bool IsGlobalScope => !IsRepositoryScope;

    /// <summary>The scope's fields that show.</summary>
    public GitScopeViewModel Shown => IsRepositoryScope ? Repository : Global;

    /// <summary><c>GitScopeRepository</c> is enabled: a repository's tab is active (7.4, D90).</summary>
    public bool CanUseRepositoryScope => _repository is not null;

    /// <summary><c>GitScopeRepository</c>'s tooltip: <c>{name} — {folder}</c>, or why it is disabled.</summary>
    public string RepositoryScopeToolTip => _repository is { } repository ? $"{repository.Name} — {repository.Path}" : Texts.ScopeRepositoryDisabled;

    /// <summary><c>GitScopeCaption</c>.</summary>
    public string Caption => IsRepositoryScope ? Texts.CaptionRepository : Texts.CaptionGlobal;

    /// <summary>The global caption's tooltip and help text: the global file's full path (8.2); null in the repository scope.</summary>
    /// <remarks>
    /// Null until the outside read has run: <see cref="IGitConfiguration.GlobalFile"/> checks files on
    /// a home share that may not answer, which only that read, off the UI thread, may do first (8.2).
    /// </remarks>
    public string? CaptionToolTip => IsRepositoryScope || _outside is null || _git is null || _git.GlobalFile is not { Length: > 0 } file ? null : file;

    /// <summary>The merge tools' list is not there yet (8.10): <c>Choose…</c> waits, disabled.</summary>
    [ObservableProperty]
    public partial bool IsLookingForMergeTools { get; private set; } = true;

    /// <summary><c>core.editor</c> as 8.1's read has it (the repository read's when there is one): the editor's mark (12.3).</summary>
    public string? CoreEditor => (_inRepository ?? _outside)?.Winner("core.editor")?.Value;

    /// <summary>Raised after the reads have filled the fields (the opening's and every read back's).</summary>
    public event EventHandler? Loaded;

    /// <summary>A field wants the keyboard focus (the first wrong one after a refused Save), by automation id.</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>Whether either scope shows an error (after a Save, SD9).</summary>
    public bool HasError => Global.HasError || Repository.HasError;

    /// <summary>The merge tools git knows, as the catalogue answered; null until then.</summary>
    internal MergeToolHelp? Tools => _tools;

    /// <summary>The tools of the not-found list whose <c>mergetool.&lt;tool&gt;.path</c> names an existing file (8.10).</summary>
    internal IReadOnlySet<string> PathFoundTools => _pathFoundTools;

    /// <summary>The outside read, or null before it.</summary>
    internal GitConfigRead? OutsideRead => _outside;

    /// <summary>The repository read, or null without a repository.</summary>
    internal GitConfigRead? RepositoryRead => _inRepository;

    /// <summary>The file names and the notices' rules (8.2, 8.4).</summary>
    internal GitFileNames Files => new(_git?.GlobalFile ?? string.Empty, _git?.XdgFile ?? string.Empty, _git?.Variable("XDG_CONFIG_HOME") is not null, _repository?.ConfigFile);

    /// <summary>A variable of the environment git gets (8.6's notices).</summary>
    internal string? Variable(string name) => _git?.Variable(name);

    /// <summary>
    /// The reads of the screen's opening (8.1, 8.6, 8.7, SD29), side by side: the outside read, and
    /// with a repository the repository read, its identity and, on a branch, Pull's reader. The
    /// merge tools' list (8.10) is asked for at the same time and does not hold the fields, nor
    /// this task: <see cref="MergeToolsLoaded"/> completes when it is there.
    /// </summary>
    public async Task LoadAsync()
    {
        var reads = ReadAllAsync();
        MergeToolsLoaded = LoadMergeToolsAsync(reads);
        await reads;
    }

    /// <summary>Completes when the merge tools' list is there (8.10), or failed; <c>Choose…</c> is enabled then.</summary>
    public Task MergeToolsLoaded { get; private set; } = Task.CompletedTask;

    /// <summary>Shows the global scope.</summary>
    [RelayCommand]
    private void ShowGlobal() => IsRepositoryScope = false;

    /// <summary>Shows "This repository" (enabled only with a repository).</summary>
    [RelayCommand(CanExecute = nameof(CanUseRepositoryScope))]
    private void ShowRepository() => IsRepositoryScope = true;

    /// <summary>Shows every error of both scopes, edited or not (7.8 step 1); shows the first wrong scope and returns whether there is one.</summary>
    public bool ShowErrors()
    {
        var global = Global.ShowErrors();
        var repository = Repository.ShowErrors();
        OnPropertyChanged(nameof(HasError));
        if (global)
        {
            IsRepositoryScope = false;
        }
        else if (repository)
        {
            IsRepositoryScope = true;
        }

        return global || repository;
    }

    /// <summary>Focuses the first wrong field of the shown scope (a refused Save).</summary>
    public void FocusFirstError()
    {
        if (Shown.FirstErrorField is { } field)
        {
            FocusRequested?.Invoke(this, field);
        }
    }

    /// <summary>The global scope's changed keys, in 8.3's order.</summary>
    public IReadOnlyList<GitConfigWrite> GlobalWrites() => ShowsFields ? Global.Writes() : [];

    /// <summary>"This repository"'s changed keys, in 8.3's order, <c>branch.&lt;b&gt;.rebase</c> last.</summary>
    public IReadOnlyList<GitConfigWrite> RepositoryWrites() => ShowsFields && _repository is not null ? Repository.Writes() : [];

    /// <summary>
    /// Writes the global keys through the app-level writer (8.3), one at a time, stopping at the
    /// first failure, which throws <see cref="GitException"/>.
    /// </summary>
    public Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return writes.Count == 0 || _git is null ? Task.CompletedTask : _git.WriteGlobalAsync(writes);
    }

    /// <summary>This repository's writes as one operation of its queue (8.3, 7.8 step 5).</summary>
    public Task<OperationResult> WriteRepositoryAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return _repository is null
            ? Task.FromResult(new OperationResult(OperationOutcome.Succeeded, string.Empty, null))
            : _repository.SaveAsync(writes, cancellation);
    }

    /// <summary>
    /// The read back of a Save (7.8 step 6, 8.4): every read of the opening again, side by side;
    /// each scope's read becomes its fields' baseline, and each written key is compared within the
    /// scope written (SD30). Returns whether a changed key is still decided by another file of its
    /// scope (a "Still" notice): the dialog then stays open (7.8 step 8).
    /// </summary>
    public async Task<bool> ReadBackAsync(IReadOnlyList<GitConfigWrite> globalWritten, IReadOnlyList<GitConfigWrite> repositoryWritten)
    {
        ArgumentNullException.ThrowIfNull(globalWritten);
        ArgumentNullException.ThrowIfNull(repositoryWritten);
        await ReadAllAsync();
        if (!ShowsFields)
        {
            return false;
        }

        var still = Global.AddStillNotices(globalWritten) | Repository.AddStillNotices(repositoryWritten);
        if (still)
        {
            IsRepositoryScope = !Global.HasStillNotice && Repository.HasStillNotice;
        }

        return still;
    }

    /// <summary>
    /// <c>Choose…</c> of <paramref name="scope"/>'s merge tool: the Pick dialog <c>Choose a merge tool</c>
    /// (7.7, 8.10) over the screen, the field's value selected (SD6); nothing is written until Save.
    /// </summary>
    internal async Task ChooseMergeToolForAsync(GitScopeViewModel scope)
    {
        if (IsLookingForMergeTools)
        {
            return;
        }

        // Asked again at each opening of the Pick dialog (8.10: the kept list answers while the run
        // goes on, the run's list once it has ended), which answers at once either way.
        try
        {
            await AdoptToolsAsync(await _mergeTools(CancellationToken.None));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("The settings screen could not get the merge tools git knows again; the list it has stays.", ex);
        }

        var options = await MergeToolOptionsAsync(scope);
        var selected = scope.MergeToolValue ?? Texts.NotSet;
        var answer = await _showPick(new PickDialogViewModel(Texts.ChooseMergeToolTitle, options, selected, Texts.PickConfirm));
        if (answer is not null)
        {
            scope.MergeToolValue = answer == Texts.NotSet ? null : answer;
        }
    }

    /// <summary>
    /// The Pick dialog's options for <paramref name="scope"/> (7.7, 8.10, SD7): Not set; the tools
    /// git found and those whose path exists, but the terminal tools; the user's own
    /// (<c>mergetool.&lt;name&gt;.cmd</c>); those git knows but did not find; the current value last
    /// when none of these holds it.
    /// </summary>
    internal async Task<IReadOnlyList<PickOption>> MergeToolOptionsAsync(GitScopeViewModel scope)
    {
        var help = _tools ?? MergeToolHelp.FailedLookup;
        var read = scope.IsGlobal ? _outside : _inRepository ?? _outside;
        var userDefined = UserDefinedTools(read);
        var pathFound = await PathFoundAsync(help, read);
        var options = new List<PickOption>
        {
            new(Texts.NotSet, scope.IsGlobal ? Texts.PickNotSetGlobalDetail : Texts.PickNotSetRepositoryDetail(scope.LowerMergeTool)),
        };
        var listed = new HashSet<string>(StringComparer.Ordinal) { Texts.NotSet };

        void Add(string name, string detail)
        {
            if (listed.Add(name))
            {
                options.Add(new PickOption(name, detail));
            }
        }

        foreach (var tool in help.Found.Where(tool => !TerminalTools.Contains(tool.Name)))
        {
            Add(tool.Name, Describe(tool));
        }

        foreach (var tool in help.NotFound.Where(tool => pathFound.Contains(tool.Name) && !TerminalTools.Contains(tool.Name)))
        {
            Add(tool.Name, Describe(tool));
        }

        foreach (var name in userDefined)
        {
            Add(name, Texts.ToolOfYourOwn(name));
        }

        foreach (var tool in help.NotFound.Where(tool => !TerminalTools.Contains(tool.Name)))
        {
            Add(tool.Name, Texts.ToolNotFound);
        }

        if (scope.MergeToolValue is { } current && !listed.Contains(current))
        {
            Add(current, TerminalTools.Contains(current) ? Texts.ToolInTerminal : Texts.ToolUnknown);
        }

        return options;
    }

    /// <summary>The user's own tools of <paramref name="read"/>: every <c>mergetool.&lt;name&gt;.cmd</c>, in the read's order.</summary>
    internal static IReadOnlyList<string> UserDefinedTools(GitConfigRead? read)
    {
        var names = new List<string>();
        foreach (var value in read?.Values ?? [])
        {
            if (value.Key.StartsWith("mergetool.", StringComparison.OrdinalIgnoreCase)
                && value.Key.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                && value.Key.Length > "mergetool.".Length + ".cmd".Length)
            {
                var name = value.Key["mergetool.".Length..^".cmd".Length];
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private static string Describe(MergeTool tool) =>
        string.IsNullOrEmpty(tool.Description)
            ? Texts.ToolFound
            : tool.Description.Replace(Texts.GraphicalSession, string.Empty, StringComparison.Ordinal);

    /// <summary>The not-found tools whose <c>mergetool.&lt;tool&gt;.path</c> names an existing file, checked off the UI thread (8.10).</summary>
    private async Task<IReadOnlySet<string>> PathFoundAsync(MergeToolHelp help, GitConfigRead? read)
    {
        var paths = help.NotFound
            .Select(tool => (tool.Name, Path: read?.Winner($"mergetool.{tool.Name}.path")?.Value))
            .Where(item => !string.IsNullOrEmpty(item.Path))
            .ToList();
        if (paths.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var exists = _fileExists;
        return await Task.Run(() => (IReadOnlySet<string>)paths
            .Where(item => SafeExists(exists, item.Path!))
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal));
    }

    private static bool SafeExists(Func<string, bool> exists, string path)
    {
        try
        {
            return exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private async Task LoadMergeToolsAsync(Task reads)
    {
        MergeToolHelp tools;
        try
        {
            tools = await _mergeTools(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("The settings screen could not get the merge tools git knows.", ex);
            tools = MergeToolHelp.FailedLookup;
        }

        // The user's own tools and the tools' paths come from the reads.
        try
        {
            await reads;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("Git's configuration could not be read for the merge tools' list.", ex);
        }

        await AdoptToolsAsync(tools);
        IsLookingForMergeTools = false;
        Global.RefreshMergeTool();
        Repository.RefreshMergeTool();
    }

    /// <summary>Takes <paramref name="tools"/> as the list the screen offers, with the tools whose paths exist (8.10).</summary>
    private async Task AdoptToolsAsync(MergeToolHelp tools)
    {
        // A path set in either scope's read counts (the repository read holds the global one too).
        var found = new HashSet<string>(await PathFoundAsync(tools, _outside), StringComparer.Ordinal);
        found.UnionWith(await PathFoundAsync(tools, _inRepository));
        _tools = tools;
        _pathFoundTools = found;
        Global.RefreshMergeTool();
        Repository.RefreshMergeTool();
    }

    /// <summary>Every read of the opening (and of a read back), side by side, then the fields from them.</summary>
    private async Task ReadAllAsync()
    {
        IsReading = true;
        try
        {
            if (_git is null)
            {
                Unavailable = "Git is not working on this computer." + Texts.GitNeeded;
                return;
            }

            var outside = ReadOutsideAsync();
            var inRepository = _repository is null ? Task.FromResult<GitConfigRead?>(null) : ReadRepositoryAsync(_repository.Path);
            var identity = _repository is null ? Task.FromResult<GitIdentityRead?>(null) : ReadIdentityAsync(_repository.Path);
            var pull = _repository?.Branch is { } branch ? ReadPullAsync(branch) : Task.FromResult<(PullConfiguration?, string?)>((null, null));
            try
            {
                await Task.WhenAll((Task)outside, inRepository);
            }
            catch (GitUnavailableException ex)
            {
                Unavailable = ex.Message + Texts.GitNeeded;
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Nothing is editable when a read fails (7.4): git's own line, or what failed.
                _log.Warning("Git's configuration could not be read.", ex);
                ReadError = $"{Texts.ReadFailed} {(ex is GitException git ? FirstGitLine(git) : ex.Message)}";
                return;
            }
            finally
            {
                // The other two end too, before the fields change: no read outlives the section's.
                await Task.WhenAll(Quietly(identity), Quietly(pull));
            }

            _outside = await outside;
            _inRepository = await inRepository;
            Unavailable = null;
            ReadError = null;
            Global.Fill();
            Repository.Fill();
            Repository.FillInEffect(await identity, await pull);

            // The global file's path may be worked out now that the outside read has run (8.2).
            OnPropertyChanged(nameof(CaptionToolTip));
        }
        finally
        {
            IsReading = false;
            Loaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<GitConfigRead> ReadOutsideAsync() => await _git!.ReadAsync(null);

    private async Task<GitConfigRead?> ReadRepositoryAsync(string workingTree) => await _git!.ReadAsync(workingTree);

    private async Task<GitIdentityRead?> ReadIdentityAsync(string workingTree)
    {
        try
        {
            return await _git!.ReadIdentityAsync(workingTree);
        }
        catch (Exception ex) when (ex is GitException or GitUnavailableException or IOException)
        {
            _log.Warning("The identity git uses could not be read.", ex);
            return new GitIdentityRead(null, null, ex is GitException git ? FirstGitLine(git) : ex.Message);
        }
    }

    private async Task<(PullConfiguration? Configuration, string? Failure)> ReadPullAsync(string branch)
    {
        try
        {
            return (await _repository!.ReadPullConfigurationAsync(branch), null);
        }
        catch (Exception ex) when (ex is GitException or GitUnavailableException or IOException)
        {
            _log.Warning("Pull's settings could not be read.", ex);
            return (null, ex is GitException git ? FirstGitLine(git) : ex.Message);
        }
    }

    private static async Task Quietly(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reported by its own read.
        }
    }

    /// <summary>Git's first <c>fatal:</c> or <c>error:</c> line, or else its first line or the exception's message.</summary>
    private static string FirstGitLine(GitException exception)
    {
        var lines = (exception.StandardError ?? string.Empty).Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .ToList();
        return lines.FirstOrDefault(line => line.StartsWith("fatal:", StringComparison.Ordinal) || line.StartsWith("error:", StringComparison.Ordinal))
            ?? lines.FirstOrDefault()
            ?? exception.Message;
    }
}

/// <summary>How 8.2 names a file of the reads, by what it is, never by its path.</summary>
/// <param name="GlobalFile">The file <c>--global</c> writes; empty for none.</param>
/// <param name="XdgFile">The XDG file; empty for none.</param>
/// <param name="XdgVariableSet"><c>XDG_CONFIG_HOME</c> is set, so the XDG file is named after it.</param>
/// <param name="LocalFile">The repository's <c>.git/config</c>, or null without a repository.</param>
public sealed record GitFileNames(string GlobalFile, string XdgFile, bool XdgVariableSet, string? LocalFile)
{
    /// <summary>The name of <paramref name="value"/>'s file (8.2's table).</summary>
    public string NameOf(GitConfigValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Scope switch
        {
            GitConfigScope.Command => Texts.CommandScopeName,
            GitConfigScope.System => Texts.SystemFileName,
            GitConfigScope.Worktree => Texts.WorktreeFileName,
            GitConfigScope.Global when IsGlobalFile(value.Origin) => Texts.GlobalFileName,
            GitConfigScope.Global when SamePath(value.Origin, XdgFile) => XdgVariableSet ? Texts.XdgUnderVariable : Texts.XdgInHome,
            GitConfigScope.Global => Texts.Included(FileNameOf(value.Origin)),
            _ when IsLocalFile(value.Origin) => Texts.LocalFileName,
            _ => Texts.IncludedByLocal(FileNameOf(value.Origin)),
        };
    }

    /// <summary>Whether <paramref name="origin"/> is the file the global writes go to.</summary>
    public bool IsGlobalFile(string? origin) => SamePath(origin, GlobalFile);

    /// <summary>Whether <paramref name="origin"/> is the repository's <c>.git/config</c>.</summary>
    public bool IsLocalFile(string? origin) => SamePath(origin, LocalFile);

    /// <summary>Whether <paramref name="value"/> lies in the file its scope's writes go to.</summary>
    public bool IsWrittenFile(GitConfigValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Scope == GitConfigScope.Global ? IsGlobalFile(value.Origin) : value.Scope == GitConfigScope.Local && IsLocalFile(value.Origin);
    }

    /// <summary>
    /// Whether two paths name one file: their full forms, ignoring case on Windows and macOS, and on
    /// macOS without the <c>/private</c> that git may add before <c>/var</c> and <c>/tmp</c>.
    /// </summary>
    public static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        static string Normal(string path)
        {
            string full;
            try
            {
                full = System.IO.Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                full = path;
            }

            full = System.IO.Path.TrimEndingDirectorySeparator(full.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar));
            return OperatingSystem.IsMacOS() && full.StartsWith("/private/", StringComparison.Ordinal) ? full["/private".Length..] : full;
        }

        return string.Equals(Normal(a), Normal(b), OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string FileNameOf(string? origin) =>
        origin is null ? string.Empty : origin[(origin.LastIndexOfAny(['\\', '/']) + 1)..];
}

/// <summary>
/// One scope's fields of the Git section (7.4, 8.2 to 8.8, 8.10): the identity, the pull mode and
/// the merge tool, each with its baseline from the scope's read, its placeholder and its notices;
/// "This repository" also has the identity and the pull in effect and <c>branch.&lt;b&gt;.rebase</c>.
/// </summary>
public sealed partial class GitScopeViewModel : ObservableObject
{
    private const string NameKey = "user.name";
    private const string EmailKey = "user.email";
    private const string MergeToolKey = "merge.tool";

    private readonly GitSettingsViewModel _owner;
    private string? _nameBaseline;
    private string? _emailBaseline;
    private string? _mergeToolBaseline;
    private PullModeReading _pullBaseline = new(PullMode.NotSet, null, null);
    private bool _nameEdited;
    private bool _emailEdited;
    private bool _showErrors;
    private bool _filling;

    internal GitScopeViewModel(GitSettingsViewModel owner, GitConfigScope scope)
    {
        _owner = owner;
        Scope = scope;
    }

    /// <summary><see cref="GitConfigScope.Global"/> or <see cref="GitConfigScope.Local"/>.</summary>
    public GitConfigScope Scope { get; }

    public bool IsGlobal => Scope == GitConfigScope.Global;

    public bool IsRepository => !IsGlobal;

    /// <summary><c>UserNameBox</c>'s text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError), nameof(HasError))]
    public partial string UserName { get; set; } = string.Empty;

    /// <summary><c>UserEmailBox</c>'s text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmailError), nameof(EmailWarning), nameof(HasError))]
    public partial string UserEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NamePlaceholder { get; private set; } = Texts.NotSet;

    [ObservableProperty]
    public partial string EmailPlaceholder { get; private set; } = Texts.NotSet;

    /// <summary><c>UserNameOrigin</c>'s lines (8.4).</summary>
    public ObservableCollection<GitNotice> NameNotices { get; } = [];

    /// <summary><c>UserEmailOrigin</c>'s lines (8.4).</summary>
    public ObservableCollection<GitNotice> EmailNotices { get; } = [];

    /// <summary><c>PullNotices</c>' lines (8.4, 8.7).</summary>
    public ObservableCollection<GitNotice> PullNotices { get; } = [];

    /// <summary><c>MergeToolNotices</c>' lines (8.4).</summary>
    public ObservableCollection<GitNotice> MergeToolNotices { get; } = [];

    /// <summary><c>UserNameError</c>, once the box was edited (D114) or after a Save.</summary>
    public string? NameError => (_nameEdited || _showErrors) ? NameRule(UserName) : null;

    /// <summary><c>UserEmailError</c>, once the box was edited or after a Save.</summary>
    public string? EmailError => (_emailEdited || _showErrors) ? EmailRule(UserEmail) : null;

    /// <summary><c>UserEmailWarning</c>: no <c>@</c>; saved anyway (8.6), and no mark on the section (SD9).</summary>
    public string? EmailWarning => (_emailEdited || _showErrors) && EmailRule(UserEmail) is null && UserEmail.Trim() is { Length: > 0 } email && !email.Contains('@', StringComparison.Ordinal)
        ? Texts.EmailWarning
        : null;

    public bool HasError => NameError is not null || EmailError is not null;

    /// <summary>The first wrong field, by automation id; null for none.</summary>
    public string? FirstErrorField => NameRule(UserName) is not null ? "UserNameBox" : EmailRule(UserEmail) is not null ? "UserEmailBox" : null;

    /// <summary>The pull radio checked: a mode, or null for <c>PullOther</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSet), nameof(IsMerge), nameof(IsMergeCommit), nameof(IsRebase), nameof(IsRebaseMerges), nameof(IsFastForwardOnly), nameof(IsOther))]
    public partial PullMode? SelectedPullMode { get; set; } = PullMode.NotSet;

    public bool IsNotSet { get => SelectedPullMode == PullMode.NotSet; set => Pick(value, PullMode.NotSet); }

    public bool IsMerge { get => SelectedPullMode == PullMode.Merge; set => Pick(value, PullMode.Merge); }

    public bool IsMergeCommit { get => SelectedPullMode == PullMode.MergeCommit; set => Pick(value, PullMode.MergeCommit); }

    public bool IsRebase { get => SelectedPullMode == PullMode.Rebase; set => Pick(value, PullMode.Rebase); }

    public bool IsRebaseMerges { get => SelectedPullMode == PullMode.RebaseMerges; set => Pick(value, PullMode.RebaseMerges); }

    public bool IsFastForwardOnly { get => SelectedPullMode == PullMode.FastForwardOnly; set => Pick(value, PullMode.FastForwardOnly); }

    /// <summary><c>PullOther</c> is checked: only while the scope's value is unusual and no mode was picked.</summary>
    public bool IsOther
    {
        get => SelectedPullMode is null;
        set
        {
            // A click on Other again goes back to the value as it is: nothing is written for it.
            if (value && ShowsOther)
            {
                SelectedPullMode = null;
            }
        }
    }

    /// <summary><c>PullOther</c> shows: the scope's value is one the six modes do not cover (8.7).</summary>
    public bool ShowsOther => _pullBaseline.Mode is null;

    /// <summary><c>PullOther</c>'s label: <c>Other: {key} = {value}</c>.</summary>
    public string? OtherText => _pullBaseline.Other;

    /// <summary><c>PullOther</c>'s second line.</summary>
    public string? OtherNote => _pullBaseline.OtherNote;

    /// <summary><c>PullNotSet</c>'s label: <c>Not set</c> (global) or <c>Same as global</c>.</summary>
    public string NotSetLabel => IsGlobal ? Texts.NotSet : Texts.PullSameAsGlobal;

    /// <summary><c>PullNotSet</c>'s tooltip (8.7).</summary>
    public string NotSetToolTip => IsGlobal ? Texts.PullNotSetGlobalTip : Texts.PullNotSetRepositoryTip;

    /// <summary>The merge tool the field holds: <c>merge.tool</c> at the scope, or null for Not set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeToolText), nameof(MergeToolStatus))]
    public partial string? MergeToolValue { get; set; }

    /// <summary>The tool a lower scope gives the repository (the repository read's global or system value), for <c>Same as global (…)</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeToolText))]
    public partial string? LowerMergeTool { get; private set; }

    /// <summary><c>MergeToolValue</c>: the tool's name, <c>Not set</c>, or in the repository scope <c>Same as global ({tool})</c> / <c>Same as global (not set)</c>.</summary>
    public string MergeToolText => MergeToolValue ?? (IsGlobal ? Texts.NotSet : Texts.MergeToolSameAsGlobal(LowerMergeTool));

    /// <summary><c>MergeToolStatus</c> (8.10), or null.</summary>
    public string? MergeToolStatus
    {
        get
        {
            if (_owner.IsLookingForMergeTools)
            {
                return Texts.MergeToolsLoading;
            }

            var tools = _owner.Tools;
            if (tools is { Failed: true, Found.Count: 0, NotFound.Count: 0 })
            {
                return Texts.MergeToolsFailed;
            }

            return MergeToolValue is { } tool
                && tools is not null
                && tools.NotFound.Any(known => known.Name == tool)
                && !_owner.PathFoundTools.Contains(tool)
                && !GitSettingsViewModel.UserDefinedTools(IsGlobal ? _owner.OutsideRead : _owner.RepositoryRead).Contains(tool)
                ? Texts.MergeToolNotFound(tool)
                : null;
        }
    }

    // "This repository" only.

    /// <summary><c>IdentityInEffect</c>: who git makes commits as here, in the app's words (8.6); null in the global scope.</summary>
    [ObservableProperty]
    public partial string? IdentityInEffect { get; private set; }

    /// <summary><c>IdentityNotices</c>' lines (8.6).</summary>
    public ObservableCollection<GitNotice> IdentityNotices { get; } = [];

    /// <summary><c>PullInEffect</c> (8.7): from Pull's own reader; null without a branch.</summary>
    [ObservableProperty]
    public partial GitNotice? PullInEffect { get; private set; }

    /// <summary><c>BranchRebaseNotice</c> (8.8), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BranchRebaseNoticeText))]
    public partial GitNotice? BranchRebase { get; private set; }

    /// <summary>The <c>branch.&lt;b&gt;.rebase</c> entry is in <c>.git/config</c>: <c>BranchRebaseButton</c> shows.</summary>
    [ObservableProperty]
    public partial bool ShowsBranchRebaseButton { get; private set; }

    /// <summary>Save removes <c>branch.&lt;b&gt;.rebase</c> (8.8).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BranchRebaseNoticeText), nameof(BranchRebaseButtonText))]
    public partial bool IsBranchRebaseMarked { get; private set; }

    /// <summary><c>BranchRebaseNotice</c>'s text.</summary>
    public string? BranchRebaseNoticeText => BranchRebase is null ? null
        : IsBranchRebaseMarked ? Texts.BranchRebaseMarked(_owner.Branch!) : BranchRebase.Text;

    /// <summary><c>BranchRebaseButton</c>'s label.</summary>
    public string BranchRebaseButtonText => IsBranchRebaseMarked ? Texts.KeepBranchSetting(_owner.Branch ?? string.Empty) : Texts.UseRepositorySetting;

    /// <summary>Whether the scope has a "Still" notice after the last Save (8.4).</summary>
    public bool HasStillNotice => NameNotices.Concat(EmailNotices).Concat(PullNotices).Concat(MergeToolNotices).Any(notice => notice.IsWarning);

    /// <summary><c>BranchRebaseButton</c>: marks the key for removal, or takes the mark back (8.8).</summary>
    [RelayCommand]
    private void ToggleBranchRebase() => IsBranchRebaseMarked = !IsBranchRebaseMarked;

    /// <summary>Shows every error, edited or not (7.8 step 1); returns whether there is one.</summary>
    internal bool ShowErrors()
    {
        _showErrors = true;
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(EmailError));
        OnPropertyChanged(nameof(EmailWarning));
        OnPropertyChanged(nameof(HasError));
        return FirstErrorField is not null;
    }

    /// <summary>The scope's changed keys, in 8.3's order: an untouched field writes nothing.</summary>
    internal IReadOnlyList<GitConfigWrite> Writes()
    {
        // A box is compared as it would be written, trimmed and empty as none, with the value read the
        // same way: git gives "name =" as an empty value and keeps a value's spaces, which an untouched
        // box must never rewrite (8.3: only changed keys are written).
        var writes = new List<GitConfigWrite>();
        var name = Normal(UserName);
        if (name != Normal(_nameBaseline))
        {
            writes.Add(new GitConfigWrite(NameKey, name));
        }

        var email = Normal(UserEmail);
        if (email != Normal(_emailBaseline))
        {
            writes.Add(new GitConfigWrite(EmailKey, email));
        }

        // Picking any of the six replaces both keys at the scope (8.7); the mode read is never rewritten.
        if (SelectedPullMode is { } mode && mode != _pullBaseline.Mode)
        {
            writes.AddRange(PullModes.Writes(mode));
        }

        if (MergeToolValue != _mergeToolBaseline)
        {
            writes.Add(new GitConfigWrite(MergeToolKey, MergeToolValue));
        }

        if (IsRepository && IsBranchRebaseMarked && _owner.Branch is { } branch)
        {
            writes.Add(new GitConfigWrite($"branch.{branch}.rebase", null));
        }

        return writes;
    }

    /// <summary>The scope's fields, placeholders and notices from the reads, as baselines (8.2, 8.4).</summary>
    internal void Fill()
    {
        _filling = true;
        try
        {
            var read = ScopeRead();
            var files = _owner.Files;
            _nameBaseline = LastOf(read, NameKey)?.Value;
            _emailBaseline = LastOf(read, EmailKey)?.Value;
            // An empty merge.tool names no tool: it shows as Not set (git's mergetool finds none either).
            _mergeToolBaseline = Normal(LastOf(read, MergeToolKey)?.Value);
            UserName = _nameBaseline ?? string.Empty;
            UserEmail = _emailBaseline ?? string.Empty;
            MergeToolValue = _mergeToolBaseline;
            _nameEdited = false;
            _emailEdited = false;
            _showErrors = false;
            IsBranchRebaseMarked = false;

            var rebase = LastOf(read, PullModes.RebaseKey);
            var fastForward = LastOf(read, PullModes.FastForwardKey);
            _pullBaseline = PullModes.Read(rebase?.Value, rebase is { Value: null }, fastForward?.Value, fastForward is { Value: null });
            SelectedPullMode = _pullBaseline.Mode;

            NamePlaceholder = Placeholder(NameKey);
            EmailPlaceholder = Placeholder(EmailKey);
            LowerMergeTool = IsGlobal ? null : Normal(LowerOf(_owner.RepositoryRead, MergeToolKey)?.Value);

            Replace(NameNotices, Notices(NameKey, value => value, files));
            Replace(EmailNotices, Notices(EmailKey, value => value, files));
            Replace(MergeToolNotices, Notices(MergeToolKey, value => value, files));
            Replace(PullNotices, PullNoticesOf(files));
            FillBranchRebase(files);
        }
        finally
        {
            _filling = false;
        }

        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(EmailError));
        OnPropertyChanged(nameof(EmailWarning));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowsOther));
        OnPropertyChanged(nameof(OtherText));
        OnPropertyChanged(nameof(OtherNote));
        OnPropertyChanged(nameof(IsOther));
        RefreshMergeTool();
    }

    /// <summary>"This repository"'s identity and pull in effect (8.6, 8.7) from the identity read and Pull's reader.</summary>
    internal void FillInEffect(GitIdentityRead? identity, (PullConfiguration? Configuration, string? Failure) pull)
    {
        if (IsGlobal || _owner.RepositoryRead is not { } read)
        {
            return;
        }

        IdentityInEffect = identity is null ? null : InEffectText(identity);
        Replace(IdentityNotices, IdentityNoticesOf(read));
        PullInEffect = _owner.Branch is { } branch ? PullInEffectOf(branch, pull.Configuration, pull.Failure, read) : null;
    }

    /// <summary>
    /// After a Save (8.4, SD30): for each key written at this scope, the scope's last entry in its
    /// read against what was written; a "Still" notice where another file of the scope still decides
    /// it. Returns whether any was added.
    /// </summary>
    internal bool AddStillNotices(IReadOnlyList<GitConfigWrite> written)
    {
        var read = ScopeRead();
        var files = _owner.Files;
        var any = false;
        foreach (var write in written)
        {
            var last = LastOf(read, write.Key);
            GitNotice? still = null;
            var isPull = write.Key is PullModes.RebaseKey or PullModes.FastForwardKey || write.Key.StartsWith("branch.", StringComparison.Ordinal);
            string Shown(string? value) => isPull ? $"{write.Key} = {value ?? "true"}" : value ?? "true";
            if (write.Value is null && last is not null)
            {
                still = new GitNotice(Texts.StillSet(Shown(last.Value), files.NameOf(last)), last.Origin, IsWarning: true);
            }
            else if (write.Value is not null && last is not null && !string.Equals(last.Value, write.Value, StringComparison.Ordinal))
            {
                still = new GitNotice(Texts.StillLater(Shown(last.Value), files.NameOf(last)), last.Origin, IsWarning: true);
            }

            if (still is null)
            {
                continue;
            }

            any = true;
            var list = write.Key switch
            {
                NameKey => NameNotices,
                EmailKey => EmailNotices,
                MergeToolKey => MergeToolNotices,
                _ => PullNotices,
            };
            list.Insert(0, still);
        }

        OnPropertyChanged(nameof(HasStillNotice));
        return any;
    }

    /// <summary><c>MergeToolChooseButton</c>'s tooltip while the list is not there (8.10).</summary>
    public string? MergeToolChooseToolTip => _owner.IsLookingForMergeTools ? Texts.MergeToolsLoading : null;

    /// <summary><c>MergeToolChooseButton</c>: the Pick dialog over the screen; disabled until the list is there (8.10).</summary>
    [RelayCommand(CanExecute = nameof(CanChooseMergeTool), AllowConcurrentExecutions = true)]
    private Task ChooseMergeToolAsync() => _owner.ChooseMergeToolForAsync(this);

    private bool CanChooseMergeTool() => !_owner.IsLookingForMergeTools;

    /// <summary>The merge tool's texts after the list or the reads changed.</summary>
    internal void RefreshMergeTool()
    {
        OnPropertyChanged(nameof(MergeToolText));
        OnPropertyChanged(nameof(MergeToolStatus));
        OnPropertyChanged(nameof(MergeToolChooseToolTip));
        ChooseMergeToolCommand.NotifyCanExecuteChanged();
    }

    partial void OnUserNameChanged(string value)
    {
        if (!_filling)
        {
            _nameEdited = true;
        }
    }

    partial void OnUserEmailChanged(string value)
    {
        if (!_filling)
        {
            _emailEdited = true;
        }
    }

    private void Pick(bool value, PullMode mode)
    {
        if (value)
        {
            SelectedPullMode = mode;
        }
    }

    /// <summary>8.6's rules for a name: <c>&lt;</c>, <c>&gt;</c>, a line break, or only punctuation; empty unsets.</summary>
    public static string? NameRule(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.IndexOfAny(['<', '>', '\r', '\n']) >= 0)
        {
            return Texts.NameRefused;
        }

        return trimmed.Length > 0 && trimmed.All(c => c is '.' or ',' or ':' or ';' or '"' or '\\' or '\'' or ' ')
            ? Texts.NamePunctuation
            : null;
    }

    /// <summary>8.6's rule for an email: <c>&lt;</c>, <c>&gt;</c> or a line break; empty unsets.</summary>
    public static string? EmailRule(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().IndexOfAny(['<', '>', '\r', '\n']) >= 0 ? Texts.EmailRefused : null;
    }

    /// <summary>
    /// <c>IdentityInEffect</c>'s text (8.6): who git makes commits as, or git's refusal in the
    /// app's words, from git's last <c>fatal:</c> line.
    /// </summary>
    public static string InEffectText(GitIdentityRead identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity is { Author: { } author, Committer: { } committer })
        {
            return author == committer ? Texts.CommitsMadeAs(author) : Texts.CommitsMadeAs(author, committer);
        }

        var line = identity.Refusal ?? string.Empty;
        if (line.Contains("unable to auto-detect email address", StringComparison.Ordinal)
            || line.Contains("no email was given and auto-detection is disabled", StringComparison.Ordinal))
        {
            return Texts.NoEmail;
        }

        if (line.Contains("no name was given and auto-detection is disabled", StringComparison.Ordinal))
        {
            return Texts.NoName;
        }

        if (line.Contains("empty ident name", StringComparison.Ordinal) && line.Contains("not allowed", StringComparison.Ordinal))
        {
            return Texts.EmptyName;
        }

        if (line.Contains("name consists only of disallowed characters", StringComparison.Ordinal))
        {
            return Texts.RefusedName;
        }

        var text = line.StartsWith("fatal: ", StringComparison.Ordinal) ? line["fatal: ".Length..] : line;
        return $"{Texts.CannotCommit} {text}";
    }

    /// <summary>A value as it is written and compared: trimmed, and none when empty.</summary>
    private static string? Normal(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Replace(ObservableCollection<GitNotice> list, IEnumerable<GitNotice> notices)
    {
        list.Clear();
        foreach (var notice in notices)
        {
            list.Add(notice);
        }
    }

    /// <summary>The scope's own read: the outside read for the global scope, the repository read for "This repository" (8.2).</summary>
    private GitConfigRead? ScopeRead() => IsGlobal ? _owner.OutsideRead : _owner.RepositoryRead;

    /// <summary>The last entry of <paramref name="key"/> of this scope in <paramref name="read"/>.</summary>
    private GitConfigValue? LastOf(GitConfigRead? read, string key) => LastOf(read, key, Scope);

    private static GitConfigValue? LastOf(GitConfigRead? read, string key, GitConfigScope scope) =>
        read?.Of(key).LastOrDefault(value => value.Scope == scope);

    /// <summary>What a repository gets from below its own scope: the repository read's last global or system entry.</summary>
    private static GitConfigValue? LowerOf(GitConfigRead? read, string key) =>
        read?.Of(key).LastOrDefault(value => value.Scope is GitConfigScope.Global or GitConfigScope.System);

    /// <summary>A box's placeholder (8.4, 8.6): global <c>Not set</c> or the system's value; repository <c>Same as global: {value}</c> or <c>Not set</c>.</summary>
    private string Placeholder(string key)
    {
        if (IsGlobal)
        {
            return LastOf(_owner.OutsideRead, key) is null && LastOf(_owner.OutsideRead, key, GitConfigScope.System) is { } system
                ? Texts.FromSystem(system.Value ?? "true")
                : Texts.NotSet;
        }

        return LowerOf(_owner.RepositoryRead, key) is { } lower ? Texts.SameAsGlobalValue(lower.Value ?? "true") : Texts.NotSet;
    }

    /// <summary>8.4's informational notices of one key at opening and after a read back.</summary>
    private List<GitNotice> Notices(string key, Func<string, string> shown, GitFileNames files, bool setsItsOwn = true)
    {
        var notices = new List<GitNotice>();
        var read = ScopeRead();
        var own = LastOf(read, key);

        // A file the app does not write decides the scope's value (an include, the XDG file).
        if (own is not null && !files.IsWrittenFile(own))
        {
            notices.Add(new GitNotice(Texts.GitUses(shown(own.Value ?? "true"), files.NameOf(own)), own.Origin));
        }

        if (IsGlobal)
        {
            // An includeIf that applies in the open repository.
            var inRepository = LastOf(_owner.RepositoryRead, key);
            if (_owner.RepositoryRead is not null && inRepository is not null
                && (own is null || inRepository.Value != own.Value || !GitFileNames.SamePath(inRepository.Origin, own.Origin)))
            {
                notices.Add(new GitNotice(Texts.InRepositoryGitUses(_owner.RepositoryName!, shown(inRepository.Value ?? "true"), files.NameOf(inRepository)), inRepository.Origin));
            }
        }
        else if (LastOf(read, key, GitConfigScope.Worktree) is { } worktree)
        {
            notices.Add(new GitNotice(Texts.WorktreeOwn(shown(worktree.Value ?? "true")), worktree.Origin));
        }

        if (LastOf(read, key, GitConfigScope.Command) is { } command)
        {
            notices.Add(new GitNotice(Texts.FromEnvironment(shown(command.Value ?? "true"))));
        }

        if (setsItsOwn && IsGlobal && LastOf(_owner.RepositoryRead, key, GitConfigScope.Local) is { } local)
        {
            notices.Add(new GitNotice(Texts.SetsItsOwn(_owner.RepositoryName!, local.Value ?? "true")));
        }

        return notices;
    }

    /// <summary><c>PullNotices</c> (8.4, 8.7): each pull key's notices, the key named; the system's line; the repository's own mode (SD8).</summary>
    private List<GitNotice> PullNoticesOf(GitFileNames files)
    {
        var notices = new List<GitNotice>();
        foreach (var key in new[] { PullModes.RebaseKey, PullModes.FastForwardKey })
        {
            // The repository's own pull keys are one notice of the pair's mode, below (SD8).
            notices.AddRange(Notices(key, value => $"{key} = {value}", files, setsItsOwn: false));
            if (IsGlobal && LastOf(_owner.OutsideRead, key) is null && LastOf(_owner.OutsideRead, key, GitConfigScope.System) is { } system)
            {
                notices.Add(new GitNotice(Texts.AlsoAppliesFromSystem(key, system.Value ?? "true"), system.Origin));
            }
        }

        if (IsGlobal && _owner.RepositoryRead is { } read)
        {
            var rebase = LastOf(read, PullModes.RebaseKey, GitConfigScope.Local);
            var fastForward = LastOf(read, PullModes.FastForwardKey, GitConfigScope.Local);
            if (rebase is not null || fastForward is not null)
            {
                var own = PullModes.Read(rebase?.Value, rebase is { Value: null }, fastForward?.Value, fastForward is { Value: null });
                notices.Add(new GitNotice(Texts.SetsItsOwn(_owner.RepositoryName!, own.Mode is { } mode ? RepositoryLabel(mode) : own.Other!)));
            }
        }

        return notices;
    }

    /// <summary>A mode's label as "This repository"'s radio shows it (SD8).</summary>
    private static string RepositoryLabel(PullMode mode) => mode switch
    {
        PullMode.NotSet => Texts.PullSameAsGlobal,
        _ => ModeLabel(mode),
    };

    /// <summary>A mode's label (8.7).</summary>
    public static string ModeLabel(PullMode mode) => mode switch
    {
        PullMode.Merge => Texts.PullMerge,
        PullMode.MergeCommit => Texts.PullMergeCommit,
        PullMode.Rebase => Texts.PullRebase,
        PullMode.RebaseMerges => Texts.PullRebaseMerges,
        PullMode.FastForwardOnly => Texts.PullFastForwardOnly,
        _ => Texts.NotSet,
    };

    /// <summary><c>BranchRebaseNotice</c> (8.8): the current branch's <c>branch.&lt;b&gt;.rebase</c>, the winning entry over every scope.</summary>
    private void FillBranchRebase(GitFileNames files)
    {
        BranchRebase = null;
        ShowsBranchRebaseButton = false;
        if (IsGlobal || _owner.Branch is not { } branch || _owner.RepositoryRead is not { } read)
        {
            return;
        }

        if (read.Winner($"branch.{branch}.rebase") is not { } entry)
        {
            return;
        }

        var value = entry.Value ?? "true";
        var inLocalFile = entry.Scope == GitConfigScope.Local && files.IsLocalFile(entry.Origin);
        var text = inLocalFile
            ? Texts.BranchRebase(branch, value)
            : Texts.BranchRebase(branch, $"{value} ({files.NameOf(entry)})");
        BranchRebase = new GitNotice(text, entry.Origin);
        ShowsBranchRebaseButton = inLocalFile;
    }

    /// <summary>8.6's identity notices, in its order.</summary>
    private List<GitNotice> IdentityNoticesOf(GitConfigRead read)
    {
        var notices = new List<GitNotice>();
        foreach (var variable in new[] { "GIT_AUTHOR_NAME", "GIT_AUTHOR_EMAIL" })
        {
            if (_owner.Variable(variable) is not null)
            {
                notices.Add(new GitNotice(Texts.VariableForAuthors(variable)));
            }
        }

        foreach (var variable in new[] { "GIT_COMMITTER_NAME", "GIT_COMMITTER_EMAIL" })
        {
            if (_owner.Variable(variable) is not null)
            {
                notices.Add(new GitNotice(Texts.VariableForCommitters(variable)));
            }
        }

        if (_owner.Variable("EMAIL") is not null)
        {
            notices.Add(new GitNotice(Texts.EmailVariable));
        }

        var files = _owner.Files;
        foreach (var key in new[] { "author.name", "author.email" })
        {
            if (read.Winner(key) is { } entry)
            {
                notices.Add(new GitNotice(Texts.KeyForAuthors(key, entry.Value ?? "true", files.NameOf(entry)), entry.Origin));
            }
        }

        foreach (var key in new[] { "committer.name", "committer.email" })
        {
            if (read.Winner(key) is { } entry)
            {
                notices.Add(new GitNotice(Texts.KeyForCommitters(key, entry.Value ?? "true", files.NameOf(entry)), entry.Origin));
            }
        }

        if (read.Winner("user.useConfigOnly") is { } configOnly && GitBoolean.TryParse(configOnly.Value, configOnly.Value is null, out var on) && on)
        {
            notices.Add(new GitNotice(Texts.UseConfigOnly));
        }

        return notices;
    }

    /// <summary><c>PullInEffect</c> (8.7): the mode Pull's reader gives and the key that decides it, with its file from the repository read.</summary>
    private GitNotice PullInEffectOf(string branch, PullConfiguration? configuration, string? failure, GitConfigRead read)
    {
        if (configuration is null)
        {
            return new GitNotice(Texts.PullFails(branch, failure ?? string.Empty));
        }

        if (configuration.Problem is { } problem)
        {
            return new GitNotice(Texts.PullFails(branch, problem));
        }

        (string Mode, string Key, string? Value)? decided = configuration switch
        {
            { FastForward: PullFastForwardSetting.Only } => (Texts.PullFastForwardOnly, PullModes.FastForwardKey, configuration.FastForwardValue),
            { Rebase: PullRebaseSetting.Rebase } => (Texts.PullRebase, configuration.RebaseKey, configuration.RebaseValue),
            { Rebase: PullRebaseSetting.Merges } => (Texts.PullRebaseMerges, configuration.RebaseKey, configuration.RebaseValue),
            { FastForward: PullFastForwardSetting.Never } => (Texts.PullMergeCommit, PullModes.FastForwardKey, configuration.FastForwardValue),
            { FastForwardValue: not null } => (Texts.PullMerge, PullModes.FastForwardKey, configuration.FastForwardValue),
            { RebaseValue: not null } => (Texts.PullMerge, configuration.RebaseKey, configuration.RebaseValue),
            _ => null,
        };

        if (decided is not { } mode)
        {
            return new GitNotice(Texts.PullNothingSet(branch));
        }

        var entry = read.Winner(mode.Key);
        var file = entry is null ? Texts.LocalFileName : _owner.Files.NameOf(entry);
        return new GitNotice(Texts.PullInEffect(branch, mode.Mode, mode.Key, mode.Value ?? entry?.Value ?? "true", file), entry?.Origin);
    }
}
