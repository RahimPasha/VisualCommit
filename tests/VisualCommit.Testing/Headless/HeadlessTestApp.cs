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
    /// <item>Git's system and user configuration, so that git calls made by the app behave the
    /// same on every machine, as <see cref="TempRepo"/> ensures for its own calls. The user
    /// configuration needs Git 2.32 or newer to be left out.</item>
    /// </list>
    /// </summary>
    public static void IsolateFromTheMachine()
    {
        var folder = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "isolation");
        var emptyGitConfig = Path.Combine(folder, "gitconfig");
        Directory.CreateDirectory(Path.Combine(folder, "data"));
        if (!File.Exists(emptyGitConfig))
        {
            File.WriteAllText(emptyGitConfig, string.Empty);
        }

        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, Path.Combine(folder, "data"));
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", emptyGitConfig);

        // Nor anything the machine passes to git through the environment: extra configuration,
        // or another repository to work on.
        foreach (var variable in GitIsolation.InheritedVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }
}
