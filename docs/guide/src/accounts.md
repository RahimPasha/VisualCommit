# Sign in to GitHub, GitLab or Azure DevOps

VisualCommit works without any account. Signing in to GitHub (github.com), GitLab (gitlab.com) or
Azure DevOps (dev.azure.com) adds that service's features: VisualCommit lists and creates pull
requests (merge requests on GitLab), clones your repositories from a list, and uses the account for
git over HTTPS. For a repository on GitHub, it also gets the pictures of the commits' authors from
GitHub, shows them in the graph and the commit details, and keeps them in its data folder. You can
be signed in to one account of each service. On Azure DevOps the account is one organization's:
pull requests, the repositories to clone and git over HTTPS cover that organization's repositories
only.

Open Settings, Ctrl+, (Cmd+, on macOS), and choose **Accounts**.

<img src="pictures/accounts-section-light.png" srcset="pictures/accounts-section-light.png 2x" width="868" alt="The settings screen's Accounts section: GitHub signed in as demo--alex, The token is kept in this computer's keychain, with Sign out…; under Git the ticked check box Use for git over HTTPS and its note; GitLab and Azure DevOps each Not signed in, with Sign in….">
<img src="pictures/accounts-section-dark.png" srcset="pictures/accounts-section-dark.png 2x" width="868" alt="The settings screen's Accounts section: GitHub signed in as demo--alex, The token is kept in this computer's keychain, with Sign out…; under Git the ticked check box Use for git over HTTPS and its note; GitLab and Azure DevOps each Not signed in, with Sign in….">

## Sign in with a token

VisualCommit signs in with a personal access token, which you make on the service's website and
paste into VisualCommit. Press **Sign in…** beside the service.

- **GitHub**: the dialog **Add GitHub account**. "A fine-grained token needs read and write access
  to Contents and Pull requests of your repositories; a classic token needs the repo scope. To push
  changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic)."
  **Create a token on GitHub** opens GitHub's page for a new token in your browser.
- **GitLab**: the dialog **Add GitLab account**. "A fine-grained token needs User: Read and Project:
  Read among its user permissions, and Project: Read, Merge Request: Read and Create, and Code:
  Download and Push for the groups and projects you work in; a legacy token needs the api scope."
  **Create a fine-grained token on GitLab** and **Or create a legacy token, filled in** open
  GitLab's forms.
- **Azure DevOps**: the dialog **Add Azure DevOps account**. Type your **Organization**, its name or
  its address on dev.azure.com or visualstudio.com, then paste a token made for that organization
  with the scope Code (Read & write). **Create a token on Azure DevOps** opens the organization's
  page for new tokens.

<img src="pictures/sign-in-dialog-light.png" srcset="pictures/sign-in-dialog-light.png 2x" width="408" alt="The dialog Add GitHub account: the help on which token to paste, the link Create a token on GitHub, an empty Token box, and the buttons Cancel and a dimmed Sign in.">
<img src="pictures/sign-in-dialog-dark.png" srcset="pictures/sign-in-dialog-dark.png 2x" width="408" alt="The dialog Add GitHub account: the help on which token to paste, the link Create a token on GitHub, an empty Token box, and the buttons Cancel and a dimmed Sign in.">

Paste the token into **Token** and press **Sign in**. VisualCommit checks the token with the
service before it keeps it, and says what the service answered when it refuses it. Signing in,
like signing out, takes effect at once: the settings screen's **Cancel** does not undo it.

## Where the token is kept

The token is kept in your system's keychain: Windows' Credential Manager, the macOS keychain, or
the Secret Service on Linux. The section says "The token is kept in this computer's keychain."
VisualCommit never writes a token to a file.

Where no keychain can be reached, as on some minimal Linux desktops, the token is kept for this
session only: "No keychain was found on this computer. The token is not saved: VisualCommit keeps
it until it closes and asks for it again at the next start." When saving it in the keychain fails,
the token is also kept for this session only, and the section says why, beginning
"The token could not be saved in this computer's keychain:".

## Use the account for git

**Use for git over HTTPS**, under the account: "Clone, fetch, pull and push to GitHub over HTTPS
use this account's token in place of your git credential helpers. Git in a terminal still uses your
helpers." Untick it and press **Save** to let git ask your own credential helpers again: the box
takes effect only when you press Save, and Cancel leaves it as it was.

### A note about .netrc

When your home folder has a `.netrc` or `_netrc` file, an account is used for git, and your Git is
older than 2.46, the section shows this note: "Your home folder has a .netrc or _netrc file. With
Git 2.43.0, an entry in that file for a host can answer git's requests to it before your signed-in
account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every
version of Git." (It names your Git's version.) VisualCommit never reads the file's content.
**Dismiss** hides the note for the session. To be sure the account answers, update Git, or remove
the host's entry from that file.

## Clone from your account

Once you are signed in, the welcome page's clone form has **From GitHub…** (or the service's name;
**From an account…** when you are signed in to more than one). The command palette has
**Clone from GitHub…**, **Clone from GitLab…** and **Clone from Azure DevOps…**, which ask you to
sign in first when you are not. Each lists your account's repositories: type in **Filter** to
narrow the list, select one and press **Choose**, or press Enter. The clone form then fills in its
URL. See [Open, clone or create a repository](open-clone-create.md).

## Sign out

**Sign out…** asks first, such as **Sign out of GitHub?**, then removes the token from this
computer and, for GitHub, the authors' pictures VisualCommit saved, at once, whether you then press
Save or Cancel. VisualCommit no longer lists
pull requests, and git over HTTPS asks your own credential helpers again.

## Pull requests

See [Pull requests](pull-requests.md). When a service refuses an expired token, its section asks
you to sign in again with a new token.
