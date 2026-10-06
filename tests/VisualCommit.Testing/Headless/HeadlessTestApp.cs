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
    private static readonly TempDirectory FallbackDataDirectory = new("fallback-data");

    public static AppBuilder BuildAvaloniaApp()
    {
        // Tests give every app session its own temporary data folder. This is the safety net
        // for code that asks for the default folder anyway: it must never be the user's real one.
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, FallbackDataDirectory.Path);

        return AppBuilder.Configure<App.App>()
            .UseSkia()
            .UseHarfBuzz()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
