using VisualCommit.Testing.Headless;

namespace VisualCommit.App.Tests;

/// <summary>
/// Pictures of what a headless test drew, for a person to look at while building a screen: saved
/// only when the environment variable <c>VISUALCOMMIT_TEST_PICTURES</c> names a folder, so the
/// default test run writes nothing. The visual checks keep their own pictures (artifacts/visual).
/// </summary>
internal static class TestPictures
{
    public const string FolderVariable = "VISUALCOMMIT_TEST_PICTURES";

    /// <summary>Saves <paramref name="screenshot"/> as <c>&lt;name&gt;.png</c> in the folder, when one is named.</summary>
    public static void Save(Screenshot screenshot, string name)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        if (Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } folder)
        {
            screenshot.Save(Path.Combine(folder, name + ".png"));
        }
    }
}
