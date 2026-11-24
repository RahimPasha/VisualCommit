# Troubleshooting: large repositories

VisualCommit is tested with a repository of **100,000 commits**. Opened in a tab, its first rows
show in about 2 seconds, and the rest of the history loads while you work.

## What was measured

The numbers below were measured before the release, on the developer's Windows laptop, with the
release build and a repository of 100,000 commits. They are indications: your computer, your disk,
your virus scanner and your Git will give other numbers.

| What | Measured |
|---|---|
| The tab's first rows, from opening the repository | about 2 s |
| VisualCommit started with one such tab to restore, from the start to its first rows | about 5 s |
| VisualCommit started with ten such tabs to restore, from the start to the active tab's first rows | about 5 to 7 s |
| Memory after loading such a repository | about 290 MB |
| Memory at the highest, while the graph reloads | about 350 MB |
| The graph reloaded after a commit made outside VisualCommit | about 3 s |
| The graph reloaded after a fetch from a remote on the same computer, from the click on Fetch | about 6 s |

A fetch over the network adds the time the download takes. While the graph reloads, the window
keeps answering and the old graph stays in view.

With several tabs to restore, the tab in front, when it holds a repository, opens first, and the
others once its first rows show, or after 5 seconds at the latest, at most four at a time.

## Make git faster

VisualCommit never writes to your repository to make itself faster: it does not run git's
maintenance for you. The git commands it runs at your request, such as a fetch or a commit, still do
the automatic upkeep git does after them in a terminal; the background fetch skips even that. Two of
git's own commands help a large repository, in VisualCommit and in a terminal alike. Run them in the
repository's folder:

- `git commit-graph write --reachable` writes git's commit-graph file, which makes walking the
  history, and so loading the graph, faster.
- `git maintenance start` lets git keep the repository in shape in the background from then on, the
  commit-graph file included.

## Other things that help

- Many restored tabs of large repositories still take longer in all to open, though the tab in
  front comes first: close the tabs you do not need.
- **Hide** or **Solo** branches you are not working on ([Read the graph](../graph.md)) to draw less
  of the history.
