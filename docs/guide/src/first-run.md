# The first start: finding Git

VisualCommit works through the Git installed on your computer and needs **Git 2.34.1 or newer**.
At every start it looks for Git: first the Git chosen in Settings, if any, then on the PATH, then
in the folders Git is usually installed in. [How VisualCommit finds Git](troubleshooting/git.md)
gives the details.

When it finds a Git that works and is new enough, VisualCommit opens as usual and the first-run
screen never shows. When it cannot use Git, a tab with no repository open shows the first-run
screen in place of the welcome page. The screen says what is wrong, what to do, and offers buttons
for it. Once Git is in place, press **Check again**: VisualCommit does not need to restart, and the
tabs whose repositories could not open without Git open again.

## The status bar's Git box

The status bar at the bottom of the window names the Git in use, such as `Git 2.43.0`. Point at it
to see where that Git is and how it was found, for example `found on the PATH` or
`chosen in Settings`. While VisualCommit looks, the box reads `Looking for Git...`.

<img src="pictures/status-bar-git-light.png" srcset="pictures/status-bar-git-light.png 2x" alt="The status bar: the current branch main and the status Ready on the left, Git 2.43.0 and the Activity log button on the right.">
<img src="pictures/status-bar-git-dark.png" srcset="pictures/status-bar-git-dark.png 2x" alt="The status bar: the current branch main and the status Ready on the left, Git 2.43.0 and the Activity log button on the right.">

When Git cannot be used, the box says why (`Git not found`, `Chosen Git not found`,
`Git 2.29.0 is too old (needs 2.34.1 or newer)` or `Git is not working`), and a **Set up Git…**
button beside it brings the first-run screen back.

## When Git is missing

The screen's title reads **VisualCommit needs Git**.

On **Windows** it offers:

- **Install with winget…**, when Windows' package manager winget is on this computer. VisualCommit
  first asks **Install Git with winget?** and shows the command it runs,
  `winget install --id Git.Git -e --source winget`. **Install** opens a terminal window that runs
  it. Windows may ask for permission. When it has finished, close the window and press
  **Check again**.
- **Download Git for Windows**, which opens Git's download page in your browser.
- **Choose git.exe…**, to pick a Git program yourself (see below).
- **Check again**.

<img src="pictures/first-run-missing-light.png" srcset="pictures/first-run-missing-light.png 2x" alt="The first-run screen on Windows titled VisualCommit needs Git, saying that no Git was found on the PATH or in Git for Windows' usual install folders, with the buttons Install with winget…, Download Git for Windows, Choose git.exe… and Check again, and the link How VisualCommit finds Git. The status bar reads Set up Git… and Git not found.">
<img src="pictures/first-run-missing-dark.png" srcset="pictures/first-run-missing-dark.png 2x" alt="The first-run screen on Windows titled VisualCommit needs Git, saying that no Git was found on the PATH or in Git for Windows' usual install folders, with the buttons Install with winget…, Download Git for Windows, Choose git.exe… and Check again, and the link How VisualCommit finds Git. The status bar reads Set up Git… and Git not found.">

<img src="pictures/first-run-winget-confirm-light.png" srcset="pictures/first-run-winget-confirm-light.png 2x" alt="The question Install Git with winget?, which says that VisualCommit opens a terminal window that runs winget install --id Git.Git -e --source winget, that winget downloads Git for Windows from its community repository and runs the installer, and to press Check again when it has finished; with the buttons Cancel and Install.">
<img src="pictures/first-run-winget-confirm-dark.png" srcset="pictures/first-run-winget-confirm-dark.png 2x" alt="The question Install Git with winget?, which says that VisualCommit opens a terminal window that runs winget install --id Git.Git -e --source winget, that winget downloads Git for Windows from its community repository and runs the installer, and to press Check again when it has finished; with the buttons Cancel and Install.">

On **macOS**, Git comes with Apple's Command Line Tools. When they are not installed, the screen
offers **Install the Command Line Tools**, which asks macOS to install them (macOS asks you to
confirm), or Homebrew's command `brew install git` with a **Copy** button, to run in Terminal.
Then press **Check again**. The link **Other ways to install Git** opens Git's page for macOS.

<img src="pictures/first-run-missing-macos-light.png" srcset="pictures/first-run-missing-macos-light.png 2x" alt="The first-run screen on macOS titled VisualCommit needs Git, saying that Apple's Command Line Tools, which include Git, are not installed, with the command brew install git and its Copy button, the buttons Install the Command Line Tools, Choose the git program… and Check again, and the links Other ways to install Git and How VisualCommit finds Git.">
<img src="pictures/first-run-missing-macos-dark.png" srcset="pictures/first-run-missing-macos-dark.png 2x" alt="The first-run screen on macOS titled VisualCommit needs Git, saying that Apple's Command Line Tools, which include Git, are not installed, with the command brew install git and its Copy button, the buttons Install the Command Line Tools, Choose the git program… and Check again, and the links Other ways to install Git and How VisualCommit finds Git.">

On **Linux**, the screen names your distribution and its command, with a **Copy** button, to run
in a terminal:

| Distribution | Command |
|---|---|
| Debian, Ubuntu and those based on them | `sudo apt install git` |
| Fedora, Red Hat Enterprise Linux, CentOS | `sudo dnf install git` |
| Arch Linux | `sudo pacman -S git` |
| openSUSE, SUSE | `sudo zypper install git` |

For any other distribution it says: "Install the package named git with your distribution's
package manager, then press Check again." The link **Other ways to install Git** opens Git's page
for Linux.

<img src="pictures/first-run-missing-linux-light.png" srcset="pictures/first-run-missing-linux-light.png 2x" alt="The first-run screen on Ubuntu titled VisualCommit needs Git, asking to install Git with the distribution's package manager, with the command sudo apt install git and its Copy button, the buttons Choose the git program… and Check again, and the links Other ways to install Git and How VisualCommit finds Git.">
<img src="pictures/first-run-missing-linux-dark.png" srcset="pictures/first-run-missing-linux-dark.png 2x" alt="The first-run screen on Ubuntu titled VisualCommit needs Git, asking to install Git with the distribution's package manager, with the command sudo apt install git and its Copy button, the buttons Choose the git program… and Check again, and the links Other ways to install Git and How VisualCommit finds Git.">

## When Git is too old

The title names the version, such as **Git 2.29.0 is too old**, and the screen shows where that Git
is. Update it, then press **Check again**:

- On **Windows**, **Update with winget…** (where winget is found) runs the same winget command
  after the question **Update Git with winget?**: the installer updates the Git installed.
  **Download Git for Windows** opens the download page; its installer updates Git in place.
- On **macOS**, update the Command Line Tools in System Settings, General, Software Update, or
  install Git with Homebrew (`brew install git`).
- On **Linux**, install a newer Git from your distribution, or choose another Git. The link
  **Other ways to install Git** opens Git's page for Linux, which lists other sources.

When the Git that is too old is the one you chose in Settings, the screen offers
**Use the Git on the PATH** in place of winget, which removes your choice.

<img src="pictures/first-run-too-old-light.png" srcset="pictures/first-run-too-old-light.png 2x" alt="The first-run screen titled Git 2.29.0 is too old, saying that VisualCommit needs Git 2.34.1 or newer and found Git 2.29.0 at C:\Program Files\Git\cmd\git.exe, with the buttons Update with winget…, Download Git for Windows, Choose git.exe… and Check again. The status bar reads Git 2.29.0 is too old (needs 2.34.1 or newer).">
<img src="pictures/first-run-too-old-dark.png" srcset="pictures/first-run-too-old-dark.png 2x" alt="The first-run screen titled Git 2.29.0 is too old, saying that VisualCommit needs Git 2.34.1 or newer and found Git 2.29.0 at C:\Program Files\Git\cmd\git.exe, with the buttons Update with winget…, Download Git for Windows, Choose git.exe… and Check again. The status bar reads Git 2.29.0 is too old (needs 2.34.1 or newer).">

## When Git does not work

The title reads **Git is not working**: VisualCommit found Git, but it did not report its version.
Under **Git said:** the screen shows Git's own words. A common cause is a mistake in your global
git configuration file: then Git names the file and the line, the screen says which line to
correct, and **Show the file** shows the file in your file manager. Correct that line, or rename the
file, then press **Check again**. Otherwise repair or reinstall Git.

<img src="pictures/first-run-not-working-light.png" srcset="pictures/first-run-not-working-light.png 2x" alt="The first-run screen titled Git is not working, with Git's words fatal: bad config line 3 in file C:/Users/demo/.gitconfig under Git said:, the advice to correct line 3 of that file or rename it, and the buttons Download Git for Windows, Show the file, Choose git.exe… and Check again.">
<img src="pictures/first-run-not-working-dark.png" srcset="pictures/first-run-not-working-dark.png 2x" alt="The first-run screen titled Git is not working, with Git's words fatal: bad config line 3 in file C:/Users/demo/.gitconfig under Git said:, the advice to correct line 3 of that file or rename it, and the buttons Download Git for Windows, Show the file, Choose git.exe… and Check again.">

## When the chosen Git is gone

If you chose a Git program in Settings and there is no file there any more, the title reads
**The chosen Git is gone**. VisualCommit does not quietly use another Git. When it finds another
one, the screen names it and offers **Use the Git it found**, which removes your choice. Otherwise
choose another Git, install one, or press **Check again** once the chosen one is back.

## When part of VisualCommit is missing

Git works, but VisualCommit's helper for git's questions is missing; the title reads
**Part of VisualCommit is missing**. Without the helper, git and ssh cannot ask for a password, a
passphrase or a host key in VisualCommit, and a signed-in account cannot be used for git. Install
VisualCommit again to restore it. **Continue without it** hides the screen for this session; the
status bar's **Details…** brings it back.

## Choosing a Git program yourself

**Choose git.exe…** on Windows (**Choose the git program…** on macOS and Linux) opens a file
dialog. VisualCommit checks the program you pick before it uses it, and says why when it refuses
one, for example when it is too old. The Git you choose is saved as the **Git program** setting
([Settings](settings.md), Git) and comes first in every later search.

## Which Git VisualCommit is tested with

On Windows, VisualCommit is tested with Git for Windows. Another Git build for Windows is used too,
and VisualCommit's log then notes "is not Git for Windows; VisualCommit is tested with Git for
Windows."

## More help

The link **How VisualCommit finds Git** on the screen (**What the helper does** on the screen for
a missing helper) opens the section of [Troubleshooting: Git](troubleshooting/git.md) for the
state the screen shows.
