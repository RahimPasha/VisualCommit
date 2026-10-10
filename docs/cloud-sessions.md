# Working from a Claude Code cloud session

For the owner. A phase can be built from a cloud session (Claude Code on the web, claude.ai/code)
instead of from the Windows machine. This page says what to set up, what such a session can and
cannot do, and how the work comes back to Windows. The rules the session itself follows are in
[CLAUDE.md](../CLAUDE.md), under "Sessions away from the Windows machine".

The facts about cloud sessions below come from the Claude Code documentation as read on
2026-10-06 (code.claude.com/docs, the pages on Claude Code on the web and on cloud environments).
They can change; the documentation wins where it differs. **No cloud session has worked on this
repo yet.** What is proven is listed under "What is proven and what is not".

## What a cloud session is

- A fresh virtual machine with Ubuntu 24.04, running as root, with a clone of the repo from
  GitHub. It has `git`, the GitHub CLI (`gh`) and `curl`. It has **no .NET SDK**.
- No desktop: the app cannot be started there, and nothing of the Windows machine is there,
  including Claude's memory folder for this project and its model setting. The repo is all it
  knows.
- With the default network setting ("Trusted") it can reach nuget.org and Ubuntu's package
  servers. That is all the build needs: the set-up script installs the .NET SDK as an Ubuntu
  package. The server that Microsoft's own .NET installer downloads from
  (`builds.dotnet.microsoft.com`) is not on the default list, which is why the script does not
  rely on it.
- It reads `CLAUDE.md` like any session, so it follows the same workflow.
- When a session sits idle, its machine is paused and may later be replaced by a new one.
  Anything not pushed to GitHub is then gone. `CLAUDE.md` tells the session to commit and push
  before it ends any turn.

## What you set up

Connect GitHub in claude.ai/code and choose this repo. The repository was deleted and created
again on 2026-10-09 (D41): if you had chosen it in claude.ai/code before that day, choose it
again, and if the Claude GitHub app is installed for selected repositories only, add the
repository to it again. Two things are worth doing each time you start a session:

- **Choose Opus 5.5 as the session's model.** The model setting of the Windows machine does not
  travel with the repo. `CLAUDE.md` tells a session on another model to say so and ask you once.
- **Leave the network access on "Trusted".**

The session prepares its own machine: `CLAUDE.md` tells it to run `bash scripts/setup-linux.sh`,
which installs the .NET 10 SDK and the few system libraries the tests need. In CI's empty
container that takes about half a minute; how long it takes in a cloud session has not been
measured.

Optional, to save that time: a cloud environment can have a set-up script of its own. It runs
by itself when a session's machine is made, before Claude starts, and its result is kept for
roughly seven days as long as the script finishes within about five minutes. In the
environment's settings, as its set-up script, put:

```
curl -fsSL https://raw.githubusercontent.com/RahimPasha/VisualCommit/master/scripts/setup-linux.sh -o /tmp/setup-linux.sh && bash /tmp/setup-linux.sh || true
```

The `|| true` at the end matters: an environment script that fails keeps the session from
starting at all, and without it one failed download would lock you out. With it, a failed
set-up only means the session runs the script itself, as it would have anyway. The line reads
the script from `master`, so a correction made on a phase branch reaches it when that phase is
merged.

Two environment variables are worth adding there, both optional:
`DOTNET_CLI_TELEMETRY_OPTOUT=1` and `AVALONIA_TELEMETRY_OPTOUT=1` stop the .NET tools and
Avalonia's build step from sending usage data.

If the set-up ever fails on a download, the fallback is to set the environment's network access
to "Custom", keep the default list of package managers ticked, and add
`builds.dotnet.microsoft.com`. The session's own message will name the server it could not
reach.

## How to use it

Start a session on the repo and say what you would say on Windows: "Start phase 1", or
"Continue" for a phase that is under way.

| It can | It cannot |
|---|---|
| Build, and run all default tests, including the scripted visual walk-through | Run the real-window pass: that drives the real app on a Windows desktop with the real mouse |
| Inspect the walk-through's screenshots (drawn on Linux) | Start the app to look at it |
| Push its commits and read CI, including CI's Windows and macOS jobs | Set a phase to "Awaiting acceptance" |

So a cloud session ends a phase as **"Blocked: the real-window pass needs a session on the
Windows machine"**, with everything that does not need Windows done and pushed: the code, the
scripted pass, the updates to the architecture and the plan, and a first version of the handoff.

To finish the phase, open a session on the Windows machine and say "Continue". Expect a working
session, not a formality: the real-window test code has only been compiled until then. That
session runs both passes of the gate on one commit, fixes what the real window shows, inspects
the pictures of both, completes the report and the handoff, runs the cold-read check and sets
the phase to "Awaiting acceptance". Then you accept it as usual, by saying "Phase N accepted".

Two things to leave alone:

- **The "Create PR" button, for a phase branch.** A merge made on GitHub skips what "Phase N
  accepted" does, and a squash merge leaves the branch looking unmerged to every later session.
- **The session's git identity.** `CLAUDE.md` makes every session check, before its first
  commit, that commits carry `Rahim <rpkhajei@gmail.com>` (D40). If the cloud platform sets
  another identity that the session cannot change, it stops and tells you before committing.

A cloud session can also be moved to the Windows machine while it is running, with
`claude --teleport <session id>`; the branch has to be pushed and the local working tree clean.

## What is proven and what is not

Proven, by CI on every push that changes code (the latest run is in the CI section of
[status.md](status.md)):

- The code builds and all default tests pass on Linux: the `ubuntu-latest` job.
- `scripts/setup-linux.sh` is enough on an Ubuntu 24.04 machine that has nothing, running as
  root: the two "bare ubuntu container" jobs run it in an empty container and then build and
  test there. One takes the SDK from Ubuntu's package servers, as a cloud session would; the
  other takes it from Microsoft's installer.

Not proven by that: a CI runner's network is open, so CI cannot show that the downloads get
through a cloud session's allowlist. That rests on the documentation's list of allowed servers.

Not tried yet; the first cloud session checks these and corrects this page, and the script if
needed, on the branch it is working on:

- That the machine really is as described: no SDK, `gh` present and able to read this repo's CI
  runs, nuget.org and Ubuntu's package servers reachable, and the set-up script getting the SDK
  from them.
- That it can fetch every branch and the full history, as step 1 of "Start of every session"
  asks.
- That it may push a branch named `phase/...`, and `master`. The documentation says a cloud
  session's pushes are not limited to one branch. `CLAUDE.md` says what to do if a push is
  refused.
- Which author name and e-mail git uses for commits there, and whether the session can set
  them.
- That `gh run download` can fetch the screenshots a CI run took. GitHub serves them from
  `*.blob.core.windows.net`, which is not on the default list of allowed servers, so expect it
  to be refused. The session then says so in the report, and the Windows session looks at CI's
  macOS and Linux pictures. To let a cloud session do it, set the network access to "Custom",
  keep the default list and add `*.blob.core.windows.net`.
- How long the set-up takes there, and whether the optional environment script above works as
  written.
