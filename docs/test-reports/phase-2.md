# Phase 2 visual test report — Diff and commit workflow

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

Filled in when the gate runs.

## Scenario repos

The checks start from these repos; `ScenarioTests` pins their content and ids.

- **Changes scenario** (`Scenarios.ChangesAsync`): two commits on `main` by "Test Author"
  <author@example.com>: `28fb900` "Initial commit" (2026-01-01 12:00 UTC) and `06faadc` "Add
  calculator" (12:01), HEAD on `main`. `user.name` and `user.email` are set in the repo's own
  configuration (D72). The index and the working tree hold:

  | File | Staged (HEAD → index) | Unstaged (index → working tree) |
  |---|---|---|
  | `README.md` | M: a "## Usage" section added (lines 4 to 7) | M: line 3 "A small calculator for tests." becomes "A small calculator for the visual checks." |
  | `config/settings.json` | A: 4 lines | |
  | `src/helpers.py` | R from `src/util.py` (82% similar): line 1 `"""Small helpers for the calculator."""` becomes `"""Helpers shared by the calculator."""` | |
  | `assets/logo.png` | | M: a 48×48 indigo (`#4353D8`) image becomes a 64×48 green (`#1E8E5A`) one, each with a white 16×16 square in its middle; 7,028 bytes become 9,332 |
  | `data/blob.bin` | | M: binary, the 256 bytes 0 to 255 become 320 bytes (64 bytes of `0xFF` added) |
  | `data/large.txt` | | M: all 30,000 lines change, "Line 00001 of the large file." to "Row 00001 of the large file." and so on: 30,000 lines removed and 30,000 added |
  | `docs/guide.md` | | A: untracked, 4 lines: "# Guide", "", "Add numbers with Add.", "Subtract them with Subtract." |
  | `docs/old-notes.txt` | | D: 2 lines, "Old notes." and "They are out of date." |
  | `src/Calculator.cs` | | M: two hunks, below |

  `git diff -- src/Calculator.cs` (index → working tree) prints these two hunks:

  ```
  @@ -12,7 +12,7 @@ public sealed class Calculator
   
       public int Add(int a, int b)
       {
  -        return a + b;
  +        return a + b + _offset;
       }
   
       public int Subtract(int a, int b)
  @@ -29,4 +29,8 @@ public sealed class Calculator
       {
           return a / b;
       }
  +
  +    public int Negate(int a) => -a;
  +
  +    public int Square(int a) => a * a;
   }
  ```

- **CRLF scenario** (`Scenarios.CrlfAsync`): one commit `5b1f13c` "Add notes" (2026-01-01 12:00
  UTC) with `notes.txt`, the 20 lines "Note 1" to "Note 20". The repo's own configuration sets
  `core.autocrlf=true`, so the index holds LF and the working tree CRLF. In the working tree,
  line 2 reads "Note 2, changed" and line 18 "Note 18, changed", with CRLF. `git diff` prints
  two hunks, `@@ -1,5 +1,5 @@` and `@@ -15,6 +15,6 @@ Note 14`.
- **Linear scenario** (phase 0): 3 commits on `main`, a clean working tree.

Commits that a check makes through the app are by "Test Author" <author@example.com>, from the
repo's configuration, dated 2026-01-02 09:00 UTC by the harness (D72). Plain git, given the same
index, message, identity and date, makes these commits, which the checks expect:

| Made in | Commit | Message | Parent |
|---|---|---|---|
| Check 14 | `f976a24` (`f976a24c1cb298e1c293542624fb96fe0b2d81c5`) | "Add usage and settings", a blank line, "Explains how to use the calculator." | `06faadc` |
| Check 15 | `f9add9d` (`f9add9d8b5d8ae985965e5a4215dd46b0d0a0517`), replacing `f976a24` | "Add usage, settings and a guide", a blank line, "Explains how to use the calculator." | `06faadc` |

Changes made outside the app, as a terminal would, go through `TempRepo` with its fixed clock.

## What phase 2 must show

Sizes are in logical pixels; a tolerance of 2 pixels is allowed. Everything phase 1's report
describes stays as it is, except where this section adds to it. At 1100×700 the graph area is
440 wide and 586 high (as in phase 1); the right panel is 400 wide.

### New colours

| Token | Dark | Light | Used for |
|---|---|---|---|
| `VcDiffAddedBrush` | `#1A2E24` | `#E6F6EC` | An added line, gutter included |
| `VcDiffAddedWordBrush` | `#2B5A3F` | `#B4E5C6` | The changed words of an added line |
| `VcDiffRemovedBrush` | `#331D23` | `#FBE9EB` | A removed line, gutter included |
| `VcDiffRemovedWordBrush` | `#6A2D37` | `#F3BAC1` | The changed words of a removed line |
| `VcDiffHunkBrush` | `#1C2130` | `#EEF0FB` | A hunk's header row |
| `VcDiffFillerBrush` | `#181A20` | `#F3F4F6` | Side by side: the empty side of a row that has a line on one side only |
| `VcBackdropBrush` | `#99000000` | `#66000000` | The dimmed backdrop behind a dialog (black, 60% and 40% opaque) |

A context line, and the background of the diff view's body, is the window background
(`#14161B` dark, `#FFFFFF` light). A disabled button's text is `VcTextDisabledBrush`.

### The working-changes row (D60)

While the repository has changes (staged, unstaged, untracked or conflicted files), the graph
shows one more row above its first commit, 26 high like the others; every commit row moves down
by one row. When the changes are gone, the row goes and the commits move back up. When the row
appears or goes while the graph is scrolled away from the top, the rows in view stay where they
are; when it is at the top, the new row is shown.

- **Graph column:** a ring 10 across, 2 wide, in the colour of the lane HEAD's commit is in,
  centred on that lane (as a node is) and on the row; inside it the row's background. From the
  ring's bottom a dashed line, 2 wide, in the same colour, runs straight down to the top of
  HEAD's node: a dash of 3 starting at the ring's bottom, then a gap of 3, and so on. It is drawn
  under the rows it passes: a node, a ring or a line of another row in its lane is drawn over
  it. With no commits yet (an unborn HEAD) the ring is in lane 0, colour 0, without the line.
  UI Automation sees the row as part of the graph (D49); the real-window pass clicks it by
  position, 13 below the top of the rows.
- **Message column:** "Working changes" in the secondary text colour. The other columns are empty.
- It is selected, hovered and clicked like any row. With it selected, Down selects the first
  commit; with the first commit selected, Up selects it; Home selects it. When the row goes
  while it is selected, nothing is selected and the details panel shows its placeholder, except
  after a commit or amend, which selects the new commit.
- Selecting it shows the stage panel in the right panel. Selecting a commit shows the commit's
  details, as in phase 1.

For the changes scenario the graph area at 1100×700 shows: row 0 the working-changes row (ring in
lane 0, colour 0: `#7C8CFF` dark, `#4353D8` light); row 1 "Add calculator" with the label `main`
(check mark) on a filled node in lane 0; row 2 "Initial commit". The columns are Branch / Tag,
Graph (48, one lane), Message and Date, as phase 1's rules give.

### The stage panel

Shown in the right panel while the working-changes row is selected, from the top:

1. **Header**, 36 high as phase 1's: "Working changes" (semibold) at the left; at the right the
   two toggles "Flat" (`StageFlatButton`) and "Tree" (`StageTreeButton`), as phase 1's Flat and
   Tree, which change the same setting (D70, D44).
2. **Restore bar**, 40 high, only after a discard (D62, D73): on the control background, with a
   line under it, "Discarded changes to <file name>." (one file) or "Discarded changes to <N>
   files." (`RestoreBarText`) at the left; at the right a "Restore" button
   (`RestoreDiscardButton`) and a close button with an "×" icon (`DismissRestoreButton`, named
   "Close"). Restore puts the files back and closes the bar; the close button closes it and
   leaves the files discarded; the next discard replaces it.
3. **Unstaged header**, 34 high (the line under the panel header, or under the restore bar,
   separates it from what is above): "Unstaged files (N)" (`UnstagedTitle`) in the heading
   style of phase 1's "Changed files (N)"; at the right the buttons "Stage all"
   (`StageAllButton`) and "Discard all" (`DiscardAllButton`), shown only when the list has files.
4. **Unstaged list** (`UnstagedFileList`): the files with unstaged changes and the untracked
   files. Rows 24 high, as
   the commit details' file rows: a status letter (A for an added or untracked file, in the
   success colour; M, warning; D, danger; R, accent; U for a conflicted file, danger), the file
   name, and in the secondary colour its folder (flat list only); a renamed file adds "renamed
   from <old path>". Flat, the files are in the byte order of their paths; as a tree, folders
   come first, then files, each by name, all folders open, as phase 1's tree. An empty list
   shows "No unstaged changes" in the secondary colour, 12 from the left and 8 from the top.
5. **Staged header**, 34 high, with a line above it: "Staged files (N)" (`StagedTitle`); at the
   right "Unstage all" (`UnstageAllButton`), shown only when the list has files.
6. **Staged list** (`StagedFileList`): as the unstaged list, for the files whose index differs
   from HEAD; empty: "No staged changes".
7. **Commit area**, with a line above it, 12 of padding on every side, from the top: the
   "Summary" box (`CommitSummary`, 32 high, one line, the hint "Summary"), with at its right
   inside the box, in the secondary colour, the characters left of 72 (`SummaryCounter`: "72"
   when empty; negative, in the warning colour, past 72); 8 below it the "Description" box
   (`CommitDescription`, 80 high, several lines, wrapped, the hint "Description"); 8 below it
   the check box "Amend previous commit" (`AmendCheckBox`), its row 20 high; 8 below it the
   commit button (`CommitButton`), 32 high and as wide as the area, with the accent background
   and the accent-text colour when enabled, and the control background with the disabled text
   colour when disabled. Its label is "Commit 1 file" or "Commit N files" for the N staged
   files; "Amend previous commit" while the check box is ticked; "Stage files to commit" when
   nothing is staged and the box is not ticked. It is enabled only when the summary has text
   other than spaces and there is something to do (staged files, or the box ticked). While a
   commit runs it says "Committing…" and is disabled. When a commit fails, git's own message
   shows under the button in the danger colour, wrapped (`CommitError`), 8 below it, until the
   next attempt; the commit area grows by its height and the lists give way.

The two lists share the height the other parts leave, half each. At 1100×700 with no restore
bar the panel's content is 550 high, the commit area 212 and the headers 34 each, which leaves
135 for each list: five whole rows and part of a sixth.

A file row (`StageFileRow`, named by the file's path) shows, while the mouse is over it and
while it is selected, icon buttons 22 by 22 at its right end, 2 apart and 6 from the edge: on an
unstaged file "Stage" (`RowStageButton`, a plus) and "Discard" (`RowDiscardButton`, an arrow
turning back); on a staged file "Unstage" (`RowUnstageButton`, a minus); each named so for UI
Automation and in its tooltip. Clicking a row's button acts on that file only: it does not
select the row or open its diff. Clicking anywhere else on the row selects it (the selection
background; one selected file across both lists) and opens its diff (D63). A file row's tooltip
shows its whole path. Unstaging a renamed file unstages both its paths: the new one leaves the
index and the old one gets HEAD's entry back.

Ticking "Amend previous commit" fills the summary and description with HEAD's subject and body
when both are empty; otherwise it leaves them. It is disabled when HEAD has no commit. After a
successful commit or amend, the summary and description are emptied and the box unticked; the
working-changes row stays selected while there are changes, and when there are none the new
commit is selected instead.

### The diff view (D61, D63 to D65)

Opening a file's diff replaces the graph area (column headers and rows) with the diff view; the
left and right panels stay. Its close button or Esc closes it and shows the graph again, as it
was; closing clears the file row's selection. Clicking a ref in the left panel, or a parent link,
also closes it. Each tab keeps its own. The view always shows the file's current diff: after
any write the app makes (stage, unstage, discard, restore, commit, amend) or a change made
outside the app, it is read again, keeping its scroll position where it can; when the diff it
shows has become empty, the view closes. When it opens, the diff's body has the keyboard focus.
Esc closes it from anywhere in the window except while a text box has the focus; while a
dialog is open, Esc closes only the dialog.

- **Header**, 36 high, on the chrome background with a line under it: at the left the status
  letter (coloured as in the lists), 6 after it the file name (semibold), 8 after that the folder
  in the secondary colour, and for a rename "renamed from <old path>" after it; this text ends in
  "…" when it does not fit, with the whole path in a tooltip. At the right, in the secondary
  colour, what is compared: "Unstaged" (index → working tree), "Staged" (HEAD → index), or a
  commit's short id (its first parent → the commit; a root commit against nothing); then the
  close button, 28 by 28, with an "×" icon (`CloseDiffButton`, named "Close").
- **Toolbar**, 36 high, on the chrome background with a line under it: at the left the toggles
  "Inline" (`InlineDiffButton`) and "Side by side" (`SideBySideDiffButton`), as Flat and Tree,
  the one in use with the selection background; hidden for an image or a binary file. At the
  right the actions: for an unstaged diff "Stage file" (`StageFileButton`) and "Discard file"
  (`DiscardFileButton`); for a staged diff "Unstage file" (`UnstageFileButton`); for a commit's
  diff none. While lines are selected (below), "Stage lines" (`StageLinesButton`) and "Discard
  lines" (`DiscardLinesButton`), or "Unstage lines" (`UnstageLinesButton`), take the place of
  the file's buttons.
- **Body**: the rest of the area (514 high at 1100×700).

**Text.** Code is drawn in JetBrains Mono NL, 12 pixels: 7.2 wide per character and 15.84 high
per row (the font's line height at that size). A row's background: added `VcDiffAddedBrush`,
removed `VcDiffRemovedBrush`, context the window background. Each side has a gutter: number
columns, each 8 + 7.2 × d + 8 wide, where d is the number of digits of the largest number in that
column (at least 2), with the numbers in the code font, right-aligned 8 from the column's right
edge, in the secondary colour and without thousands separators; then a sign column 16 wide with
"+" or "-" centred (in the secondary colour) for an added or removed line. The gutter takes its
row's background. Lines are not wrapped: a line wider than its column is cut at the column's
edge, and a horizontal scroll bar under the body scrolls the text (side by side, both halves
together); the gutter does not scroll.

**Inline.** One column. The gutter has two number columns, old then new: a context line shows
both, a removed line the old only, an added line the new only. Within a change, the removed
lines come before the added ones, as git prints them.

**Side by side.** Two halves of equal width with a line between them: the old file on the left
(one number column), the new on the right. A context line shows on both sides, on the same row.
In a change of n removed and m added lines, the removed lines fill the left side and the added
lines the right, from the change's first row down, over max(n, m) rows; a side with fewer lines
has filler rows below them, with `VcDiffFillerBrush` and no number or sign. Both halves scroll
together.

**Hunk headers.** Each hunk starts with a header row 26 high on `VcDiffHunkBrush` across the
whole width, gutter included, with git's header line ("@@ -12,7 +12,7 @@ public sealed class
Calculator") in the code font, in the secondary colour, from the start of the text column. At
the right end of the row are the hunk's buttons: text buttons 22 high, centred in the row, with
8 of padding on each side of their label, 4 apart and 6 from the row's right edge: for an
unstaged diff "Stage hunk" (`StageHunkButton`) and "Discard hunk" (`DiscardHunkButton`); for a
staged diff "Unstage hunk" (`UnstageHunkButton`); for a commit's diff none. The header text is
cut with "…" 8 before the first button. The buttons of the first hunk come first in UI
Automation's order, then those of the second, and so on. Side by side, the header line is on
the left half and the buttons at the right end of the right half. Git's "\ No newline at end of
file" shows as a row of its own in the secondary colour, without numbers.

**Syntax highlighting** (D64): by the file's extension, with the colours and font styles of
Visual Studio Code's Dark+ theme (dark) or Light+ theme (light); text the theme leaves alone is
in the main text colour. When several rules of the theme match a token, the first that
TextMateSharp's `Theme.Match` returns with a colour wins, as in Visual Studio Code: a theme's own
rule before the one it includes. A file with no grammar (`.txt`) is all in the main text
colour. For the scenario's lines:

| Text | Dark | Light |
|---|---|---|
| `public`, `int` in `src/Calculator.cs`; `def` in Python | `#569CD6` | `#0000FF` |
| `return` (a control keyword) | `#C586C0` | `#AF00DB` |
| `Add`, `Negate`, `Square`, `clamp` (method and function names) | `#DCDCAA` | `#795E26` |
| `a`, `b`, `_offset` (parameters and fields) | `#9CDCFE` | `#001080` |
| `+`, `-`, `*`, `/`, `=>` (operators) | `#D4D4D4` | `#000000` |
| `"""Helpers shared by the calculator."""` (a Python string) | `#CE9178` | `#A31515` |
| `# Calculator`, `## Usage` (Markdown headings), in bold | `#569CD6` | `#800000` |
| `{`, `}`, `;`, `(`, `)`, and the line "A small calculator for the visual checks." | the main text colour | the main text colour |

**Word-level highlights.** In a change, the first removed line is paired with the first added
line, the second with the second, and so on, as far as both sides have lines. The words of a
pair (runs of letters, digits and `_`; runs of white space; each other character alone) are
compared in order: those in a longest common sequence of the two lines' words keep the plain
background, and the others have the stronger one, `VcDiffRemovedWordBrush` on the removed line
and `VcDiffAddedWordBrush` on the added one, touching ones as one run. Where several sequences
are equally long, a shared word is matched as early as it can be in the added line. A pair
whose common sequence holds no run of letters, digits or `_` has no highlights. For the
scenario:

- `README.md`, unstaged: the removed line 3 has "tests" highlighted; the added line 3 has "the
  visual checks" highlighted; "A small calculator for" and the full stop are plain on both.
- `src/Calculator.cs`, line 15: the removed line `return a + b;` has nothing highlighted; the
  added line `return a + b + _offset;` has ` + _offset` highlighted, and `return a + b` and `;`
  plain.
- `src/helpers.py`, staged, line 1: the removed line has `Small`, `helpers` and `for`
  highlighted; the added line has `Helpers`, `shared` and `by` highlighted; the quotes, the
  spaces between those words and ` the calculator.` are plain on both.

**Selecting lines.** In a working-changes diff, clicking a row's line number selects that row;
Shift with a click on another row's number selects every row from the first to it; dragging
over the text selects the rows the selection touches. Selected rows have the selection
background (`#2A3150` dark, `#DDE1FA` light) behind their text. When the selection holds at
least one added or removed line, the line buttons appear (above); they act on those added and
removed lines only. Side by side, rows selected on the left count their removed lines, on the
right their added lines.

**Very large diffs** (D65). For a diff of more than 10,000 lines in its hunks (hunk headers not
counted) or more than 1 MiB of text, the body shows, centred: "Very large diff" (14, semibold),
under it "N lines removed and M added." in the secondary colour, and under that a button "Show
diff" (`ShowLargeDiffButton`). The toolbar's file buttons stay. "Show diff" shows the diff as
above but without syntax highlighting or word-level highlights.

**Binary files.** The body shows, centred: "Binary file" (14, semibold), and under it "Before: N
bytes" and "After: N bytes" on two lines in the secondary colour (an added file only "After", a
deleted file only "Before").

**Nothing to show.** A diff without hunks that is not binary (an empty new file, a change of
mode only) shows, centred in the secondary colour, "No changes to the file's content.".

Numbers in the app's texts (not in the gutter) have a comma as thousands separator, whatever the
system's culture: "30,000", "7,028".

**Images** (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.webp` that git calls binary). The body has
two halves, "Before" (left) and "After" (right), each with that heading at its top (in the
heading style), 12 from the top, and under it the image, centred in its half, at its size in
pixels taken as logical pixels (scaled down to fit its half, never up), with a 1-pixel border
in the border colour, and under the image "<width> × <height> pixels, <N> bytes" in the
secondary colour. An added file's "Before" half and a deleted file's "After" half say "No
image" instead.

### The confirmation dialog (D62, D69)

A discard of any kind first shows a dialog over the whole window: the window is covered by the
backdrop colour, and in its middle a card 400 wide (narrower windows: 40 less than the window)
on the panel background with a 1-pixel border and corners rounded by 8, padding 20. On the card:
the title "Discard changes?" (16, semibold); 12 below it the question (`ConfirmQuestion`),
wrapped:

- one file: "Discard the changes to <file name>?"
- several files: "Discard the changes to <N> files?"
- a hunk: "Discard this hunk of <file name>?"
- lines: "Discard 1 selected line of <file name>?" or "Discard <N> selected lines of <file name>?"

and under it "A snapshot is saved first, so the changes can be restored." in the secondary
colour; 20 below that, right-aligned, the buttons "Cancel" (`CancelButton`) and "Discard"
(`ConfirmButton`, with the danger background and white text), 8 apart. The Cancel button has
the keyboard focus (asserted through the focus manager; a screenshot need not show a focus
border). Esc or Cancel closes the dialog and changes nothing; Discard saves the snapshot (D67),
discards, and shows the restore bar.

### Settings

`settings.json` gets `diffMode`, "Inline" (the default) or "SideBySide" (D61).

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Unless a check says otherwise, it
starts by writing a session with one tab on a fresh copy of the named scenario and starting the
app at 1100×700 in the dark theme, then waits until the graph has drawn its rows.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Working-changes row and stage panel, dark, 1100×700 | 1. Changes scenario. Screenshot. 2. Click the working-changes row. Screenshot. 3. Click the row "Add calculator", then press Up; then press Down, then Home. 4. Hover over the unstaged file `src/Calculator.cs` (scrolling the list to it). Screenshot. | After step 1: the graph as "What phase 2 must show" gives for the changes scenario: the working-changes row, then "Add calculator" with `main` (check mark), then "Initial commit"; nothing selected; the details panel says "Select a commit to see its details.". After step 2: the row has the selection background; the right panel's header says "Working changes" with Flat selected; "Unstaged files (7)" with Stage all and Discard all, and in this order: M `README.md`; M `logo.png` `assets`; M `blob.bin` `data`; M `large.txt` `data`; A `guide.md` `docs`; D `old-notes.txt` `docs`; M `Calculator.cs` `src` (the list scrolls: five whole rows show); "Staged files (3)" with Unstage all: M `README.md`; A `settings.json` `config`; R `helpers.py` `src` renamed from `src/util.py`; the commit area with empty boxes, the counter "72", the box not ticked and the disabled button "Commit 3 files". After step 3: Up selects the working-changes row again and the stage panel shows; Down selects "Add calculator" and its details show; Home selects the working-changes row. After step 4: the row shows the Stage and Discard buttons, with the hover background. No text is clipped except text that ends in "…" by phase 1's rules. | | Steps 1 and 2 | | |
| 2 | Stage panel in light, 1920×1080, as a tree | 1. As check 1, light theme, 1920×1080. 2. Click the working-changes row. Screenshot. 3. Click "Tree". Screenshot. 4. Click "Flat". | After step 2: as check 1 step 2 in the light colours, with all seven unstaged files showing without scrolling, and all six graph columns. After step 3: both lists as trees: unstaged: `assets` (M `logo.png`), `data` (M `blob.bin`, M `large.txt`), `docs` (A `guide.md`, D `old-notes.txt`), `src` (M `Calculator.cs`), then M `README.md`; staged: `config` (A `settings.json`), `src` (R `helpers.py`, renamed from `src/util.py`), then M `README.md`; `settings.json`'s `fileList` is "Tree". After step 4 the lists are flat again and `fileList` is "Flat". | | n/a | | |
| 3 | Diff of a modified file, inline and side by side, 1400×900 | 1. Changes scenario at 1400×900 (a size the real window also has, wide enough for side by side: the graph area is 740 wide); click the working-changes row. 2. Click the unstaged `src/Calculator.cs` (scrolling the list to it). Screenshot. 3. Click "Side by side". Screenshot. 4. Press Esc. Screenshot. | After step 2: the diff view in place of the graph; the header: M `Calculator.cs` `src`, "Unstaged", the close button; the toolbar: Inline selected, Stage file, Discard file. The body, inline, rows in this order: the header row of `@@ -12,7 +12,7 @@ public sealed class Calculator` with Stage hunk and Discard hunk; context 12/12 (empty), 13/13 `public int Add(int a, int b)`, 14/14 `{`; removed 15 `return a + b;`; added 15 `return a + b + _offset;`; context 16/16 `}`, 17/17 (empty), 18/18 `public int Subtract(int a, int b)`; the header row of `@@ -29,4 +29,8 @@ public sealed class Calculator` with its buttons; context 29/29 `{`, 30/30 `return a / b;`, 31/31 `}`; added 32 (empty), 33 `public int Negate(int a) => -a;`, 34 (empty), 35 `public int Square(int a) => a * a;`; context 32/36 `}`. Row backgrounds and signs as "Text" says; the word-level highlights of line 15 as listed; the syntax colours as listed. The file row is selected in the stage panel. After step 3: side by side, the same hunks: on rows aligned as "Side by side" says: the removed line 15 on the left facing the added line 15 on the right; in the second hunk, after 31/31 `}`, four rows with fillers on the left and 32 to 35 on the right, then 32/36 `}`; Side by side selected; `settings.json`'s `diffMode` is "SideBySide". After step 4: the graph again, with the working-changes row selected and no file row selected. | | Steps 2 and 3 | | |
| 4 | Added, deleted and renamed files, both modes, light, 1920×1080 | 1. Changes scenario, light theme, 1920×1080; click the working-changes row. 2. Click the unstaged `docs/guide.md`. Screenshot. 3. Click "Side by side". Screenshot. 4. Click the unstaged `docs/old-notes.txt`. Screenshot. 5. Click "Inline". Screenshot. 6. Click the staged `src/helpers.py`. Screenshot. 7. Click "Side by side". Screenshot. | Step 2: A `guide.md` `docs`, "Unstaged"; one hunk `@@ -0,0 +1,4 @@` with Stage hunk and Discard hunk; four added rows 1 to 4: `# Guide` (the heading colour, bold), empty, `Add numbers with Add.`, `Subtract them with Subtract.`. Step 3: the left side four filler rows, the right the four added lines. Step 4: side by side (the mode is kept): D `old-notes.txt` `docs`; one hunk `@@ -1,2 +0,0 @@`; the left side the removed lines 1 "Old notes." and 2 "They are out of date.", the right side two filler rows. Step 5: inline, the two removed rows. Step 6: inline: R `helpers.py` `src` renamed from `src/util.py`, "Staged"; the toolbar has Unstage file; one hunk `@@ -1,4 +1,4 @@` with Unstage hunk only; removed 1 `"""Small helpers for the calculator."""`, added 1 `"""Helpers shared by the calculator."""`, both in the string colour, with the word-level highlights listed under "Word-level highlights"; then context 2/2 and 3/3 (empty) and 4/4 `def clamp(value, low, high):` (`def` in the keyword colour, `clamp` in the function colour). Step 7: the same side by side, the removed line 1 facing the added line 1. All in the light colours. | | n/a | | |
| 5 | Image diff | 1. Changes scenario; click the working-changes row. 2. Click the unstaged `assets/logo.png`. Screenshot. | The header M `logo.png` `assets`, "Unstaged"; the toolbar without Inline and Side by side, with Stage file and Discard file. The body: "Before" with the 48×48 indigo image with its white square and "48 × 48 pixels, 7,028 bytes"; "After" with the 64×48 green image with its white square and "64 × 48 pixels, 9,332 bytes". Sampled: the middle of each image is white; a point 4 inside each image's left edge is `#4353D8` (before) and `#1E8E5A` (after). | | Yes | | |
| 6 | Binary file | 1. As check 5, but click the unstaged `data/blob.bin`. Screenshot. | The header M `blob.bin` `data`; no Inline or Side by side; the body: "Binary file", "Before: 256 bytes", "After: 320 bytes". | | n/a | | |
| 7 | Very large file | 1. As check 5, but click the unstaged `data/large.txt`. Screenshot. 2. Click "Show diff". Screenshot. 3. Press Ctrl+End. Screenshot. 4. Click "Side by side". Screenshot. | After step 1: "Very large diff", "30,000 lines removed and 30,000 added.", and the button "Show diff"; the toolbar has Inline (selected), Side by side, Stage file and Discard file. After step 2: the hunk header `@@ -1,30000 +1,30000 @@` with its buttons, then the removed rows from old 1 "Line 00001 of the large file." on; no word-level highlights (the word "Line" of the first row has the plain removed background) and the text in the main text colour. After step 3 (the diff's body has had the keyboard focus since it opened): the last rows: added 29998 to 30000 (the gutter's form), the last "Row 30000 of the large file.", its bottom at the body's bottom. After step 4: side by side, the removed line 1 on the left facing the added line 1 on the right in the first row under the hunk header ("Line 00001 …" and "Row 00001 …", each cut at its half's edge), without word-level highlights. | | n/a | | |
| 8 | A commit's file opens its diff | 1. Changes scenario. 2. Click the row "Add calculator". Screenshot. 3. Click `Calculator.cs` in the details panel's file list. Screenshot. 4. Click the close button. Screenshot. | After step 2: the details of "Add calculator": "Changed files (6)": A `logo.png` `assets`, A `blob.bin` `data`, A `large.txt` `data`, A `old-notes.txt` `docs`, A `Calculator.cs` `src`, A `util.py` `src`. After step 3: the diff view: A `Calculator.cs` `src`, "06faadc"; the toolbar with Inline and Side by side and no actions; one hunk `@@ -0,0 +1,32 @@` without buttons; 32 added rows from 1 `namespace Demo;`; the file row has the selection background. After step 4: the graph with "Add calculator" still selected and its details shown; no file row selected. | | n/a | | |
| 9 | Stage and unstage whole files | 1. Changes scenario; click the working-changes row. 2. Hover over the unstaged `docs/guide.md` and click its Stage button. Screenshot. 3. Click "Stage all". Screenshot. 4. Click "Unstage all". Screenshot. | After step 2: the graph area still shows the graph (a row's button opens no diff); Unstaged files (6), without `guide.md`; Staged files (4): `README.md`, `settings.json`, `guide.md` (A, `docs`), `helpers.py`; the button "Commit 4 files". `git status` agrees. After step 3: "No unstaged changes" with "Unstaged files (0)" and no Stage all or Discard all; Staged files (9): M `README.md`, M `logo.png`, A `settings.json`, M `blob.bin`, M `large.txt`, A `guide.md`, D `old-notes.txt`, M `Calculator.cs`, R `helpers.py` renamed from `src/util.py`; `git diff` (index → working tree) is empty. After step 4: "No staged changes", "Staged files (0)"; Unstaged files (10): M `README.md`, M `logo.png`, A `settings.json`, M `blob.bin`, M `large.txt`, A `guide.md`, D `old-notes.txt`, M `Calculator.cs`, A `helpers.py`, D `util.py` (the rename is now an untracked file and a deleted one); the index equals HEAD (`git diff --cached` is empty); the working tree is unchanged (its files have the same bytes as before step 2). | | n/a | | |
| 10 | Stage one hunk | 1. As check 3, steps 1 and 2. 2. Click "Stage hunk" in the first hunk's header. Screenshot. 3. Click the staged `src/Calculator.cs`. Screenshot. | After step 2: the unstaged diff has one hunk left, `@@ -29,4 +29,8 @@ public sealed class Calculator`, with the same rows as before; Staged files (4) now has M `Calculator.cs` `src`, and Unstaged files still (7). `git diff --cached -- src/Calculator.cs` is exactly the first hunk, `@@ -12,7 +12,7 @@`, changing line 15; the working file is unchanged. After step 3: the diff view shows "Staged" and that one hunk with Unstage hunk; the toolbar has Unstage file. | | Steps 2 and 3, with real clicks on the hunk button | | |
| 11 | Stage one line, then unstage it | 1. As check 10, steps 1 and 2. 2. Click the line number 35 (the added `public int Square(int a) => a * a;`). Screenshot. 3. Click "Stage lines". Screenshot. 4. Click the staged `src/Calculator.cs`. Screenshot. 5. Click the line number 32 (the added `public int Square(int a) => a * a;` of the staged diff), then "Unstage lines". Screenshot. | After step 2: that row has the selection background; the toolbar shows Stage lines and Discard lines instead of Stage file and Discard file. After step 3: the unstaged diff is one hunk `@@ -29,5 +29,8 @@ public sealed class Calculator`: context 29/29 `{`, 30/30 `return a / b;`, 31/31 `}`; added 32 (empty), 33 `public int Negate(int a) => -a;`, 34 (empty); context 32/35 `public int Square(int a) => a * a;`, 33/36 `}`. The index's `src/Calculator.cs` is the committed file with line 15 changed and `    public int Square(int a) => a * a;` inserted after line 31 (33 lines); `git diff --cached -- src/Calculator.cs` has the two hunks `@@ -12,7 +12,7 @@` and `@@ -29,4 +29,5 @@`. After step 4: the staged diff with those two hunks; the second adds the one line 32. After step 5: the staged diff is the one hunk `@@ -12,7 +12,7 @@ public sealed class Calculator`; the index's file is the committed one with line 15 changed; the unstaged diff's second hunk is `@@ -29,4 +29,8 @@ public sealed class Calculator` again. | | n/a | | |
| 12 | Unstage a hunk | 1. Changes scenario; click the working-changes row. 2. Click the staged `README.md`. Screenshot. 3. Click "Unstage hunk". Screenshot. | After step 2: "Staged"; one hunk `@@ -1,3 +1,7 @@` with Unstage hunk: context 1 to 3, then added 4 (empty), 5 `## Usage` (the heading colour), 6 (empty), 7 `Create a Calculator and call Add.`. After step 3: `README.md` has left the staged list (Staged files (2)); the diff view is closed, because the staged diff it showed is empty; Unstaged files still (7), and the unstaged `README.md` now compares with HEAD's: `git diff -- README.md` changes line 3 and adds lines 4 to 7; the index's `README.md` is HEAD's. | | n/a | | |
| 13 | Line endings with `core.autocrlf` | 1. CRLF scenario; click the working-changes row; click `notes.txt`. Screenshot. 2. Click "Stage hunk" in the first hunk. 3. Click "Discard hunk" in the remaining hunk, then "Discard" in the dialog. Screenshot. | After step 1: two hunks, `@@ -1,5 +1,5 @@` and `@@ -15,6 +15,6 @@ Note 14`, removed and added lines "Note 2" / "Note 2, changed" and "Note 18" / "Note 18, changed"; no row shows a carriage-return mark or any character after its text. After step 3: the diff view has closed (its diff is empty) and the graph shows; the unstaged list is empty and the staged list has M `notes.txt`; the index's `notes.txt` is the committed text with line 2 "Note 2, changed", all lines ending in LF; the working file has the same text with every line ending in CRLF (line 18 back to "Note 18"); the restore bar says "Discarded changes to notes.txt.". | | n/a | | |
| 14 | Commit | 1. Changes scenario; click the working-changes row. 2. Click the Summary box and type "Add usage and settings". Screenshot. 3. Click the Description box and type "Explains how to use the calculator.". 4. Click "Commit 3 files". Screenshot. | After step 2: the counter says "50"; the button "Commit 3 files" is enabled. After step 4: the graph shows the working-changes row (still selected), then "Add usage and settings" with `main` (check mark), dated 2026-01-02 09:00, then "Add calculator" (no label), then "Initial commit"; the new commit is `f976a24` with the message as in "Scenario repos", by Test Author <author@example.com>, its parent `06faadc`. The stage panel: "No staged changes", the same seven unstaged files, empty boxes, "72", and the disabled "Stage files to commit". The left panel shows `main`; the status bar `main`. | | Yes, typing with the real keyboard | | |
| 15 | Amend | 1. As check 14. 2. Stage `docs/guide.md` with its row's Stage button. 3. Tick "Amend previous commit". Screenshot. 4. Click the Summary box, select all its text (Ctrl+A) and type "Add usage, settings and a guide". 5. Click the commit button (`CommitButton`), which now says "Amend previous commit". Screenshot. | After step 3: the summary "Add usage and settings" and the description "Explains how to use the calculator." (the boxes were empty); the button says "Amend previous commit" and is enabled. After step 5: the graph shows the working-changes row, then "Add usage, settings and a guide" with `main`, dated 2026-01-02 09:00, then "Add calculator", "Initial commit"; no row for `f976a24`. HEAD is `f9add9d`, whose parent is `06faadc` and whose tree has `docs/guide.md`. The stage panel: "No staged changes", Unstaged files (6) without `guide.md`, the boxes empty and not ticked. | | n/a | | |
| 16 | A commit that git refuses shows git's message | 1. Changes scenario, with a `pre-commit` hook in its git folder (a `#!/bin/sh` script, executable) that prints "Commit blocked by the test hook" to standard error and exits with 1. 2. Click the working-changes row, type the summary "Blocked", click "Commit 3 files". Screenshot. | Under the button, in the danger colour: "Commit blocked by the test hook". HEAD is still `06faadc`; the graph, the staged list (3) and the summary "Blocked" are unchanged; the button is enabled again. | | n/a | | |
| 17 | Discard a file, cancel, discard, restore, dismiss | 1. Changes scenario; click the working-changes row. 2. Hover over the unstaged `src/Calculator.cs` (scrolling the list to it) and click its Discard button. Screenshot. 3. Click "Cancel". 4. Click its Discard button again, then "Discard". Screenshot. 5. Click "Restore". Screenshot. 6. Click its Discard button again, then "Discard"; then click the restore bar's close button. Screenshot. | After step 2: the dialog: "Discard changes?", "Discard the changes to Calculator.cs?", the snapshot line, Cancel (with the keyboard focus) and Discard; the backdrop over the whole window; the graph area still shows the graph. After step 3: no dialog; nothing changed (the working file is as before). After step 4: no dialog; Unstaged files (6) without `Calculator.cs`; the working file is the committed text; the restore bar "Discarded changes to Calculator.cs." with Restore and its close button; a ref `refs/visualcommit/backup/discard-…` points at a commit whose parent is HEAD and whose `src/Calculator.cs` is the file as it was; the graph has no row for that commit and the left panel lists nothing under `refs/visualcommit`. After step 5: the restore bar is gone; Unstaged files (7) with `Calculator.cs` again; the working file has the bytes it had before step 4. The backup ref stays. After step 6: no restore bar; Unstaged files (6) without `Calculator.cs`; the working file is the committed text; two backup refs. | | Steps 2, 4 and 5 | | |
| 18 | Discard all, then restore, untracked and binary files included | 1. Changes scenario; click the working-changes row. 2. Click "Discard all". Screenshot. 3. Press Esc. 4. Click "Discard all", then "Discard". Screenshot. 5. Click "Restore". Screenshot. | After step 2: the dialog says "Discard the changes to 7 files?". After step 3: no dialog and nothing changed. After step 4: "No unstaged changes", Staged files (3) unchanged; the working tree equals the index: `docs/guide.md` is gone, `docs/old-notes.txt` is back, `README.md` has the staged text, the image, the binary file, `large.txt` and `Calculator.cs` have their committed bytes; the restore bar says "Discarded changes to 7 files.". After step 5: every one of the seven files has the bytes it had before step 4 (`docs/old-notes.txt` is deleted again and `docs/guide.md` back); Unstaged files (7) as in check 1; the index is unchanged throughout. | | n/a | | |
| 19 | Discard a hunk and a line | 1. As check 3, steps 1 and 2. 2. Click "Discard hunk" in the second hunk. Screenshot. 3. Click "Discard". Screenshot. 4. Click "Restore". 5. Click the line number 33 (`public int Negate(int a) => -a;`), then "Discard lines". Screenshot. 6. Click "Discard". Screenshot. | After step 2: the dialog says "Discard this hunk of Calculator.cs?", over the diff. After step 3: the diff shows only the first hunk; the working file has line 15 changed and no Negate or Square. After step 4: the file is as before step 3, and the diff shows both hunks again. After step 5: the dialog says "Discard 1 selected line of Calculator.cs?". After step 6: the working file is the scenario's except that the line `    public int Negate(int a) => -a;` is gone (35 lines); the second hunk is `@@ -29,4 +29,7 @@` with the added rows 32 (empty), 33 (empty), 34 `public int Square(int a) => a * a;`. | | n/a | | |
| 20 | Working-tree changes made outside the app appear | 1. Linear scenario (clean). Screenshot. 2. Outside the app, write a new file `notes.txt` with one line "A note". Wait until the working-changes row shows (at most 5 seconds). Screenshot. 3. Click it, then click `notes.txt` in the unstaged list. 4. Outside the app, append the line "Another note" to `notes.txt`. Wait until the diff shows it (at most 5 seconds). Screenshot. 5. Outside the app, run `git add notes.txt`. Wait until the staged list has it (at most 5 seconds). Screenshot. 6. Outside the app, add `build/` to `.git/info/exclude` and write `build/out.txt`; wait 2 seconds. 7. Outside the app, run `git rm --cached notes.txt` and delete `notes.txt`. Wait until the working-changes row is gone (at most 5 seconds). Screenshot. | After step 1: as phase 1's linear graph, without a working-changes row. After step 2: the working-changes row above "Describe the project", which is now row 1 with `main`; the dashed line from the ring to `main`'s node. After step 4: the diff of `notes.txt`: one hunk `@@ -0,0 +1,2 @@`, added 1 "A note" and 2 "Another note". After step 5: the diff view has closed (its unstaged diff is empty) and the graph shows; Staged files (1): A `notes.txt`; "No unstaged changes". After step 6: nothing changed (the ignored file does not show). After step 7: no working-changes row; the graph as after step 1; the right panel says "Select a commit to see its details.". | | Steps 1 and 2, the file written while the real app runs | | |
| 21 | The diff mode survives a restart, 1400×900 | 1. Changes scenario at 1400×900; click the working-changes row; click the unstaged `README.md`; click "Side by side". 2. Close the app and start a new instance on the same data folder, at 1400×900. 3. Click the working-changes row, then the unstaged `README.md`. Screenshot. | The diff is side by side: one hunk `@@ -1,6 +1,6 @@`; on the left 3 "A small calculator for tests." with "tests" highlighted, on the right 3 "A small calculator for the visual checks." with "the visual checks" highlighted, facing each other; context rows 1, 2 and 4 to 6 on both sides; `# Calculator` and `## Usage` in the heading colour, bold. `settings.json`'s `diffMode` is "SideBySide". | | Yes, a real process restart | | |
| 22 | Unstage a renamed file with its row's button | 1. Changes scenario; click the working-changes row. 2. Hover over the staged `src/helpers.py` and click its Unstage button. Screenshot. | Staged files (2): M `README.md`, A `settings.json`; Unstaged files (9): M `README.md`, M `logo.png`, M `blob.bin`, M `large.txt`, A `guide.md`, D `old-notes.txt`, M `Calculator.cs`, A `helpers.py`, D `util.py`; `git diff --cached --name-only` prints `README.md` and `config/settings.json`; the working tree is unchanged. | | n/a | | |
| 23 | The working-changes row when HEAD's commit is not the first | 1. Graph scenario of phase 1, with a new untracked file `todo.txt` ("Later") written before the app starts. Screenshot. | Row 0 is the working-changes row, its ring in lane 0 (colour 0); rows 1 to 13 are phase 1's table of the graph scenario, each one row lower: the stash's ring on row 1, "Highlight matches" on row 2, "Add settings page" with `main` (check mark) on row 3. The dashed line runs in lane 0 from the working-changes ring down to `main`'s node, under the stash's ring and lane 0's line, which are drawn over it; the stash's ring is as in phase 1. | | n/a | | |
| 24 | The first commit of a new repository | 1. Start the app on an empty data folder, 1100×700, dark; click "Init" and choose a new, empty folder `fresh`. 2. Outside the app, set `user.name` and `user.email` in `fresh`'s configuration to Test Author and author@example.com, and write `first.txt` ("First"). Wait until the working-changes row shows. Screenshot. 3. Click it. Screenshot. 4. Click the Stage button of `first.txt`, type the summary "First commit", click "Commit 1 file". Screenshot. | After step 2: the working-changes row on row 0, its ring in lane 0, colour 0, without a dashed line; "No commits yet" and its second line still in the middle of the rows area. After step 3: the stage panel with Unstaged files (1): A `first.txt`; the check box "Amend previous commit" is disabled. After step 4: no working-changes row; one commit row "First commit" with the branch git created (check mark), dated 2026-01-02 09:00, selected, and its details shown (Changed files (1): A `first.txt`); "No commits yet" is gone; its id is the one plain git gives for the same tree, message, identity and date. | | n/a | | |
| 25 | The summary counter and the commit button's rules | 1. Changes scenario; click the working-changes row. 2. Type a summary of 75 characters ("Explain how the calculator adds, subtracts, multiplies and divides numbers."). Screenshot. 3. Select all of it (Ctrl+A) and type three spaces. Screenshot. 4. Select all and type "Keep"; click the Description box and type "Draft"; tick "Amend previous commit". Screenshot. | After step 2: the counter says "-3" in the warning colour; the button "Commit 3 files" is enabled. After step 3: the counter says "69"; the button is disabled. After step 4: the summary is still "Keep" and the description "Draft" (the boxes were not empty, so ticking left them); the button says "Amend previous commit" and is enabled. | | n/a | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say
"n/a". The real-window pass compares these screenshots with the scripted ones (D33, D58): check
1 steps 1 and 2, check 3 steps 2 and 3, check 5, check 10 steps 2 and 3, check 14 step 4, check
17 steps 2, 4 and 5, check 20 step 2 and check 21. It uses the real mouse and keyboard, finds
buttons, lists and file rows through UI Automation (a row's buttons inside that row, after the
real mouse has moved over it; a hunk's buttons by their order), clicks the working-changes row
by position, and restarts the real process in check 21. Checks 3, 10, 11, 19 and 21 run at
1400×900 in both passes, the largest standard size that fits the development screen (as phase
1's check 16), because side by side needs the width.

## Changes to expected results

The expected results above were first committed in `6c4031d`. Before any UI was built, an
independent review of them (Opus 5.5, with no context but the repo) recomputed every git result
with plain git and every colour with TextMateSharp, and found errors and gaps; they were
corrected in the next commit to this file, still before the UI:

- `return` is a control keyword, `#C586C0` dark and `#AF00DB` light: the first value was taken
  from the last matching theme rule instead of the first. Operators and bold headings were added
  to the table, and how rules are resolved was stated.
- The word-level rule was worded as sets of words, which contradicted its own examples; it now
  says what the examples assume (a longest common sequence, matched early in the added line),
  and check 4 names the renamed line's highlights.
- Side by side at 1100×700 leaves each half about 27 characters, too few for the lines checks 3,
  4 and 21 expect to see whole: those checks now run at 1400×900 (3, 21, and 10, 11, 19, which
  start as check 3) or 1920×1080 (4). Lines are not wrapped; the gutter's sizes, the row height
  and the hunk buttons' layout were stated.
- Restore closes the restore bar (D73, which makes D62 exact); the diff is read again after every
  write of the app, restore and commit included.
- A row's own button opens no diff; unstaging a rename unstages both paths; selecting after a
  commit that leaves the tree clean selects the new commit; Esc's reach, the commit area's check
  box row (20), the disabled button, the commit error's room and the lines between the panel's
  parts were stated.
- Automation ids for the lists, rows, row buttons and texts the checks assert were added.
- Checks were added or extended where a rule had no check: unstaging by file (22) and by line
  (11), a very large diff side by side (7), the working-changes row when HEAD's commit is not
  the first row (23), the first commit of a new repository (24), the summary counter and the
  button's rules (25), Esc and the close button of the restore bar (17, 18), and a change made
  outside the app while its diff is open (20).

Phase 1's checks run on clean repositories, so the working-changes row (D60) does not appear in
their pictures. Phase 1's check 18 commits through `TempRepo`, which writes the file before it
commits it: with the working tree watched (D71) a working-changes row can show for a moment in
between. Its test also waits until no working-changes row shows before each screenshot; its
expected result is unchanged.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven.

## Other platforms

The CI run of the gate's commit and its result in each job (the three platforms and the two
bare Ubuntu containers), and which of the screenshots CI took on macOS and Linux were opened.

## macOS checklist for the owner

1. Open a repository with changes: the working-changes row shows at the top of the graph; click
   it and the stage panel lists the unstaged and staged files.
2. Click a changed source file: its diff replaces the graph, with syntax colours in the JetBrains
   Mono font; switch to "Side by side" and back; press Esc.
3. Stage a hunk with "Stage hunk", and a single line by clicking its number and "Stage lines";
   check with `git diff --cached` in Terminal.
4. Type a summary with the Mac keyboard (including an accented letter, such as "é"), commit,
   then tick "Amend previous commit", change the summary and amend.
5. Discard a file (the dialog asks first), then click "Restore": the file comes back.
6. Edit a file in another editor while the app runs: the working-changes row and the lists
   follow within a few seconds.
7. Open an image's diff: both images show, with their sizes.
