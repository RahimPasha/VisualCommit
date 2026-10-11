// Part C's real-window checks (docs/test-reports/phase-3.md, part C, "Real window for part C"):
// check 13 steps 1 to 3, Esc, then check 16 steps 1 to 3 and Esc, on ONE launch on a fresh ops copy
// (one test method); check 14 steps 4 to 8 on CheckoutBlockedAsync (left-panel rows double-clicked
// with the real mouse, found by RealApp.RefRow; the working-changes row below the banner clicked at
// RealApp.GraphRowCentreY(0), D122); check 15 steps 1 to 3 (its changes written before the launch).
// "Select all and type" is RealApp.ReplaceText (Ctrl+A on the real keyboard). Only part C's writer
// edits this file. Shared helpers are in Phase3RealWindowPass.cs and RealApp.Checks.cs.
namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
}
