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
        /// its own: Check_1's calls sum to about 440 s (13 to GitHub at 30 s, 10 to the fake in process
        /// at 5 s) and 30 s more per leftover, so the 600 s cover five leftovers at full limits (a
        /// stopped Check_2 leaves two: its branch and its pull request); the fake's start, a few git
        /// calls with no limit of their own, is outside that sum. Check_3's calls sum to about 150 s
        /// (D342). Real calls take a small part of their limits. The new providers' sums
        /// (docs/phase-7b-design.md 15.5): Azure DevOps' Check_1 sends at most 4 calls and 6 for five
        /// leftovers (5 abandons and one refs POST), 300 s at 30 s; its probes go to Check_1b_probes,
        /// 16 calls to the service at 30 s and 16 to the fake in process at 5 s, 560 s; its Check_2's
        /// app steps (sign-in, list, load, push and create at 60 s, the clone at 120 s) and its own
        /// reads (the sandbox, about 13 probes held to 15 s each, the clean-up's three calls at 30 s)
        /// sum to about 600 s at full limits; Check_4's to about 360 s. GitLab's Check_1 sends 4
        /// calls and 15 for five leftovers (each request's close and delete, each branch's delete),
        /// 570 s; its Check_1b_probes 7 to the service and 7 to the fake, 245 s; its Check_2 about
        /// 555 s. Real calls take a small part of their limits.
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
