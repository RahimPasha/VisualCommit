# Troubleshooting: Git

VisualCommit works through the Git installed on your computer and needs Git 2.34.1 or newer. When
it cannot use Git, it shows the first-run screen, whose link **How VisualCommit finds Git** (or
**What the helper does**) opens the section of this page for what the screen shows.
[The first start](../first-run.md) shows each screen.

## How VisualCommit finds Git

At every start, and each time you press **Check again**, VisualCommit looks for Git in this order:

1. **The Git chosen in Settings**, Git, **Git program**, if you chose one. VisualCommit uses it
   whatever it answers, and does not quietly fall back to another Git: if it is gone or does not
   work, the screen says so.
2. **The PATH**: the first `git` found in the folders of the PATH VisualCommit was started with
   (`git.exe` on Windows).
3. **The usual install folders**: on Windows, Git for Windows' folders, such as
   `C:\Program Files\Git\cmd\git.exe`, under Program Files, Program Files (x86) and your own
   programs folder; on macOS and Linux, `/usr/bin/git`, `/usr/local/bin/git`,
   `/opt/homebrew/bin/git` and `/opt/local/bin/git`. An app started from the macOS Finder or a Linux
   desktop launcher can get a shorter PATH than a terminal, which these folders make up for.

On macOS, `/usr/bin/git` is used only while Apple's Command Line Tools are installed; without them
it is only a stand-in that offers to install them.

VisualCommit runs a Git program only by its full path, and on Windows only an `.exe`. The status
bar's Git box names the Git in use; point at it to see its path and how it was found.

## Git is missing { #git-missing }

The screen **VisualCommit needs Git** means that no Git was found in any of the places above.

- **Windows**: install Git for Windows, with **Install with winget…** (where winget is on this
  computer) or **Download Git for Windows**, then press **Check again**. VisualCommit does not need
  to restart.
- **macOS**: install the Command Line Tools (**Install the Command Line Tools**), or Git with
  Homebrew, `brew install git`, then press **Check again**.
- **Linux**: install the package `git` with your distribution's package manager, such as
  `sudo apt install git` on Debian and Ubuntu or `sudo dnf install git` on Fedora, then press
  **Check again**.

If Git is installed somewhere else, press **Choose git.exe…** (**Choose the git program…** on macOS
and Linux) and pick it.

<img src="../pictures/first-run-missing-light.png" srcset="../pictures/first-run-missing-light.png 2x" alt="The first-run screen on Windows titled VisualCommit needs Git, with the buttons Install with winget…, Download Git for Windows, Choose git.exe… and Check again, and the link How VisualCommit finds Git.">
<img src="../pictures/first-run-missing-dark.png" srcset="../pictures/first-run-missing-dark.png 2x" alt="The first-run screen on Windows titled VisualCommit needs Git, with the buttons Install with winget…, Download Git for Windows, Choose git.exe… and Check again, and the link How VisualCommit finds Git.">

## The chosen Git is gone { #git-chosen-missing }

The screen **The chosen Git is gone** means that Settings names a Git program and there is no file
at that path now, or that the setting is not a full path to the program itself (on Windows, an
`.exe`). VisualCommit does not use another Git in its place without asking you.

- When it found another Git, the screen names it: **Use the Git it found** removes your choice and
  uses that one.
- Otherwise, choose another Git, install one, or put the chosen one back and press **Check again**.

You can also change or remove the choice in Settings, Git, **Git program**.

## Git is too old { #git-too-old }

The screen's title names the version, such as **Git 2.29.0 is too old**: VisualCommit needs Git
2.34.1 or newer. It shows where that Git is.

- **Windows**: update Git for Windows, with **Update with winget…** or
  **Download Git for Windows**; its installer updates it in place.
- **macOS**: update the Command Line Tools in System Settings, General, Software Update, or install
  Git with Homebrew.
- **Linux**: install a newer Git from your distribution. If your distribution has none new enough,
  **Other ways to install Git** opens Git's page for Linux, which lists other sources.

Or choose a newer Git that is already on this computer. Then press **Check again**.

<img src="../pictures/first-run-too-old-light.png" srcset="../pictures/first-run-too-old-light.png 2x" alt="The first-run screen titled Git 2.29.0 is too old, saying that VisualCommit needs Git 2.34.1 or newer and found Git 2.29.0 at C:\Program Files\Git\cmd\git.exe, with the buttons Update with winget…, Download Git for Windows, Choose git.exe… and Check again.">
<img src="../pictures/first-run-too-old-dark.png" srcset="../pictures/first-run-too-old-dark.png 2x" alt="The first-run screen titled Git 2.29.0 is too old, saying that VisualCommit needs Git 2.34.1 or newer and found Git 2.29.0 at C:\Program Files\Git\cmd\git.exe, with the buttons Update with winget…, Download Git for Windows, Choose git.exe… and Check again.">

## Git does not work { #git-not-working }

The screen **Git is not working** means that VisualCommit found Git, but it did not report its
version, said nothing, or could not be started. Under **Git said:** the screen shows Git's own
words.

**A broken global git configuration.** The most common cause is a mistake in your global git
configuration file, such as `~/.gitconfig`. Git then names the file and the line, as in
`fatal: bad config line 3 in file C:/Users/demo/.gitconfig`, and the screen says
"Git cannot read line 3 of" the file: correct that line, or rename the file. **Show the file**
shows it in your file manager. Then press **Check again**.

<img src="../pictures/first-run-not-working-light.png" srcset="../pictures/first-run-not-working-light.png 2x" alt="The first-run screen titled Git is not working, with Git's words fatal: bad config line 3 in file C:/Users/demo/.gitconfig under Git said:, the advice to correct line 3 of that file or rename it, and the buttons Download Git for Windows, Show the file, Choose git.exe… and Check again.">
<img src="../pictures/first-run-not-working-dark.png" srcset="../pictures/first-run-not-working-dark.png 2x" alt="The first-run screen titled Git is not working, with Git's words fatal: bad config line 3 in file C:/Users/demo/.gitconfig under Git said:, the advice to correct line 3 of that file or rename it, and the buttons Download Git for Windows, Show the file, Choose git.exe… and Check again.">

**Otherwise**, repair or reinstall Git: on Windows Git for Windows, on Linux the package `git`. On
macOS, Apple's Command Line Tools can be damaged or half installed; the screen then offers to
install them again and shows the command that selects them,
`sudo xcode-select --switch /Library/Developer/CommandLineTools`, to run in Terminal.

## The sign-in helper is missing { #helper-missing }

The screen **Part of VisualCommit is missing** means that Git works, but VisualCommit's helper for
git's questions, the program `visualcommit-askpass` (`visualcommit-askpass.exe` on Windows) that is
installed beside VisualCommit, is not where it should be; the screen shows where it was looked for. Git and ssh start that helper when
they need a password, a passphrase or an answer about a host key, and when they use a signed-in
account's token. Without it, they cannot ask in VisualCommit, and a signed-in account cannot be used
for git.

Install VisualCommit again to restore the helper, then press **Check again**.
**Continue without it** hides the screen for this session; the status bar's **Details…** brings it
back.

## Which Git is tested

On Windows, VisualCommit is tested with Git for Windows. Another Git build for Windows is used too,
with a note in VisualCommit's log. See [Data and logs](data-and-logs.md) for where the log is.
