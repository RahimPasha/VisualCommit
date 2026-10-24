using VisualCommit.Core.Git;

namespace VisualCommit.Core;

/// <summary>
/// What the machine has installed, as the app asks for it (docs/phase-6-design.md 12.9): the
/// editors found, the merge tools git finds and <c>PATHEXT</c>. As <see cref="AppClocks"/>, one
/// seam: the app reads the machine (<c>AppSession.Start</c> with none given and
/// <see cref="Variable"/> unset), the harnesses give <see cref="Fixed"/>, so that no picture shows
/// what one machine happens to have. No catalogue says whether a program exists: the menus never
/// ask (12.1).
/// </summary>
/// <param name="findEditors">The editors found on this computer (12.3).</param>
/// <param name="mergeTools">The merge tools git knows (8.10).</param>
/// <param name="pathExtensions">The <c>PATHEXT</c> value Open file reads (12.8); null off Windows.</param>
public sealed class ToolCatalogues(
    Func<CancellationToken, Task<IReadOnlyList<FoundEditor>>> findEditors,
    Func<CancellationToken, Task<MergeToolHelp>> mergeTools,
    Func<string?> pathExtensions)
{
    /// <summary>Set to <c>fixed</c>, the app uses <see cref="Fixed"/>: the real-window pass sets it for the app it starts.</summary>
    public const string Variable = "VISUALCOMMIT_TOOL_CATALOGUES";

    /// <summary>Windows' default <c>PATHEXT</c>, the value <see cref="Fixed"/> gives there and the test processes run with (17.2).</summary>
    public const string DefaultWindowsPathExtensions = ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC";

    /// <summary>The editors found on this computer (12.3).</summary>
    public Func<CancellationToken, Task<IReadOnlyList<FoundEditor>>> FindEditors { get; } = findEditors ?? throw new ArgumentNullException(nameof(findEditors));

    /// <summary>The merge tools git knows (8.10).</summary>
    public Func<CancellationToken, Task<MergeToolHelp>> MergeTools { get; } = mergeTools ?? throw new ArgumentNullException(nameof(mergeTools));

    /// <summary>The <c>PATHEXT</c> value Open file asks by besides its own lists (12.8); null off Windows.</summary>
    public Func<string?> PathExtensions { get; } = pathExtensions ?? throw new ArgumentNullException(nameof(pathExtensions));

    /// <summary>The editors of <see cref="Fixed"/>: Visual Studio Code, Sublime Text and IntelliJ IDEA, ids every platform's catalogue has.</summary>
    public static IReadOnlyList<FoundEditor> FixedEditors { get; } = OperatingSystem.IsWindows()
        ?
        [
            new("vscode", "Visual Studio Code", @"C:\Tools\Microsoft VS Code\Code.exe"),
            new("sublime", "Sublime Text", @"C:\Tools\Sublime Text\subl.exe"),
            new("idea", "IntelliJ IDEA", @"C:\Tools\JetBrains\IntelliJ IDEA 2026.1\bin\idea64.exe"),
        ]
        :
        [
            new("vscode", "Visual Studio Code", "/opt/tools/vscode/code"),
            new("sublime", "Sublime Text", "/opt/tools/sublime/subl"),
            new("idea", "IntelliJ IDEA", "/opt/tools/idea/bin/idea"),
        ];

    /// <summary>The merge tools of <see cref="Fixed"/>.</summary>
    public static MergeToolHelp FixedMergeTools { get; } = new(
        [new("kdiff3", null), new("meld", null), new("vimdiff", null)],
        [new("araxis", null), new("bc", null), new("p4merge", null), new("winmerge", null)],
        Failed: false);

    /// <summary>
    /// The harnesses' catalogues (12.9, 17.2), the same in every run: three editors under a root
    /// that does not exist, nothing of which is ever started; the merge tools <c>kdiff3</c>,
    /// <c>meld</c> and <c>vimdiff</c> found and <c>araxis</c>, <c>bc</c>, <c>p4merge</c> and
    /// <c>winmerge</c> not, without descriptions and there at once (<c>merge-tools.json</c> neither
    /// read nor written); on Windows its default <c>PATHEXT</c>, none elsewhere.
    /// </summary>
    public static ToolCatalogues Fixed { get; } = new(
        _ => Task.FromResult(FixedEditors),
        _ => Task.FromResult(FixedMergeTools),
        () => OperatingSystem.IsWindows() ? DefaultWindowsPathExtensions : null);

    /// <summary>The catalogues this process's environment asks for: <see cref="Fixed"/> when <see cref="Variable"/> is <c>fixed</c>, otherwise null (the machine's).</summary>
    public static ToolCatalogues? FromEnvironment() =>
        string.Equals(Environment.GetEnvironmentVariable(Variable), "fixed", StringComparison.Ordinal) ? Fixed : null;
}

/// <summary>An editor found on this computer (docs/phase-6-design.md 12.3).</summary>
/// <param name="Id">Its <c>KnownEditors</c> id (<c>vscode</c>).</param>
/// <param name="Name">The name the settings screen shows (<c>Visual Studio Code</c>).</param>
/// <param name="Program">The program's absolute path.</param>
public sealed record FoundEditor(string Id, string Name, string Program);
