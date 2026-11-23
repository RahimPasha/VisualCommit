# Search

Press **Search** in the toolbar, or Ctrl+F (Cmd+F on macOS), to open the search bar above the
graph. Type what you look for: VisualCommit searches as you type and marks the commits that match.
The other rows, except **Working changes**, are dimmed, and the bar counts the matches.

<img src="pictures/search-bar-light.png" srcset="pictures/search-bar-light.png 2x" alt="The search bar above the graph in the mode Commits with readme typed and 2 matches, its previous, next and close buttons; in the graph the two matching commits, Add a French readme and Update the readme, are in bold and marked at the left, and the other rows, except Working changes, are dimmed.">
<img src="pictures/search-bar-dark.png" srcset="pictures/search-bar-dark.png 2x" alt="The search bar above the graph in the mode Commits with readme typed and 2 matches, its previous, next and close buttons; in the graph the two matching commits, Add a French readme and Update the readme, are in bold and marked at the left, and the other rows, except Working changes, are dimmed.">

## What it searches

Choose the mode with the button at the bar's left, which shows the mode in use and opens a menu
of the three modes:

- **Commits**: the commit's whole message, its author, or the start of its SHA (at least 4
  characters).
- **Files**: the commits whose own change touches a file whose path contains the text.
- **Changes**: the commits whose own change adds or removes the text, to find when a piece of code
  came or went.

Case does not matter for the letters A to Z.

## Go from match to match

- **Next match**: Enter in the box, the down arrow button, or F3 (Cmd+G on macOS).
- **Previous match**: Shift+Enter in the box, the up arrow button, or Shift+F3 (Cmd+Shift+G on
  macOS).

The graph selects each match in turn and scrolls to it.

## Close the search

Press Esc while the focus is in the search bar, or press the bar's **×**. The graph shows every
row as before.
