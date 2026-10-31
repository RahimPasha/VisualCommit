using System.Runtime.CompilerServices;
using Avalonia.Headless;
using VisualCommit.LiveHostingTests;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]
[assembly: CollectionBehavior(DisableTestParallelization = true)]   // one sandbox, steps in order

namespace VisualCommit.LiveHostingTests
{
    internal static class ProcessIsolation
    {
        /// <summary>
        /// Before <c>Main</c>, any test and Avalonia's set-up (docs/phase-7a-design.md 14.3): the live
        /// variables are read and removed first, since the isolation removes both too (1.8) and no git
        /// or app process may inherit them; then the process is cut off from the machine as every test
        /// project's is. Check_2's step limits sum to about 510 s (14.4), above the watchdog's default
        /// 180 s, so the limit is 600 s unless the owner set one. The watchdog holds each test to it on
        /// its own: Check_1's calls sum to about 440 s (13 to GitHub, 10 to the fake in process at
        /// 5 s each) and 30 s more per leftover, Check_3's to about 150 s (D342).
        /// </summary>
        [ModuleInitializer]
        internal static void Isolate()
        {
            LiveSettings.CaptureAndRemove();          // first: the isolation removes both too (1.8)
            HeadlessTestApp.IsolateFromTheMachine();
            if (Environment.GetEnvironmentVariable(HangWatchdogAttribute.LimitVariable) is null)
            {
                // Check_2's step limits sum to about 510 s (14.4), above the default 180 s.
                Environment.SetEnvironmentVariable(HangWatchdogAttribute.LimitVariable, "600");
            }
        }
    }
}
