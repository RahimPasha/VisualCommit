# Install VisualCommit

> [!NOTE]
> The installers come with the public beta. Until then, VisualCommit is built from its source
> code. This page will then say how to install it on each system.

VisualCommit needs Git 2.34.1 or newer on every system. [The first start](first-run.md) says how
VisualCommit finds Git and what to do when it finds none.

## Windows

The installers come with the public beta. Until then, build from source (below).

## macOS

The installers come with the public beta. Until then, build from source (below).

## Linux

The installers come with the public beta. Until then, build from source (below).

## Build from source

You need the .NET 10 SDK and Git 2.34.1 or newer. In a terminal:

```text
git clone https://github.com/RahimPasha/VisualCommit.git
cd VisualCommit
dotnet run --project src/VisualCommit.App
```

The [repository's README](https://github.com/RahimPasha/VisualCommit/blob/master/README.md) has
more on building and testing.

## What an uninstall leaves

Removing VisualCommit's program files does not remove what it saved elsewhere:

- **Its data folder**, with its settings, the tabs it restores, your workspaces and its logs. Where
  that folder is on each system is in [Data and logs](troubleshooting/data-and-logs.md). Delete
  the folder to remove them.
- **Tokens in the keychain.** A hosting account you signed in to keeps its token in your system's
  keychain. Sign out in Settings, Accounts before you uninstall, and VisualCommit removes it; see
  [Sign in to GitHub, GitLab or Azure DevOps](accounts.md).
- **Git's own settings.** The name, email, pull mode and merge tool you save in Settings, Git are
  written to git's own configuration: for all repositories to your global git configuration, for
  one repository to its `.git/config`. Git uses them, in a terminal too, and they stay. See
  [Settings](settings.md).
