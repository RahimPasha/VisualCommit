namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the settings screen's operations and of Open in editor (docs/phase-6-design.md 7,
/// 8.3, 12, 14.1, 14.2, 14.6, 14.8, 14.10), and the screen's own texts (<see cref="SettingsTexts"/>).
/// </summary>
public static partial class OperationTexts
{
    /// <summary>This repository's writes of the settings screen's Save, one operation of its queue (8.3, 14.1).</summary>
    public static OperationText SaveRepositorySettings { get; } =
        new("Save repository settings", "Saving the repository's settings…", "Saving the settings failed", "Git could not save the repository's settings.");

    /// <summary>The result of <see cref="SaveRepositorySettings"/> in the status bar (14.2).</summary>
    public const string SavedRepositorySettings = "Saved the repository's settings";

    /// <summary>The error title of a failed global write of the settings screen (7.8, 14.1).</summary>
    public const string SaveSettingsFailedTitle = "Saving the settings failed";

    /// <summary>The summary of a failed global write (7.8, 14.1): no repository line.</summary>
    public const string SaveGlobalSettingsFailed = "Git could not save the settings for all repositories.";

    /// <summary>The error title when the chosen editor cannot start (12.7).</summary>
    public const string EditorFailedTitle = "Could not open the editor";

    /// <summary>
    /// <c>Open settings…</c>: the palette's command (5.4, 14.7) and the editor failure's extra
    /// button, which opens the settings screen on Tools (12.7); it opens a dialog, so it ends in "…".
    /// </summary>
    public const string OpenSettingsButton = "Open settings…";

    /// <summary>The summary when the editor could not be started (12.7).</summary>
    public static string EditorCouldNotStart(string name, string message) => $"{name} could not be started: {message}";

    /// <summary>The summary when the editor's program is gone (12.7).</summary>
    public static string EditorIsGone(string name, string program) => $"{name} is not at {program} any more: choose an editor in Settings.";

    public static partial class MenuTexts
    {
        /// <summary>A file menu's item after "Open file" while an editor is chosen (docs/phase-6-design.md 12.1): fixed, whichever editor it is.</summary>
        public const string OpenInEditor = "Open in editor";
    }

    /// <summary>
    /// The settings screen's texts (docs/phase-6-design.md 14.8, 14.6, 7, 8, 12), one place for the view
    /// models and the view, which binds its labels, notes and buttons to them with <c>x:Static</c>
    /// (<c>Views/Dialogs/SettingsDialogView.axaml</c>).
    /// </summary>
    public static class SettingsTexts
    {
        // The frame (7.1, 7.8).
        public const string Title = "Settings";
        public const string General = "General";
        public const string Git = "Git";
        public const string Tools = "Tools";
        public const string KeyboardShortcuts = "Keyboard shortcuts";
        public const string HasError = "Has an error";
        public const string Saving = "Saving…";
        public const string Save = "Save";
        public const string Cancel = "Cancel";

        /// <summary>The toolbar's Settings button (7.2, 6.8), before its key.</summary>
        public const string OpenSettings = "Open settings";

        public static string WaitingFor(string title) => $"Waiting for {title} to finish…";

        // General (7.3).
        public const string ThemeLabel = "Theme";
        public const string ThemeDark = "Dark";
        public const string ThemeLight = "Light";
        public const string ThemeSystem = "System";
        public const string ThemeNote = "System follows your operating system's light or dark mode.";
        public const string FetchingLabel = "Fetching";
        public const string AutoFetch = "Fetch in the background every";
        public const string Minutes = "minutes";
        public const string AutoFetchNote = "Only the active tab's repository; it never asks for a password.";
        public const string AutoFetchError = "Enter a whole number of minutes from 1 to 60.";
        public const string WorkingTreeLabel = "Working tree";
        public const string KeepIgnored = "Keep ignored files";
        public const string KeepIgnoredNote =
            "A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. " +
            "Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.";
        public const string StashUntracked = "Include untracked files when stashing";
        public const string StashUntrackedNote = "The Stash changes dialog starts with this; its own check box changes it too.";
        public const string OpeningFilesLabel = "Opening files";
        public const string RunnableIntro = "Open file asks first before it opens a file with one of these extensions:";
        public const string AlsoAskFor = "Also ask for";
        public const string RunnablePlaceholder = ".py .pl";
        public const string RunnableUnix = "On Linux and macOS it also asks before it opens a file marked as a program.";

        public static string FromPathExt(string extensions) => $"From PATHEXT: {extensions}";

        public static string NotAnExtension(string entry) => $"\"{entry}\" is not an extension such as .py: use letters and digits.";

        // Git (7.4, 8).
        public const string ScopeGlobal = "All repositories (global)";
        public const string ScopeRepository = "This repository";
        public const string ScopeRepositoryDisabled = "Open a repository's tab to change its own settings";
        public const string CaptionGlobal = "Saved in your global git configuration, which git in a terminal uses too.";
        public const string CaptionRepository = "Saved in this repository's .git/config, which all its worktrees share. These values win over those for all repositories.";
        public const string Reading = "Reading git's configuration…";
        public const string ReadFailed = "Git's configuration could not be read:";
        public const string GitNeeded = " Git's own settings need it.";
        public const string NameLabel = "Name";
        public const string EmailLabel = "Email";
        public const string NotSet = "Not set";
        public const string NameRefused = "A name cannot contain <, > or a line break.";
        public const string NamePunctuation = "Git refuses a name made only of punctuation.";
        public const string EmailRefused = "An email address cannot contain <, > or a line break.";
        public const string EmailWarning = "This does not look like an email address.";
        public const string NoEmail = "Git cannot make commits here: no email is set.";
        public const string NoName = "Git cannot make commits here: no name is set.";
        public const string EmptyName = "Git cannot make commits here: the name is empty.";
        public const string RefusedName = "Git cannot make commits here: it refuses the name.";
        public const string CannotCommit = "Git cannot make commits here:";
        public const string UseConfigOnly = "user.useConfigOnly is set, so git does not guess a missing name or email.";
        public const string EmailVariable = "EMAIL is set in the environment the app was started with, so git uses it when no email is set.";
        public const string PullLabel = "Pull";
        public const string PullSameAsGlobal = "Same as global";
        public const string PullMerge = "Merge (fast-forward when possible)";
        public const string PullMergeCommit = "Merge, always with a merge commit";
        public const string PullRebase = "Rebase";
        public const string PullRebaseMerges = "Rebase, keeping merge commits";
        public const string PullFastForwardOnly = "Fast-forward only";
        public const string PullNotSetGlobalTip = "Nothing set: the app merges, and git in a terminal stops when the branches have diverged";
        public const string PullNotSetRepositoryTip = "Uses the pull mode for all repositories";
        public const string MergeToolLabel = "Merge tool";
        public const string Choose = "Choose…";
        public const string MergeToolNote = "Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.";
        public const string MergeToolsLoading = "Looking for the merge tools git knows…";
        public const string MergeToolsFailed = "Git could not list its merge tools.";
        public const string UseRepositorySetting = "Use the repository's setting";

        public static string SameAsGlobalValue(string value) => $"Same as global: {value}";

        public static string FromSystem(string value) => $"From git's system configuration: {value}";

        public static string CommitsMadeAs(string author) => $"Commits here are made as {author}.";

        public static string CommitsMadeAs(string author, string committer) => $"Commits here are made as {author}, committed as {committer}.";

        public static string VariableForAuthors(string variable) => $"{variable} is set in the environment the app was started with, so git uses it for authors.";

        public static string VariableForCommitters(string variable) => $"{variable} is set in the environment the app was started with, so git uses it for committers.";

        public static string KeyForAuthors(string key, string value, string file) => $"{key} = {value} ({file}) is used for authors instead.";

        public static string KeyForCommitters(string key, string value, string file) => $"{key} = {value} ({file}) is used for committers instead.";

        public static string PullInEffect(string branch, string mode, string key, string value, string file) => $"Pull into {branch}: {mode} ({key} = {value} in {file}).";

        public static string PullNothingSet(string branch) =>
            $"Pull into {branch}: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.";

        public static string PullFails(string branch, string problem) => $"Pull into {branch} fails: {problem}";

        public static string BranchRebase(string branch, string value) =>
            $"{branch} has its own pull setting, branch.{branch}.rebase = {value}, which git uses instead of the mode above.";

        public static string BranchRebaseMarked(string branch) => $"Save removes branch.{branch}.rebase, so {branch} pulls as set above.";

        public static string KeepBranchSetting(string branch) => $"Keep {branch}'s setting";

        public static string MergeToolSameAsGlobal(string? tool) => tool is null ? "Same as global (not set)" : $"Same as global ({tool})";

        public static string MergeToolNotFound(string tool) => $"git did not find {tool} on this computer: Merge tool… fails until it is installed.";

        // 8.4's notices.
        public static string StillLater(string value, string file) => $"Still {value} in {file}, which git reads later.";

        public static string StillSet(string value, string file) => $"Still set to {value} in {file}: the app cannot change that file.";

        public static string GitUses(string value, string file) => $"Git uses {value} from {file}.";

        public static string InRepositoryGitUses(string repository, string value, string file) => $"In {repository}, git uses {value} from {file}.";

        public static string WorktreeOwn(string value) => $"This worktree has its own value, {value}, in config.worktree; the app does not change it.";

        public static string FromEnvironment(string value) => $"{value} comes from the environment the app was started with and wins over every file.";

        public static string AlsoAppliesFromSystem(string key, string value) => $"{key} = {value} also applies, from git's system configuration.";

        public static string SetsItsOwn(string repository, string value) => $"{repository} sets its own: {value}.";

        // 8.2's names of files.
        public const string GlobalFileName = "your global configuration";
        public const string XdgUnderVariable = "git/config under XDG_CONFIG_HOME";
        public const string XdgInHome = ".config/git/config";
        public const string SystemFileName = "git's system configuration";
        public const string LocalFileName = ".git/config";
        public const string WorktreeFileName = "config.worktree (this worktree only)";
        public const string CommandScopeName = "the environment the app was started with";

        public static string Included(string fileName) => $"{fileName} (included)";

        public static string IncludedByLocal(string fileName) => $"{fileName} (included by .git/config)";

        // Tools (7.5, 12.5).
        public const string EditorLabel = "Editor";
        public const string EditorNone = "None";
        public const string EditorOther = "Other";
        public const string GitsEditor = " (git's editor)";
        public const string EditorNote = "Open in editor appears in the file menus, beside Open file, once an editor is chosen.";
        public const string LookingForEditors = "Looking for editors…";
        public const string ProgramLabel = "Program";
        public const string ArgumentsLabel = "Arguments";
        public const string Browse = "Browse…";
        public const string ArgumentsHint = "{path} is the file's full path, {line} its line (1 for now). Without {path}, the path comes last.";
        public const string ChooseProgramTitle = "Choose the editor's program";
        public const string ProgramEmpty = "Choose the editor's program.";
        public const string ProgramMissing = "There is no file at this path.";
        public const string ProgramNotExe = "Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.";
        public const string ProgramNotExecutable = "This file is not marked as a program.";
        public const string DefaultArguments = "{path}";

        // Keyboard shortcuts (7.6).
        public const string ShortcutsIntro = "These keys are fixed.";

        // The Pick dialogs (7.7, 14.6).
        public const string ChooseEditorTitle = "Choose an editor";
        public const string ChooseMergeToolTitle = "Choose a merge tool";
        public const string PickConfirm = "Choose";
        public const string PickNone = "None";
        public const string PickNoneDetail = "no Open in editor in the file menus";
        public const string PickOther = "Other…";
        public const string PickOtherDetail = "a program of your choice";
        public const string PickNotSetGlobalDetail = "Merge tool… stays disabled";
        public const string ToolFound = "found on this computer";
        public const string ToolNotFound = "not found on this computer";
        public const string ToolUnknown = "git does not know this tool";
        public const string ToolInTerminal = "runs in a terminal; the app cannot start it";
        public const string GraphicalSession = " (requires a graphical session)";

        public static string PickNotSetRepositoryDetail(string? tool) => tool is null ? "same as global (not set)" : $"same as global ({tool})";

        public static string ToolOfYourOwn(string name) => $"your own tool (mergetool.{name}.cmd)";
    }
}
