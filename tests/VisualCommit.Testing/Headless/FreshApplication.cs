using System.Reflection;
using Avalonia.Headless;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Runs part of a test with a brand-new Avalonia <see cref="Avalonia.Application"/>, as a restart
/// of the app would have. An <c>[AvaloniaFact]</c> gets one application for the whole test; a
/// test that must cross a restart is a plain <c>[Fact]</c> that calls
/// <see cref="RunAsync{T}"/> once per app instance, on the same data folder.
/// </summary>
public static class FreshApplication
{
    /// <summary>
    /// Runs <paramref name="action"/> on the headless UI thread of <paramref name="testAssembly"/>
    /// with a new application and dispatcher, and returns its result.
    /// </summary>
    public static Task<T> RunAsync<T>(Assembly testAssembly, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(testAssembly);

        // Dispatch completes its task on the UI thread, and an awaiting test would carry on
        // right there: xunit would then start the next test from the UI thread and wait for a
        // dispatch that the same thread has to run. Finishing on the thread pool avoids that.
        return session.Dispatch(action, cancellationToken).ContinueWith(
            task => task.GetAwaiter().GetResult(),
            CancellationToken.None,
            TaskContinuationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }
}
