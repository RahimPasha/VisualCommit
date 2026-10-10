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
    internal static void IsolateGit() => GitIsolation.IsolateThisProcess();
}
