using VisualCommit.Testing.Headless;

namespace VisualCommit.App.Tests;

/// <summary>
/// Pictures of what a headless test drew, for a person to look at while building a screen: taken
/// and saved only when the environment variable <c>VISUALCOMMIT_TEST_PICTURES</c> names a folder,
/// so the default test run spends no time on them and writes nothing. The visual checks keep
/// their own pictures (artifacts/visual).
/// </summary>
internal static class TestPictures
{
    public const string FolderVariable = "VISUALCOMMIT_TEST_PICTURES";

    /// <summary>Captures the window as <c>&lt;name&gt;.png</c> in the folder, when one is named.</summary>
    public static void Save(ShellDriver app, string name)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } folder)
        {
            app.Capture().Save(Path.Combine(folder, name + ".png"));
        }
    }

    /// <summary>Captures a view test's window as <c>&lt;name&gt;.png</c> in the folder, when one is named.</summary>
    public static void Save(DiffHost host, string name)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } folder)
        {
            host.Capture().Save(Path.Combine(folder, name + ".png"));
        }
    }
}
