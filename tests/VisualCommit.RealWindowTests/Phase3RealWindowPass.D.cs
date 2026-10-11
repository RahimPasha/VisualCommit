// Part D's real-window checks (docs/test-reports/phase-3.md, part D, "The real-window pass for
// these checks"): checks 17 and 21 on ONE server and ONE launch (17 with RequireAuth = false, then
// the server switched to RequireAuth = true; check 21's request lists asserted on
// server.Requests.Skip(n), n read just before its step 1); check 21b in a launch of its own (D115);
// checks 28 and 29 step 1 on one launch on sync; check 29 step 3 on ops with `mirror`; checks 19
// and 22 each on sync at the server; check 31 in two launches (RealApp.ClickCloseButton,
// RealApp.WaitForExit). A held fetch's percentage N may differ from the scripted picture's (the
// report's "Percentages"): run.txt reports it. Server scenarios: AuthAtServerAsync and
// SyncAtServerAsync in Phase3RealWindowPass.cs. Only part D's writer edits this file. Shared
// helpers are in Phase3RealWindowPass.cs and RealApp.Checks.cs.
namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
}
