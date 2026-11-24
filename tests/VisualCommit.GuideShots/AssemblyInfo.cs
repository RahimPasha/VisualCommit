using System.Runtime.CompilerServices;
using Avalonia.Headless;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]

// One shot at a time: every shot uses the same demo root, whose folders each start replaces.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VisualCommit.GuideShots
{
    internal static class ProcessIsolation
    {
        /// <summary>
        /// Reads the guide's variables and the machine's facts that no picture may show before
        /// anything changes the process's environment, then cuts the process off from the machine
        /// as every walk-through does (<see cref="HeadlessTestApp.IsolateFromTheMachine"/>).
        /// </summary>
        [ModuleInitializer]
        internal static void Isolate()
        {
            GuideSettings.Capture();
            HeadlessTestApp.IsolateFromTheMachine();
        }
    }
}
