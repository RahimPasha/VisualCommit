using Avalonia;
using Avalonia.Headless;
using VisualCommit.Core;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Builds the real <see cref="App.App"/> for Avalonia's headless mode, with Skia rendering so
/// that windows can be captured as screenshots. A UI test assembly points at it with
/// <c>[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]</c>.
/// </summary>
public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        IsolateFromTheMachine();

        return AppBuilder.Configure<App.App>()
            .UseSkia()
            .UseHarfBuzz()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>
    /// Cuts the app under test off from the user's files, through environment variables of the
    /// test process, which the app and every git process it starts inherit:
    /// <list type="bullet">
    /// <item>The data folder. Tests give every app session its own temporary one; this is the
    /// safety net for code that asks for the default folder anyway.</item>
    /// <item>Git's system and user configuration and the machine's time zone, so that git calls
    /// made by the app and the dates it shows are the same on every machine
    /// (<see cref="GitIsolation.IsolateThisProcess"/>).</item>
    /// </list>
    /// </summary>
    public static void IsolateFromTheMachine()
    {
        var data = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "isolation", "data");
        Directory.CreateDirectory(data);
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, data);
        GitIsolation.IsolateThisProcess();
    }
}
