# Compare commits and branches

## Two commits

Select two commits in the graph: click the first, then Ctrl-click the second (Cmd-click on
macOS). The right panel turns into **Compare**: **From** the lower of the two rows in the graph
**To** the upper one, and the files that differ between them.

<img src="pictures/compare-light.png" srcset="pictures/compare-light.png 2x" alt="The window with the commits Show the battery level and Prepare release 1.1 selected in the graph, and the right panel Compare: From 0ce815d Prepare release 1.1, To c8a5135 Show the battery level, and Changed files (4): README.md, display.py, sensors.py and units.py.">
<img src="pictures/compare-dark.png" srcset="pictures/compare-dark.png 2x" alt="The window with the commits Show the battery level and Prepare release 1.1 selected in the graph, and the right panel Compare: From 0ce815d Prepare release 1.1, To c8a5135 Show the battery level, and Changed files (4): README.md, display.py, sensors.py and units.py.">

- Click a file to see its diff from **From** to **To**.
- Click **From** or **To** to scroll the graph to that commit without losing the comparison.
- The two arrows at the panel's top right swap **From** and **To**.
- **Flat** and **Tree** list the files as elsewhere.

Select another commit to leave the comparison.

## Two branches

Right-click a branch, a remote branch or a tag and choose **Compare** with the branch you are on,
such as `Compare feature/forecast with main`. Or drag a branch onto another and choose **Compare**
in the menu ([Merge](merge.md)). The comparison shows what changed on the first since it left the
second, and how many commits each side has that the other lacks. **All differences** shows every
difference between the two instead. Close the comparison with its **×**.
