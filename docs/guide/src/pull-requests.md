# Pull requests

When you are signed in to the service a repository's remote is on (GitHub, GitLab or Azure DevOps;
see [Sign in to GitHub, GitLab or Azure DevOps](accounts.md)), the left panel's
**Pull requests** section lists the repository's open pull requests. On Azure DevOps the remote
must be in the organization you signed in to: for a repository of another organization, the section
names both organizations and lists nothing; to see its pull requests, sign out and sign in to that
organization in Settings, **Accounts**. On GitLab they are merge requests, and the section and its
items say so; GitHub numbers them `#12`, GitLab and Azure DevOps `!12`.

<img src="pictures/pull-requests-light.png" srcset="pictures/pull-requests-light.png 2x" width="700" alt="The left panel's Pull requests section with one open pull request, #12 Predict rain fro… by demo--alex, and in the graph the branch feature/forecast's label carrying the badge #12.">
<img src="pictures/pull-requests-dark.png" srcset="pictures/pull-requests-dark.png 2x" width="700" alt="The left panel's Pull requests section with one open pull request, #12 Predict rain fro… by demo--alex, and in the graph the branch feature/forecast's label carrying the badge #12.">

Each row shows the request's number, title and author; point at it for its branches. A branch
whose pull request is open carries the request's number on its label in the graph.

Signed out, the section offers **Sign in to GitHub to see pull requests…** (or the service's
name).

## Open, check out, copy

Right-click a pull request:

- **Open in browser** opens it on the service's website.
- **Check out** and its branch checks out the request's branch, creating a local branch for it
  when there is none. When the repository already has that branch from one of its remotes,
  VisualCommit checks it out as it is, without fetching it; otherwise it fetches it first. On
  Azure DevOps the request's branch must be a branch of one of the repository's remotes. When a
  request cannot be checked out, the item is dimmed; point at it to see why.
- **Copy link** copies its address.

The section's own menu has **Refresh pull requests** and **Open pull requests in browser**.

## Create a pull request

Right-click a branch and choose **Create pull request…** (on GitLab, **Create merge request…**).
You can also drag a branch onto a remote branch on the service, or onto a local branch that tracks
the branch of the same name there, and choose it in the menu that opens, which sets both branches.
The dialog **Create pull request** has:

- **From**, the branch with your changes, and **Into**, the branch to merge into;
- a **Title** and a **Description**;
- **Draft**, to open it as a draft.

Press **Create**. When the branch, or some of its commits, are not on the service yet, the dialog
says so and **Create** pushes them first. Once the request exists, the branch's menu reads
**Open pull request** and its number, such as `Open pull request #12`, in place of
**Create pull request…**.
