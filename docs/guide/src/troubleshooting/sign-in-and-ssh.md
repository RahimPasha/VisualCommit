# Troubleshooting: sign-in and SSH

## When git asks for something

A fetch, pull, push or clone may need a password, a passphrase or your answer about a host. Git
and ssh then ask through VisualCommit, in one of these dialogs:

- **Sign in to** and the host: "Git needs a user name and a password or token for" the address.
  Type them and press **Sign in**. Git asks your credential helpers first, such as Git Credential
  Manager: one may answer without this dialog or show its own sign-in window, and a helper may
  remember what you type here.
- **Key passphrase**: "Enter the passphrase for" your SSH key, then **Unlock**.
- **Unknown host**: ssh shows a host it has not seen before, with its key's fingerprint, and asks
  "Trust this host and connect?" **Trust and connect** adds the host to ssh's list of known hosts.
  Check the fingerprint with the service before you trust it.
- **Git asks**: any other question, answered in one box.

**Cancel** in any of them stops the operation. A fetch that VisualCommit runs in the background
never asks.

When a repository's remote is on GitHub, GitLab or Azure DevOps (on Azure DevOps, in the
organization you signed in to) and you signed in to that service in VisualCommit with
**Use for git over HTTPS** ticked, git over HTTPS uses the account's token and does not ask; see
[Sign in to GitHub, GitLab or Azure DevOps](../accounts.md).

If the first-run screen says that part of VisualCommit is missing, these dialogs cannot come: see
[The sign-in helper is missing](git.md#helper-missing).

## An old OpenSSH does not ask through VisualCommit

An OpenSSH older than 8.4 ignores VisualCommit's sign-in dialog. RHEL 8 ships 8.0, and the
OpenSSH built into Windows can be older where you chose it.

- On Linux and macOS it asks in the terminal VisualCommit was started from, if there is one: answer
  there, or cancel the operation in VisualCommit.
- On Windows no terminal shows its question, not even the one you started VisualCommit from:
  VisualCommit runs git without a console window. If the operation waits, cancel it in
  VisualCommit.

To avoid it, update OpenSSH, load your key into an SSH agent before you start VisualCommit, or use
the remote's HTTPS address. `ssh -V` in a terminal prints the version of the ssh that terminal
finds, which may not be the one git runs: on Windows, Git for Windows brings its own unless you
chose the one built into Windows.

## A .netrc file answers before your account

Git can read passwords from a `.netrc` or `_netrc` file in your home folder. Before Git
2.46, an entry in that file for a host answers git's requests to it before your signed-in account,
so git may use an old password instead of the account's token. Git 2.46 or later stops this for
git's own requests; git-lfs reads the file with every version of Git. When this can happen,
Settings, **Accounts** shows a note that says so. Update Git, or remove the host's entry from the
file.

## A token is refused

When a service refuses an account's token, because it expired or was revoked, Settings,
**Accounts** says so, such as "GitHub refused the token. Sign in again with a new token." Make a new
token on the service and sign in with it.
