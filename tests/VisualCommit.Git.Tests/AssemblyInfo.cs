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
        // tests run beside them. On a CI runner with few cores the pool then grew too slowly, and
        // a test's own continuation waited for the blocked handler to time out; with enough
        // threads from the start, a blocked handler cannot starve the test that watches it.
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, 64), Math.Max(completionPorts, 64));
    }
}
