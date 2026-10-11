// Part B's real-window checks (docs/test-reports/phase-3.md, part B, "How the checks of each part
// are run"): check 9 steps 2, 3 and 8; check 10 steps 8 and 9 (part C's files written before the
// launch); check 11 steps 5, 8, 10 and 11 (the real Ctrl and Shift held during real clicks). Each
// on a fresh copy of its scenario, in a launch (and a test method) of its own. While a banner is
// shown, rows are clicked below the ConflictBanner element (RealApp.GraphRowCentreY, D122), never
// at a fixed y; in check 9 step 3 the pointer rests on BannerText's centre until the banner's
// tooltip shows (RealApp.RestPointerOn). Only part B's writer edits this file. Shared helpers are
// in Phase3RealWindowPass.cs and RealApp.Checks.cs.
namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
}
