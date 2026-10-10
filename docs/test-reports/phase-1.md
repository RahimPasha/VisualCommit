# Phase 1 visual test report — Repos and commit graph

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

Filled in when the gate runs.

## Scenario repos

The checks start from these repos; `ScenarioTests` pins their content and commit ids.

- **Graph scenario** (`Scenarios.GraphAsync`): 12 commits and a stash, made one minute apart from
  2026-01-01 12:00 UTC by "Test Author" <author@example.com>. Branches `main` (HEAD),
  `feature/login`, `feature/search`, `bugfix/crash-on-start`; a bare remote `origin` with
  `origin/main` and `origin/feature/search`; `main` tracks `origin/main` and is 1 ahead and 1
  behind; `feature/search` tracks `origin/feature/search` and is 1 ahead; tags `v0.1`
  (annotated) and `v0.2` (lightweight); one stash.
- **Linear scenario** (`Scenarios.LinearAsync`, from phase 0): 3 commits on `main`.
- **100k-commit repo** (`LargeHistory`): 100,000 commits. Row `r` of the graph (0 at the top)
  shows commit number `100000 - r`: "Merge commit n" when n is a multiple of 50, "Feature commit
  n" when n mod 50 is 42 to 49, "Main commit n" otherwise. Feature commits are in lane 1, all
  others in lane 0. Lane 0 has colour 0 all the way down. The feature commits just below "Merge
  commit m" have colour `(100000 - m) / 50 + 1`, so the palette colour of that lane changes with
  every merge and is colour 0's every eighth time (for example below "Merge commit 99650"). HEAD
  is on `main` (row 0); the branch `feature` is on row 1; the tag `middle` is on row 50,000
  ("Merge commit 50000"); the last row is "Main commit 1".

Dates are shown in UTC: both harnesses set `VISUALCOMMIT_TIME_ZONE=UTC` (D43). Commits a check
makes itself are made through `TempRepo`, by "Test Author" on the scenario's clock, never with
the machine's clock.

Where a stash goes and how lanes are given out is as D47, D48 and D54 say.

## What a repository tab must show

Sizes are in logical pixels; a tolerance of 2 pixels is allowed. The shell around the graph
area (tab strip, toolbar, status bar, panel sizes and background colours) is as in
[phase 0's report](phase-0.md), "What the shell must show", except where this section says
otherwise.

**Tabs.** One tab per open repository, labelled with the repository's folder name (`graph`,
`linear`, ...), and "New tab" for a tab without one. Tabs are at least 120 and at most 220 wide;
a longer name ends in "…" and the tab's tooltip shows it whole. The active tab has the accent
line along its top edge and the graph area's background; the others have the tab strip's. Every
tab has a close button (an "×" icon) at its right, except a lone "New tab". The "+" button
after the last tab is enabled.

**Graph area, with a repository open.** A column-header row 28 high, then one row per commit,
26 high, starting with the newest. Columns, from the left:

| Column | Width | Shows |
|---|---|---|
| Branch / Tag | 130 | The refs on the commit as labels, 18 high with rounded corners, placed from 6 pixels after the column's left edge with 4 pixels between them: first the branch HEAD is on, then other local branches, remote branches, tags, the stash. When the next label does not fit, it and the rest are shown as one label "+N" (N labels hidden). The first label is always shown; when it alone is too wide, its name ends in "…". Hovering a row's labels shows all of them, in full, in a tooltip |
| Graph | 16 per lane plus 16, at least 48, at most 240 | Lanes 16 apart, the first lane's centre 16 from the column's left edge. Lines 2 wide. A commit is a filled circle 10 across in its lane's colour; a merge commit a filled circle 7 across; a stash a ring 10 across, 2 wide, with the row's background inside |
| Message | The rest, at least 120 | The subject in the main text colour; a stash's message in the secondary text colour. Cut off with "…" when too long |
| Author | 140 | The author's name, secondary colour |
| Date | 120 | `yyyy-MM-dd HH:mm`, secondary colour |
| SHA | 72 | The first 7 characters of the id, secondary colour |

When the graph area is too narrow for all columns with a message at least 120 wide, the SHA
column is hidden first, then Author, then Date, headers included (D49). The vertical scroll bar
lies over the right edge of the rows and takes no width from the columns. At 1100×700 the graph
area is 440 wide: for the graph scenario (3 lanes, a Graph column of 64) and the 100k-commit repo
(2 lanes, 48) it shows Branch / Tag, Graph, Message and Date. At 1920×1080 it is 1260 wide and
shows all six. The rows area is the graph area's height less the 28 of the headers: 558 at
1100×700, so 21 whole rows and part of a 22nd show.

Labels: the text is 11 pixels, semibold, with 6 pixels of padding on each side; a check mark or
tag icon is 10 pixels with 3 pixels after it. A detached HEAD's label comes before all others.
A local branch is filled with its row's node colour, with its name in the label text
colour (`#10121A` dark, `#FFFFFF` light); the branch HEAD is on also has a check mark before its
name; a detached HEAD is a filled label "HEAD". A remote branch has a 1-pixel border in the node
colour, no fill, and its name in the main text colour. A tag and a stash have the control
background, a border in the border colour and their name (`v0.1`, `stash@{0}`) in the main
text colour; a tag also has a tag icon.

A selected row has the selection background (`#2A3150` dark, `#DDE1FA` light) over its whole
width; the row under the mouse has the row-hover background (`#1C1F27` dark, `#F3F4F7` light).
Nothing is selected when a repository opens, and the details panel then shows phase 0's
"Select a commit to see its details.".

**Lane colours**, by colour number modulo 8 (D48):

| # | Dark | Light |
|---|---|---|
| 0 | `#7C8CFF` | `#4353D8` |
| 1 | `#3FB97F` | `#1E8E5A` |
| 2 | `#E0A23B` | `#B7791F` |
| 3 | `#E5606B` | `#C93A46` |
| 4 | `#4FB3D9` | `#1F86B0` |
| 5 | `#B07CE8` | `#8048C7` |
| 6 | `#D97EB6` | `#B54A8C` |
| 7 | `#8FB84A` | `#5F8A1E` |

**The graph scenario's graph.** Lanes are counted from 0 at the left.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0, ring | On main: Work in progress on README | 2026-01-01 12:12 | `95daa96` |
| 1 | `feature/search` | 1, 1 | Highlight matches | 2026-01-01 12:11 | `f463b85` |
| 2 | `main` with check mark | 0, 0 | Add settings page | 2026-01-01 12:10 | `47e6ec7` |
| 3 | `origin/main` | 2, 2 | Fix typo in docs | 2026-01-01 12:09 | `974bd83` |
| 4 | `v0.2` | 0, 0 | Bump version | 2026-01-01 12:08 | `2775228` |
| 5 | `bugfix/crash-on-start` | 2, 3 | Fix crash on start | 2026-01-01 12:07 | `72267a3` |
| 6 | `origin/feature/search` | 1, 1 | Add search box | 2026-01-01 12:06 | `6aab2e4` |
| 7 | | 0, 0, merge | Merge branch 'feature/login' | 2026-01-01 12:05 | `f7919b8` |
| 8 | | 0, 0 | Update README | 2026-01-01 12:04 | `10bcd42` |
| 9 | `feature/login` | 1, 4 | Validate passwords | 2026-01-01 12:03 | `4a62ef9` |
| 10 | | 1, 4 | Add login form | 2026-01-01 12:02 | `7b230d3` |
| 11 | `v0.1` | 0, 0 | Add app skeleton | 2026-01-01 12:01 | `933250d` |
| 12 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `53347b3` |

A label wider than the column allows ends in "…" as the column's rule says, and what is shown
before the "…" is the start of its name. Whether `bugfix/crash-on-start` and
`origin/feature/search` are cut follows from the sizes above; the test measures the text with
the same font.

Lines: lane 0 runs from row 0 to row 12. Lane 1 (colour 1) starts at row 1 and ends in the node of
row 7. Lane 2 (colour 2) starts at row 3 and ends in the node of row 4; lane 2 starts again at row
5 (colour 3) and ends in the node of row 7. From row 7's node a line goes down to lane 1 (colour
4), which runs to row 10 and ends in the node of row 11. Every author is "Test Author".

**Left panel, with a repository open.** The filter box, then the sections in phase 0's order.
A section header is 30 high: a chevron pointing down when the section is open and right when it
is closed, the title, and at the right the number of items (local branches, remote branches,
tags, stashes) that match the filter. Items are 26 high, indented 14 per level; a folder has a
chevron and its name; folders come before items, each sorted by name. A local branch shows its
name, in the accent colour for the branch HEAD is on, and at the right, when it has an upstream,
an up arrow with the number of commits ahead and a down arrow with the number behind (each only
when not 0). A name too long for the panel ends in "…", with the whole name in its tooltip. Folders are closed when the repository opens, except those that hold the branch
HEAD is on; sections and remotes are open. "Pull requests" stays closed with the count 0
(phase 7). For the graph scenario:

```
▾ Local branches          4
    ▸ bugfix
    ▸ feature
      main          ↑1 ↓1        (accent colour)
▾ Remotes                 2
    ▾ origin
        ▸ feature
          main
▸ Pull requests           0
▾ Tags                    2
      v0.1
      v0.2
▾ Stashes                 1
      On main: Work in progress on README
```

**Commit details**, in the right panel under its "Commit details" header: the subject in 14-point
semibold, wrapped; the body below it, wrapped; then the rows "Author" (name and e-mail
address), "Date", "Committer" and "Commit date" (only when they differ from the author's), "SHA"
(the full id), "Parents" (the short ids of the parents, each a link that selects that commit),
and for a stash "Stash" (`stash@{0}`); a stash's Parents row lists only the commit it was made
on, not git's internal index and untracked-files commits. Then "Changed files (N)" with two toggle buttons, "Flat"
and "Tree", and the file list. A file row shows a status letter (A in the success colour, M in
the warning colour, D in the danger colour, R in the accent colour), the file name, and in the
secondary colour its folder; a renamed file adds "renamed from <old path>". The tree shows
folders first, then files, each sorted by name, all folders open; a file row in the tree shows
the status letter and the file name, and for a renamed file "renamed from <old path>", but no
folder.

**Status bar.** The branch HEAD is on (`main`), "Detached at <short id>" for a detached HEAD,
and "No repository" for a tab without one. The rest as in phase 0.

**Welcome page** (the graph area of a tab without a repository; the column headers are hidden):
centred, "No repository open" and "Open, clone or init a repository to see its history." as in
phase 0; under them three buttons "Open", "Clone" and "Init"; then the heading "Recent
repositories" and the list of recent repositories, newest first, each with its name and, in
the secondary colour, its folder; or "No recent repositories" when the list is empty. An error
shows in the danger colour under the buttons. The side panels show their phase 0 empty states.

**Clone form** (replaces the buttons and the recent list): the heading "Clone a repository",
the fields "Repository URL", "Parent folder" (with a "Browse" button) and "Folder name", and the
buttons "Clone" and "Cancel". The folder name fills itself from the URL's last part without
".git" until it is typed into. While cloning, the fields are disabled, a progress bar shows the
percentage of the current stage and a text shows git's stage and percentage as
"<stage> <percent>%" (such as "Receiving objects 45%"); a stage without a percentage shows its
name alone.

**Logged measurements** (D50, D57), in the data folder's log:
`<name>: first graph rows drawn after <N> ms`,
`<name>: loaded <N> commits in <N> ms`, and, when the tab closes or the app exits,
`<name>: graph frames while loading: <N> drawn, 95th percentile <N.N> ms, longest <N.N> ms`
for the frames drawn while the history loaded and
`<name>: graph frames: <N> drawn, 95th percentile <N.N> ms, longest <N.N> ms` for the frames
drawn after it, which are the ones judged.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). "Scripted" uses a fake folder
dialog that answers with the folder named in the step (D42); "Real window" uses the real one.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Graph of the graph scenario, dark, 1100×700 | 1. Write a session with one tab on a copy of the graph scenario. 2. Start the app, 1100×700, dark. 3. Wait until the graph has drawn all 13 rows. 4. Screenshot. | The tab is labelled `graph` and is the only tab, with a close button. The graph area shows the graph scenario's graph exactly as in the table above: labels, lanes, colours, node shapes, messages and dates; the columns Branch / Tag, Graph, Message and Date, with Author and SHA hidden. Nothing is selected. The left panel shows the graph scenario's tree as above. The details panel shows "Select a commit to see its details.". The status bar shows `main`. No text is clipped, except text that ends in "…" by the rules above (messages, labels, left-panel names) and has its whole text in a tooltip. | | Yes | | |
| 2 | Graph of the graph scenario, light, 1920×1080 | As check 1, light theme, 1920×1080. | As check 1 in the light colours, with all six columns: every Author cell says "Test Author" and every SHA cell the short id from the table. The left panel is still 260 wide and the right panel 400. | | n/a | | |
| 3 | Commit details and the file list | 1. As check 1. 2. Click the row "Add settings page". 3. Screenshot. 4. Click "Tree". 5. Screenshot. 6. Close the app, start a new instance on the same data folder, click the same row, screenshot. | After step 2 the row has the selection background and the details panel shows: "Add settings page"; the body "The settings page lists the defaults." and "It replaces the old notes." on two lines; Author "Test Author <author@example.com>"; Date "2026-01-01 12:10"; no Committer row; SHA, the full id of `47e6ec7`; Parents `2775228`; "Changed files (5)", "Flat" selected, and in this order: M `README.md`; D `notes.txt` `docs`; R `main-app.txt` `src`, renamed from `src/app.txt`; A `defaults.txt` `src/settings`; A `page.txt` `src/settings`. After step 4 the list is a tree: `docs` (D `notes.txt`), `src` (`settings` with A `defaults.txt` and A `page.txt`, then R `main-app.txt`), then M `README.md`; `settings.json` says the file list is Tree. After step 6 the new instance shows the tree straight away. | | Yes (steps 1 to 5, real mouse) | | |
| 4 | Details of a merge and of a stash | 1. As check 1, light theme. 2. Click the row "Merge branch 'feature/login'". Screenshot. 3. Click the parent link `4a62ef9`. Screenshot. 4. Click the stash row. Screenshot. | After step 2: the merge's details, with Parents `10bcd42` and `4a62ef9`, and "Changed files (2)": A `login.txt` `src`, A `rules.txt` `src/validation` (compared with the first parent). After step 3: the row "Validate passwords" is selected and its details shown. After step 4: subject "On main: Work in progress on README", a Stash row `stash@{0}`, Parents `47e6ec7`, "Changed files (1)": M `README.md`. | | n/a | | |
| 5 | Left panel: sections, folders, counts | 1. As check 1. 2. Screenshot. 3. Click the folder `feature` under Local branches. Screenshot. 4. Click the header "Tags". Screenshot. | After step 2 the left panel is as in the tree above: counts 4, 2, 0, 2, 1; `main` in the accent colour with ↑1 ↓1. After step 3 the folder is open and shows `login` (no counts) and `search` with ↑1 and no down arrow. After step 4 the Tags section is closed (chevron right, count still 2) and the Stashes section has moved up under it. | | n/a | | |
| 6 | The filter narrows the list | 1. As check 1. 2. Click the filter box and type `sea`. Screenshot. 3. Clear the filter (select all and delete). Screenshot. | After step 2 only matching items remain, with the folders that hold them open: Local branches 1 (`feature` / `search` ↑1), Remotes 1 (`origin` / `feature` / `search`), Pull requests 0, Tags 0, Stashes 0, sections with no match showing their header only. After step 3 the panel is as in check 1 again. | | Yes (typing with the real keyboard) | | |
| 7 | A ref in the left panel selects its commit | 1. As check 1. 2. Click the tag `v0.1`. Screenshot. 3. Click the stash entry. Screenshot. 4. Click `main` under origin. Screenshot. | Each click selects the commit's row in the graph and shows its details: step 2 "Add app skeleton", step 3 the stash, step 4 "Fix typo in docs". The clicked item has the selection background in the left panel. | | n/a | | |
| 8 | The 100k-commit repo, top, middle and bottom | 1. Write a session with one tab on the 100k-commit repo; start the app, 1100×700, dark. 2. Wait until the first rows are drawn. Screenshot. 3. Turn the mouse wheel 10 notches down over the graph. Screenshot. 4. Wait until all 100,000 commits are loaded (the log says `loaded 100000 commits`). Click the tag `middle` in the left panel. Screenshot. 5. Click a row of the graph, then press End. Screenshot. 6. Press Home. Screenshot. | In every screenshot every row of the graph area is drawn: its node in the lane and colour that "Scenario repos" gives, its message, its date, and its lines joined to the rows above and below; no row is blank or doubled, and only the top or bottom row of the view may be cut by the view's edge. Step 2: the top row is "Merge commit 100000" with the label `main` (with check mark), row 1 "Feature commit 99999" with the label `feature`. Step 3: the top row is "Main commit 99970" (one notch scrolls 3 rows). Step 4: "Merge commit 50000" is selected, labelled `middle`, and is the row nearest the vertical centre of the rows area (±1 row); the rows around it are the commits numbered next to it. Step 5: "Main commit 1" is selected and is the last row, its bottom edge at the bottom of the rows area; the top row of the view is cut. Step 6: "Merge commit 100000" is selected and at the top. The left panel shows Local branches 2, Tags 1. | | Yes | | |
| 9 | Q1: time to the first graph and frame times | 1. As check 8, steps 1 and 2. 2. Scroll through the history with the mouse wheel: 200 notches down, then 200 up. 3. Close the app. 4. Read the log. | The log has the four measurement lines for the tab. On the Windows development machine, in the real-window pass: the first graph rows are drawn within 2,000 ms of the start of opening the repository; of the frames drawn after the history loaded (the scroll), the 95th percentile of draw times is at most 8 ms and the longest at most 33 ms (D50, D57). The scripted walk-through records its numbers as an indication only. | | Yes (judged here) | | |
| 10 | Welcome page and tabs | 1. Write a session with no tabs and a recent list of the graph and linear scenarios' folders (graph newest). 2. Start the app, 1100×700, dark. Screenshot. 3. Click "+". Screenshot. 4. Close the second tab with its close button. Screenshot. | After step 2: one tab "New tab" without a close button; the graph area shows the welcome page with "Open", "Clone", "Init", "Recent repositories" and the two entries `graph` then `linear`, each with its folder. The left panel and details panel show phase 0's empty states; the status bar shows "No repository". After step 3: a second "New tab" is active, and both tabs have close buttons. After step 4: one tab again, without a close button. | | n/a | | |
| 11 | Open a repository | 1. Start the app on an empty data folder, 1100×700, light. 2. Click "Open" and choose a plain folder that is not a repository. Screenshot. 3. Click "Open" and choose a copy of the linear scenario. Screenshot. 4. Click "+", then look at the welcome page. Screenshot. | After step 2: an error under the buttons in the danger colour, "<folder> is not a git repository.", and the tab is still "New tab". After step 3: the tab is labelled `linear` and shows its 3 commits ("Describe the project", "Add greeting", "Add README") on one lane with the label `main` (check mark) on the top row; the status bar shows `main`. After step 4: the new tab's recent list shows `linear` with its folder. `session.json` lists the tab and the recent repository. | | Yes (the native folder dialog) | | |
| 12 | Init a repository | 1. Start the app on an empty data folder, 1100×700, dark. 2. Click "Init" and choose a new, empty folder `fresh`. Screenshot. | The folder now holds a git repository. The tab is labelled `fresh`; the graph area shows its column headers and, centred below them, "No commits yet" and "Make the first commit in this repository to see it here."; the left panel's counts are all 0; the status bar shows the branch git created (as `git symbolic-ref --short HEAD` reports it). | | n/a | | |
| 13 | Clone with progress | 1. Start the app on an empty data folder, 1100×700, dark. 2. Click "Clone". Screenshot. 3. Type the `file://` URL of the 100k-commit repo, a parent folder, and the folder name `cloned`. 4. Click "Clone" and take a screenshot while the progress shows a percentage between 1 and 99. 5. Wait until the clone is done. Screenshot. | After step 2: the clone form, empty, with the URL field focused. During step 4: the fields are disabled, the progress bar and the stage text show git's progress (such as "Receiving objects 45%"), and "Cancel" is enabled. After step 5: the tab is labelled `cloned` and shows the 100k-commit history; the top row's labels are `main` (check mark) and a "+1" label for `origin/main`, whose tooltip lists both; row 1 has the label `origin/feature`; the Remotes section shows `origin` with 2 branches; `cloned` is in the recent list. | | n/a | | |
| 14 | A failed and a cancelled clone | 1. As check 13 step 2. 2. Type the `file://` URL of a folder that does not exist, a parent folder and the folder name `cloned`, and click "Clone". Screenshot. 3. Type the 100k-commit repo's URL and the folder name `cloned`, start the clone, and click "Cancel" while it runs. Screenshot. | After step 2: in the danger colour, git's own error: the same `fatal:` line that `git clone` prints for that URL in a terminal; the form stays filled in and enabled; no folder `cloned` was left behind. After step 3: the form is enabled again with the message "Clone cancelled.", and the folder `cloned` does not exist. | | n/a | | |
| 15 | Three tabs, restored after a restart | 1. Write a session with three tabs: the graph scenario, the linear scenario and the 100k-commit repo, the second one active. 2. Start the app, 1100×700, dark. Screenshot. 3. Click the third tab. Screenshot. 4. Click the first tab, then close the app. 5. Start a new instance on the same data folder. Screenshot. | After step 2: three tabs `graph`, `linear`, `large-history-v1`; `linear` is active and its graph is shown. After step 3: the 100k history is shown, with the left panel and status bar of that repo. After step 5: the same three tabs in the same order, `graph` active and its graph shown. | | Yes (a real process restart) | | |
| 16 | Window and panels restored after a restart | 1. Start the app on an empty data folder at 1920×1080 (real window: 1400×900, which fits the development screen). 2. Drag the left panel's edge to make it 320 wide and the right panel's edge to make it 460 wide. 3. Resize the window to 1300×800 (real window: and move it). 4. Close the app and start a new instance on the same data folder without setting its size. Screenshot. | The new instance's window is 1300×800 (real window: at the position it was moved to), the left panel 320 and the right panel 460 wide, and the graph area takes the rest (520). `session.json` holds these values. | | Yes (a real process restart) | | |
| 17 | Panels give way in a narrow window | 1. Start the app on an empty data folder at 1920×1080, dark. 2. Drag the left panel to 500 wide and the right panel to 700 wide. Screenshot. 3. Resize the window to 1000×700. Screenshot. 4. Resize it back to 1920×1080. Screenshot. | After step 2: left 500, graph area 720, right 700. After step 3: the graph area is 320 and the panels have shrunk in proportion to how far each was above its minimum (180 for the left panel and 280 for the right one; the graph's minimum is 320, the window's 1000×560, all from phase 0): left 275, right 405 (each ±2); no panel is cut off by the window's edge and no text is clipped. After step 4: left 500, right 700 again. | | n/a | | |
| 18 | Changes made in a terminal appear | 1. As check 1. 2. Click the row "Add settings page". 3. Outside the app, commit a file on `main` with the message "Commit from a terminal" (through `TempRepo`: by Test Author, at 2026-01-01 12:13). 4. Wait until the graph shows it (at most 5 seconds). Screenshot. 5. Outside the app, detach HEAD at `v0.2`. 6. Wait until the status bar changes (at most 5 seconds). Screenshot. | After step 4: the top row is "Commit from a terminal", dated 2026-01-01 12:13, with the label `main` (check mark), in lane 0; the stash is now row 1; "Add settings page" is still selected (now row 3) with its details shown; the left panel shows `main` ↑2 ↓1. After step 6: the status bar shows "Detached at 2775228"; the row "Bump version" has a filled "HEAD" label first; `main` has no check mark and is no longer in the accent colour in the left panel. | | Yes (steps 1 to 4) | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say
"n/a". The real-window pass compares its screenshots of checks 1, 3, 8 (steps 2, 5 and 6), 15
and 18 with the scripted ones (D33); check 8's middle, check 11's dialog and check 16's window
position are inspected by eye only.

## Changes to expected results

The frame statistics of check 9 and "Logged measurements" were changed after the graph was built
(D57): the review of the graph control measured frames of up to 60 ms while the 100k history
loads in the real app, against under 1 ms while scrolling; D50 judges smoothness over the
scroll, so the judged statistics now cover the frames after the load, and the frames during it
are logged on a line of their own. The time to the first rows is unchanged.

The expected results above were first committed in `27f40e4`. Before any UI was built, an
independent review of them found gaps and errors, and they were corrected in the next commit to
this file: where a stash goes and how a lane is given out were stated (D54), the 100k repo's
colours, the view's geometry in check 8, the label and column sizes, the stash's parents, and
checks 14, 16 and 18 were made exact. Nothing about the app had been built against the first
version.

Phase 1 changes on purpose what phase 0's checks 1 to 8 show for a tab without a repository
(D42, D53). Their tests are updated in the same commit that changes the app:

- The tab is labelled "New tab" instead of "No repository", and the "+" button is enabled.
- The graph area shows the welcome page: its column headers are hidden, and under "No
  repository open" and "Open, clone or init a repository to see its history." come the buttons
  "Open", "Clone" and "Init", the heading "Recent repositories" and "No recent repositories".
- The colour sample that phase 0 took from the graph's column headers is taken from the tab
  strip instead, since the headers are hidden. The samples phase 0 took inside the graph area
  (such as at 480,400 in check 6, and below the column headers) are moved to spots the welcome
  page leaves empty, since its buttons and recent list may cover the old ones.

The left panel, toolbar, details panel and status bar of a tab without a repository are
unchanged.

## Found by the gate

Filled in when the gate runs.

## Could not run

Filled in when the gate runs.

## Other platforms

Filled in when the gate runs.

## macOS checklist for the owner

Filled in when the gate runs.
