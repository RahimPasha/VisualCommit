# Phase 8b: the owner's questions, with the research

plan.md's "Phase 8b" and the 8a handoff ("For the next phase", "Do first", step 4) ask each of
8b's questions before the design, "with the research's recommendation". That research lies in
`D:/Projects/Personal/phase8-work/research/` on the owner's Windows machine, outside the
repository. This page is what a session without that folder has: a cloud session, or a new clone.

- **When.** Research agents wrote its 17 files for the lead on 2026-11-18, the day before D399
  recorded the split into 8a and 8b, which the owner had chosen on 2026-11-09: the repo readers
  already write 8a and 8b, while the web briefs are written for "phase 8". Every price, version,
  limit and service term below is as of 2026-11-18 and must be read again before anything rests on
  it. The repo readers read `master` before 8a; where 8a changed one of their facts, this page says
  so. The page records no decision: each recommendation is the research's, with its source, and
  each answer is the owner's and becomes a decision.
- **The files.** Six web briefs, `web-<topic>.md`; three skeptics who checked them the same day,
  `web-verify-<topics>.md`; eight repo readers, `repo-<topic>.md`, among them `repo-critic.md`,
  whose section 5 merges the others' questions. Where a brief and its skeptic disagree, this page
  gives the skeptic's correction and says so.
- **Order.** Sections 1 to 11 are plan.md's list, in order; 12 to 14 are asks the 8a handoff names
  and plan.md's list lacks; 15 is what is already answered.

## 1. The installers per platform

plan.md: "the installers per platform (recommended: Velopack, MIT, with a per-user `Setup.exe` on
Windows, a `.pkg` and a `.zip` per architecture on macOS and a self-updating AppImage on Linux,
each self-contained per runtime, not single-file and not trimmed, the askpass helper
self-contained beside the app)".

Options weighed:
- **Velopack on all three systems.** MIT; stable 1.2.161 of 2026-09-29 (1.2.158 deprecated for
  "critical bugs"). One CLI, `vpk`, and one NuGet package: a per-user `Setup.exe` with no
  elevation; `.app`, `.pkg` and `.zip` on macOS, built only on a Mac; an AppImage alone on Linux.
- **Not recommended:** MSIX with an `.appinstaller` file; Inno Setup or WiX with NetSparkleUpdater
  (a fallback); Squirrel.Windows (seeking maintainers) and Clowd.Squirrel (archived for Velopack);
  Flathub (source builds only, rules on AI-written code, host git through a sandbox exit); a `.dmg`
  with Sparkle. Later: `.deb`/`.rpm` without updates, a Homebrew cask once notarised, the Store
  (section 12).
- **The publish.** Self-contained per runtime (Velopack installs .NET only on Windows, and .NET's
  installer needs administrator rights); not single-file; not trimmed (AvaloniaEdit and
  TextMateSharp are not known to be trim-safe, and `DialogViewLocator` finds views by name, which
  the trimmer would break); ReadyToRun only if a measure shows it worth its size.
- **The helper.** No code sets `DOTNET_ROOT`, so the framework-dependent helper does not start
  where no .NET 10 is installed: publish it self-contained for the same runtime into the same
  folder, or compile it ahead of time (`repo-ship.md`, option b).

Recommendation: Velopack, as plan.md gives it, its version pinned and each upgrade tested: one tool
for three systems, no administrator rights on Windows, GitHub Releases as its source (D400), and an
update check that sends no install id (the skeptic read Velopack's source; D401 rests on it).

What the skeptics corrected or added:
- .NET's security patches: the brief said a release "monthly at most"; the skeptic counted 12 to 15
  a year, twice in some months.
- The AppImage: Velopack ships the static type2 runtime, so no `libfuse2` (the brief left it open).
  Whether the host needs `fusermount` or fuse3 is open: a CI smoke test should install fuse3.
- A `.pkg` installed into `/Applications` makes the updater ask for an administrator's password, so
  a standard user cannot update: default to `~/Applications`, or test a standard user.
- Velopack's GitHub source reads only the 10 newest releases: more than ten betas after a stable
  release hide it from stable users. Keep betas few, or give the beta its own source.
- `web-channels.md` also proposed a `.dmg` and `.deb`/`.rpm`; its skeptic aligned it with this
  brief. Apple says a `.pkg` signed with an expired Developer ID Installer certificate must be
  signed again to run, while a signed app keeps running (the macOS skeptic), so `.pkg` against a
  `.dmg` or `.zip` as the main download is still to settle.

Not settled: whether two self-contained programs in one folder is a supported layout (a test on
three systems must prove the helper after an install and an update); the sizes (from memory, 100
to 130 MB installed, 40 to 55 MB to download on Windows); ReadyToRun's effect; whether signing
covers the helper under macOS's hardened runtime; whether the SDK refuses a self-contained publish
while `VisualCommit.Git` references the framework-dependent helper.

The owner provides: nothing to buy for Velopack. An app id that never changes (Velopack's pack id,
the macOS bundle id, the Linux desktop id): `repo-ship.md` (question 3) recommends
`io.github.rahimpasha.VisualCommit`, the stem of the Secret Service schema the app already writes.
For the signed macOS packages (D403): a Developer ID Application certificate, a Developer ID
Installer certificate if a `.pkg` ships, and notary credentials, as GitHub secrets the owner sets.
Sources: `web-installers.md`; `web-verify-installers-channels.md` I1 to I28;
`web-verify-winsign-macos.md` M20; `web-macos.md` Option A; `repo-ship.md`.

## 2. The macOS token store

plan.md: "the macOS token store (risk 34: the research would keep `/usr/bin/security` until a move
is tried on a real Mac)".

Each account's token is a login-keychain item made by `/usr/bin/security`, which trusts that tool,
so any process of the user reads it through `security` without a prompt (risk 34). Options
(`web-macos.md`):
- **Security.framework** in the file-based login keychain with the default access list: the item
  trusts the signed app, an update signed by the same team with the same identifier keeps access
  without a prompt, and other programs are prompted. A one-time migration moves the old items.
- **The data protection keychain**, the one Apple recommends (`SecItem`'s default on macOS is still
  the file-based keychain): an App ID, restricted entitlements and a provisioning profile checked at
  every launch; no throwaway keychain for CI. Left for later.
- **Keep `/usr/bin/security`**, with risk 34 an accepted limit, as on Windows and Linux.

The brief recommended the move through the `SecItem` calls once the app has a Developer ID, which
D403 gives. Its skeptic overrode it, and plan.md carries the skeptic's version. The brief said the
move works "as Git Credential Manager does", but Git Credential Manager writes with the legacy
`SecKeychain*` calls, so it proves the file-based keychain under a Developer ID, not the
`SecItemAdd` path; and with no Mac, nothing shows whether a user is prompted after an update. So:
keep the `security` store in 8b; copy Git Credential Manager's calls or test the `SecItem` path;
let the migration wait for a test on a real Mac; risk 34 closes only after it. Not settled: the
prompts after an update are inferred, never tested; Apple calls the file-based keychain "on the
road to deprecation" (TN3137), though nothing announced removes it.

The owner provides: if the store is to move, a real Mac for the test (the skeptic names a borrowed
one or a rented cloud Mac). Sources: `web-macos.md` (The token store, Recommendation);
`web-verify-winsign-macos.md` M12 to M15 and section 2's item 1.

## 3. The platforms, architectures and oldest systems

plan.md: "the platforms, architectures and oldest systems shipped (.NET 10 supports macOS 15 and
later and no consumer Windows 10)".

Recommendation: win-x64, osx-arm64, osx-x64 and linux-x64 for the beta; win-arm64 and linux-arm64
to the backlog, since no CI runner or machine at hand proves them (`repo-ship.md`, question 2);
`web-installers.md` would add linux-arm64 if CI's time allows. One macOS package per architecture,
no universal bundle: .NET merges only single-file or Native AOT executables, and this app ships two
executables on one runtime (`web-macos.md`). The oldest systems are .NET 10's (`repo-critic.md`,
item 31): macOS 15; Windows 11, as .NET 10 lists Windows 10 only in its Enterprise, IoT and LTSC
editions; Ubuntu 22.04, 24.04 and 26.04, Debian 13 (not 12), RHEL 8 to 10, Fedora 43 and 44 and
Leap 16.0 (`web-git.md` fact 14). `app.manifest` still declares Windows 10 and 11: change it, or
say it in the guide.

What the skeptics added: macOS 27 runs on Apple silicon alone (Apple, WWDC 2025), so Intel Macs
stop at macOS 26, with security updates to about 2028; Homebrew runs on Intel until September 2027;
GitHub offers `macos-15-intel` and `macos-26-intel` runners for the x64 package and ends x86_64
macOS runners in the fall of 2027. So the Intel package should get an end date. Sources:
`web-verify-winsign-macos.md` M9, M10, M16, M21; `web-verify-installers-channels.md` I20;
`web-verify-git-brand.md` G4.

## 4. The version scheme

plan.md: "the version scheme, stamped from a tag that the owner's identity makes (D40)".

Today `Directory.Build.props`' `Version` is 0.1.0 and the build stamps the commit, which About shows
as `0.1.0 (<short commit>)` (8a handoff); `app.manifest`'s identity is fixed at `1.0.0.0`. Options
(`repo-ship.md`, question 5): `1.0.0-beta.N` for the betas, then `1.0.0`; `0.9.x` for the beta;
keep `0.x`.

Recommendation: `1.0.0-beta.N`, then `1.0.0`, passed as `-p:Version=` from the git tag in the
release workflow, with `Directory.Build.props` keeping a development default and `app.manifest`'s
version decided with it. The owner's identity makes the tag and CI makes no commit: a workflow that
bumps versions, commits notes or tags as `github-actions` breaks D40 or blurs it (`repo-critic.md`,
item 33 and 6.2). Not settled: who shows as a release's author. `web-channels.md` read, in
secondary sources only, that a release made with the workflow's `GITHUB_TOKEN` shows the
`github-actions` bot, and recommended that the workflow attach the files and the owner create or
publish the release; its skeptic found no GitHub page that says either way. The owner provides:
the tags.

## 5. What proves "receive an update", and when the beta becomes 1.0

plan.md: "what proves "receive an update" and when the beta becomes 1.0".

Recommendation (`repo-critic.md`, item 34): publish `beta.1` and `beta.2` on the real channel and
update from one to the other; a local feed covers the gate's rounds. Velopack takes a local folder
as its feed, and the Windows steps run unattended: `Setup.exe --silent --installto <folder>`, the
update through the app, `Update.exe uninstall --silent` (`web-installers.md`); `repo-gate.md` adds a
feed on a loopback port, 47400 or later. The research does not say how a real-channel update fits
section 6, which publishes the first beta only after the acceptance.

What a skeptic overrode: `web-channels.md` had stable users read a fixed
`releases/latest/download/<feed>` address, outside the API's 60 calls an hour per IP address. Its
skeptic recommends Velopack's GitHub source for the first release (one API call a check, at most
daily, failures silent): the fixed address is untested with Velopack, and deltas for a user more
than one version behind would not be found there. The briefs also differ on how often to check
(every few hours, or at most daily); D401 settles only that the user is asked first.

The research holds no recommendation for when the beta becomes 1.0; the critic asks only that it
be written down. Bearing on it: D402 leaves R2 open on Windows through the beta, and "install
without help" not fully met there while the files are unsigned; the route, "none" among its
options, is chosen before the first stable release (section 12); a standard Mac user cannot update
an app installed into `/Applications` (section 1).

## 6. From which commit a public beta is built

plan.md: "from which commit a public beta is built, since phase work reaches `master` only on
acceptance".

The critic raised it (`repo-critic.md`, item 35 and 6.1). Options: the public betas from `master`
after "Phase 8b accepted"; or a pre-release from the phase branch, which needs the owner's leave,
since CLAUDE.md's Rules let phase work reach `master` only through the acceptance. Recommendation:
gate on local builds, and publish the first public beta from `master` right after "Phase 8b
accepted"; the critic names the branch as the owner's alternative.

## 7. The shape of the regression against the installed build

plan.md: "the shape of the regression against the installed build".

The scripted walk-through runs the app's code inside the test process, so it cannot run against an
install; only the real-window pass starts an executable, the one its csproj's `AppExecutable` fixes
at build time. Options (`repo-gate.md` 5), with times estimated from 7b's, assuming a default run
of 1 h 45 min to 1 h 50 min after 8a and 8b (8a's default run alone took 1 h 53 min 8 s at its
counted round, and its real-window pass 40 min 56 s, by the 8a handoff, so the per-round figures
below are likely low):
- **Shape 1.** The real-window pass of phases 0 to 8b against the installed app replaces the pass
  against the build output; the default run on the same commit stands for the scripted checks.
  About 2 h 45 min to 3 h a round, 50 to 60 minutes of it with the mouse.
- **Shape 2.** Both passes every round, to tell code faults from packaging faults: about 40 minutes
  more of the mouse a round. **Shape 3.** Shape 1 plus a clean machine (Windows Sandbox or a second
  account) every round: 30 to 60 minutes more.

Recommendation: shape 1, with CI's install, update and uninstall check on its Windows runner, and
one clean-profile run by hand (Windows Sandbox) before the beta. The harness then needs
(`repo-gate.md` 4.1): `AppExecutable` pointed at the installed app; an output folder of its own; an
update-check switch set off in `GitIsolation.MachineIndependence`; the test switches and the Debug
log level kept in the shipped build; a start of the real app's executable, not Velopack's root
stub, since the pass attaches by process id; and proof that the installed files are the gate's
build (their hashes, or the commit About shows since 8a, D431).

The same reader asked two more questions, which plan.md's list lacks: may CI build, install,
update and uninstall packages and hold signing secrets (recommended: a separate workflow, run by
hand and on 8b's gate commit, secrets only once signing is chosen); and may the test switches ship
(recommended: keep them, the Debug log included, and document those a user may meet). Sources:
`repo-gate.md` 4 and 5, questions 1, 3 and 4; `repo-critic.md` items 36, 38 and 39.

## 8. Test packages in the owner's own Windows profile

plan.md: "whether the opt-in install checks may install a test-labelled package in the owner's own
Windows profile".

An install writes a per-user folder, a Start-menu entry and an uninstall entry under `HKCU`, which
the app's isolation variables do not move (`repo-gate.md` 4.3). Options: the owner's profile, under
a package id of its own, removed at the end; Windows Sandbox (the owner turns it on, and git and
.NET must go in); a second local account; CI's Windows runner only.

Recommendation (`repo-gate.md`, question 2): the owner's profile under a distinct package id for
the gate's rounds, plus one Windows Sandbox run by hand before the beta. An install from the
non-elevated shell also proves that no administrator rights are needed, which a GitHub runner,
run as administrator with UAC off, cannot (`web-installers.md`). Whether `vpk pack --shortcuts`
accepts an empty list is unverified. The real-window assembly fixes `RepoRoot` at build time: a run
in Sandbox or another account needs the repository at the same path, a build there, or an
override. The owner provides: the leave.

## 9. What an uninstall removes

plan.md: "what an uninstall removes".

Outside the install folder (`repo-owed.md` 5; the 8a handoff): the data folder (settings, session,
workspaces, merge tools, accounts, avatars, logs, and since 8a `instance.lock` and
`instance.json`); the tokens in the system's keychain; git's own configuration written from
Settings; backups under `refs/visualcommit/backup/` in the user's repositories. 8a's
`docs/guide/src/install.md`, "What an uninstall leaves", says so and tells the user to sign out
first; 8b rewrites it. Velopack's `Update.exe uninstall` is documented to "Remove all app
shortcuts, files, and registry entries" on Windows; on Linux Velopack makes only an AppImage, with
no installer.

Recommendation (`repo-critic.md`, item 41): keep the data folder; delete the app's keychain items
(sign out); say in the guide what stays, the backups and git's global configuration.

Corrected since: the readers doubted the macOS data folder. 8a settled it as
`~/Library/Application Support/VisualCommit`, held by `AppPathsTests` in CI's macOS job
(architecture.md, "Settings, session, log and data folder"). Not settled: how an uninstall would
remove the keychain items. Velopack's bootstrap in `Program.Main` runs its uninstall hook
(`repo-ship.md`), but no file says what the app may do there; the critic counts tokens left after
an uninstall among the security points nobody looked at (`repo-critic.md` 6.5).

## 10. The channels for bug and security reports

plan.md: "the channels for bug and security reports".

Options (`repo-about.md`, question 5): GitHub Issues for bugs and GitHub's private vulnerability
reporting for security; Issues and an e-mail address for security; Issues alone.

Recommendation: the first. It puts no personal address in the repository or the app; private
reporting adds a "Report a vulnerability" button to the Advisories page, and `SECURITY.md` points
to it (`web-channels.md` G). With it, before the beta is announced: `SECURITY.md`; `CHANGELOG.md`
in the Keep a Changelog form; a short `CONTRIBUTING.md` that says the project uses an AI assistant;
issue templates (a bug report that asks for the app's, the system's and git's versions, and a
`config.yml` that turns off blank issues and links to private reporting). The guide's
`troubleshooting/data-and-logs.md`, "Reporting a problem", waits for the answer (plan.md). The
owner provides: the leave to turn on private vulnerability reporting, a GitHub setting (CLAUDE.md's
Rules). No cost.

## 11. When a Mac is at hand

plan.md: "when a Mac is at hand for the 78 steps of the macOS checklists of phases 0 to 7b and
phase 8a's lines ("From phase 8a, by hand" below; five of them in its report's "macOS checklist
for the owner")".

Options (`repo-owed.md` 4): a Mac during 8b; or 8b accepted with the Mac's steps after the
release, which amends the plan's visual checks ("the complete macOS checklist run by hand by the
owner"). The owner had said the checklists wait for "the end of the project, when a Mac is at hand"
(quoted there). Recommendation: settle it before 8b's design, since 8b's acceptance includes the
whole checklist (`repo-critic.md`, item 42); the research names no date. What only a Mac shows:
Gatekeeper's first launch of a file downloaded through a browser; whether the keychain prompts
after an update (section 2); an update by a standard user (section 1). Apple documents the
Developer ID certificate's signing request only through Keychain Access on a Mac (OpenSSL on
Windows is common but undocumented: `web-macos.md`, unverified). D403 notes that no person has run
the app on a Mac yet. The owner provides: a Mac (the skeptic names a borrowed or a rented one).

## 12. The Windows signing route and the publisher name

Not in plan.md's list. The 8a handoff ("Do first", step 4; "Waiting on the owner") asks for "the
Windows route, a month before the first stable release if it is to be Azure Artifact Signing, and
the publisher name it shows (D402)". `web-winsign.md` recommended Azure Artifact Signing from the
first public beta, the cheapest route that shows the owner's name and has official CI support, and
its skeptic agreed with the changes below; the owner chose an unsigned beta (D402), so the route
is for 1.0. Options:
- **Azure Artifact Signing**, Basic, as an individual: US$9.99 a month (CA$14.0235 on Azure's
  price list) plus tax, billed in full from the account's creation; 5,000 signatures a month; for
  individuals in the USA or Canada. The certificate shows the legal name with city, province and
  country, never the e-mail. An official GitHub Action on Windows runners with OIDC, or `vpk`'s own
  signing from Velopack 1.3 (a prerelease on 2026-11-18).
- **Others:** an individual (IV) certificate with cloud signing, such as SSL.com's, in the owner's
  name, about US$265 to 310 a year (its cloud signing's US$15-a-month tier gives 240 signings a
  month, which may be too few once every DLL is signed, unchecked; the next tier is US$63.75 a
  month); Certum's open-source cloud certificate, EUR 49 (US$58; the "from about €69" of
  `web-installers.md` is the card-and-reader set), shown as "Open Source Developer, <name>", with
  no official CI path. SignPath Foundation is free but shows its own name and asks for a team, a
  release and a reputation: the skeptics found the project unlikely to qualify yet.
- **The Microsoft Store's MSIX**: free, signed by Microsoft, no SmartScreen warning; but a second
  Windows package, every update certified, the Store's updates in place of Velopack's, and git
  probably cannot start the helper inside the package (a secondary source): a proof of concept
  first.
- **None**: "Windows protected your PC" on every release; Smart App Control blocks the app, with no
  override for one app, and since Windows' 2026 updates a user can turn it on without reinstalling,
  so the group that cannot run an unsigned build at all can only grow
  (`web-verify-installers-channels.md` I13 and "What the brief left out", item 3;
  `web-verify-winsign-macos.md` W11).

What the skeptics corrected:
- The brief said to sign only the project's own files. Smart App Control checks every file an app
  loads, so every shipped PE file without a valid maker's signature must be signed, third-party
  DLLs included, and tried once with Smart App Control on. Whether Artifact Signing's terms allow
  that was not checked. libSkiaSharp is reported signed by Microsoft (secondary); nothing was found
  for ANGLE's `av_libglesv2.dll` or Avalonia's managed DLLs.
- The identity check: the brief said minutes, "a few business days" by Microsoft. One skeptic found
  1 to 30 business days and a case of four weeks, and said to plan for one to four, billed
  throughout; the other read the 1 to 30 days as the organisations' figure. D402 took one to four.
- The billing account must be of type Individual, on a paid pay-as-you-go subscription, and its
  type cannot be changed: check it before creating the signing account.
- A signature does not end SmartScreen's warning for a new app ("several weeks and hundreds of clean
  installs", Microsoft); Velopack's page claims instant reputation, Microsoft's pages do not.
- A renewal that remakes the certificate profile changes its EKU, and whether reputation survives
  is unknown; Azure's free support has no technical support; tax adds about 5 to 12 %.

The publisher name follows the route: the legal name under Artifact Signing, with no custom name
possible; the developer's own name in the Store; "Open Source Developer, <name>" with Certum;
unsigned, "Unknown publisher". For Artifact Signing the owner provides the account above, a phone
with Microsoft Authenticator, a government ID with an address, an hour or two, and the identity
check's renewal (1 to 20 business days). Sources: `web-winsign.md`; `web-verify-winsign-macos.md`
1; `web-installers.md` B2; `web-verify-installers-channels.md` I10 to I14, I23 to I25.

## 13. The week's share

Not in plan.md's list. The 8a handoff ("Do first", step 2) asks for "the week's share (D241)": a
phase or a stage starts only when the share of the week's allowance left covers it. The research
holds no recommendation; the critic notes that no reader sized 8a or 8b against the allowance
(`repo-critic.md` 6.7). The owner gives the figure.

## 14. The three live tokens

Not in plan.md's list. The 8a handoff ("Do first", step 4; "Risks to watch"; "Waiting on the
owner") asks that "before 8b's gate the owner makes all three again, with an expiry that covers
it": GitHub's sandbox token expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04, and a
gate runs the live command twice (D335). Options (`repo-owed.md` 4; `repo-gate.md`, question 5):
make each token again; or a decision amending D335 that a phase adding no hosting code skips the
live command. Recommendation: keep the live command and make each token again "with an expiry that
covers 8b's gate" (`repo-critic.md`, item 23), each entered as a Windows user variable in Windows'
Environment Variables dialog, never typed in a shell, as 7a's design 14.6 and 7b's design 15.8 say
(on 2026-11-05 the owner set Azure DevOps' and GitLab's four variables with a script of the lead's
instead; 8a handoff, "Risks to watch").

## 15. Already answered: not to be asked again

- D397: the licence stays MIT, in the owner's name. D404: the README's "A personal project, shared
  freely" note. D398, D409, D431: About, the settings screen's sixth section. D399: phase 8 split
  into 8a and 8b; 8b on `phase/8b-packaging-beta`.
- D400: installers, update files and betas on this repository's GitHub Releases, betas as
  pre-releases; the owner approves each release before it is signed and published; no tag
  publishes by itself; winget and other channels undecided.
- D401: the app asks before its first update check, with a settings switch, "Check for updates"
  and `PRIVACY.md`, as `web-channels.md` recommended; `repo-ship.md`'s "on by default" was not
  taken.
- D402: Windows' beta ships unsigned; the route and its publisher name come before the first stable
  release (section 12); the research's "signed from the first beta" was not taken.
- D403: macOS signed with the owner's Developer ID and notarised on GitHub's macOS runners.
- D405 (with D423, D449 and D453): no git bundled; the first-run screen, with winget for a
  missing or too-old git where winget exists. D406, D420: the oldest git is 2.34.1, tested by CI's
  floor job, looked at again in May 2027.
- D407, D428 to D430, D447: the icon's files, the `.icns` among them, committed; macOS 26's `.icon`
  is in plan.md's 8b items. D408, D439, D451: the guide, built by mdBook, on GitHub Pages.
- D411: no high-contrast palette in 1.0. D412: no registered trademark. D413: risks 39, 40 and 43
  stay recorded risks. D421, D464: one instance per data folder; the updater's second key is in
  plan.md's 8b items. The macOS data folder: `~/Library/Application Support/VisualCommit`.
