using System.Runtime.CompilerServices;
using Avalonia.Headless;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]

namespace VisualCommit.VisualTests
{
    internal static class ProcessIsolation
    {
        /// <summary>
        /// Plain [Fact] tests can run before Avalonia's headless session builds the app (which
        /// isolates the process in HeadlessTestApp), and their code under test runs git too: cut
        /// the process off from the machine before any test runs.
        /// </summary>
        [ModuleInitializer]
        internal static void Isolate() => HeadlessTestApp.IsolateFromTheMachine();
    }
}
