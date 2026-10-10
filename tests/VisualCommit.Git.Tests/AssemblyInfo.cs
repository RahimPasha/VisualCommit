using System.Runtime.CompilerServices;
using VisualCommit.Testing;

[assembly: HangWatchdog]

namespace VisualCommit.Git.Tests;

internal static class Isolation
{
    /// <summary>
    /// Git calls made by the code under test (repository reading, cloning) inherit the test
    /// process's environment; cut them off from the machine's git configuration before any test runs.
    /// </summary>
    [ModuleInitializer]
    internal static void IsolateGit()
    {
        GitIsolation.IsolateThisProcess();

        // Some tests block a thread-pool thread on purpose (a slow output handler) while other
        // tests run beside them; with enough threads from the start, a blocked handler cannot
        // hold up the test that watches it while the pool grows.
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, 64), Math.Max(completionPorts, 64));
    }
}
