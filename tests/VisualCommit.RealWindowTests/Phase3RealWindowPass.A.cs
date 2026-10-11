// Part A's real-window checks (docs/test-reports/phase-3.md, part A, "How the checks of each part
// are run"): check 23 step 1, check 1 steps 1, 3 and 4, check 5 steps 1 to 3 and check 7 steps 1,
// 2 and 7, in that order, on ONE launch on one ops copy at 1100×700 (check 7's merge last): one
// test method. Graph rows and labels are clicked and dragged at positions computed from the same
// constants as the scripted expectations (RealApp.GraphRowPoint, Phase3Expectations.PartA); menus
// are found by ContextMenu and ContextMenuItem; a pointer resting on a menu item takes the path of
// RealApp.RestPointerOnMenuItem. Only part A's writer edits this file. Shared helpers are in
// Phase3RealWindowPass.cs and RealApp.Checks.cs.
namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
}
