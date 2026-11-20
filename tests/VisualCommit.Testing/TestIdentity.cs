using VisualCommit.App.Services;

namespace VisualCommit.Testing;

/// <summary>The app's identity in the harnesses, fixed so that no scripted picture changes from one commit to the next.</summary>
public static class TestIdentity
{
    /// <summary>
    /// The identity every headless start gets unless a test gives another
    /// (<c>ShellDriver.Start</c>): VisualCommit by Rahim Khajei, version <c>0.1.0 (1a2b3c4)</c>.
    /// </summary>
    public static AppIdentity Pinned { get; } = new(
        "VisualCommit",
        "Rahim Khajei",
        new AppVersionInfo("0.1.0", "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d"),
        "Copyright (c) 2026 Rahim Khajei");
}
