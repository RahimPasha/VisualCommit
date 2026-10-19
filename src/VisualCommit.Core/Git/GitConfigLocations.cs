namespace VisualCommit.Core.Git;

/// <summary>
/// Where git keeps the configuration files the settings screen names (docs/phase-6-design.md 8.2,
/// facts 0.3), worked out from the environment git gets, never asked of git: <c>git var
/// GIT_CONFIG_GLOBAL</c> names the global files only from Git 2.55 on.
/// </summary>
public static class GitConfigLocations
{
    /// <summary>The variable that names the global file and turns the usual ones off (Git 2.32).</summary>
    public const string GlobalVariable = "GIT_CONFIG_GLOBAL";

    /// <summary>The variable under which git looks for <c>git/config</c> (and <c>git/ignore</c>).</summary>
    public const string XdgVariable = "XDG_CONFIG_HOME";

    /// <summary>
    /// The file <c>git config --global</c> writes, by git's rule (0.3): <c>GIT_CONFIG_GLOBAL</c> when
    /// set; otherwise <c>&lt;home&gt;/.gitconfig</c> when it exists, else the XDG file
    /// (<c>$XDG_CONFIG_HOME/git/config</c>, or <c>&lt;home&gt;/.config/git/config</c> with the
    /// variable unset or empty) when it exists, else <c>&lt;home&gt;/.gitconfig</c>, which git then
    /// creates. The home folder is <c>HOME</c> when set; on Windows with <c>HOME</c> unset,
    /// <c>HOMEDRIVE</c>+<c>HOMEPATH</c> when both are set and that folder exists, else
    /// <c>USERPROFILE</c>, as Git for Windows sets <c>HOME</c> at its start. Null when there is no
    /// home folder (git then refuses a global write: <c>$HOME not set</c>), and when
    /// <c>GIT_CONFIG_GLOBAL</c> is set but empty: git then reads no global file, not even
    /// <c>~/.gitconfig</c>, and refuses a global write (<c>error: could not write config file :
    /// Is a directory</c>, exit 4; an <c>--unset-all</c> exits 5 and changes nothing; Git 2.36 and
    /// 2.55, 2026-10-18).
    /// </summary>
    /// <param name="environment">The value of a variable in the environment git gets, null when unset: the session's git environment over the process's (8.12).</param>
    /// <param name="fileExists">Whether a file exists.</param>
    /// <param name="directoryExists">Whether a folder exists.</param>
    /// <param name="isWindows">Whether git runs on Windows: its home rule, and backslashes in the paths.</param>
    public static string? GlobalFile(Func<string, string?> environment, Func<string, bool> fileExists, Func<string, bool> directoryExists, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);

        if (environment(GlobalVariable) is { } global)
        {
            return global.Length > 0 ? Native(global, isWindows) : null;
        }

        if (Home(environment, directoryExists, isWindows) is not { } home)
        {
            return null;
        }

        var user = Join(home, ".gitconfig", isWindows);
        if (fileExists(user))
        {
            return user;
        }

        var xdg = XdgFile(environment, home, isWindows);
        return fileExists(xdg) ? xdg : user;
    }

    /// <summary>
    /// The XDG configuration file git reads before <c>~/.gitconfig</c> when <c>GIT_CONFIG_GLOBAL</c>
    /// is unset: <c>$XDG_CONFIG_HOME/git/config</c>, or with the variable unset or empty
    /// <c>&lt;home&gt;/.config/git/config</c>; null without either. With <c>GIT_CONFIG_GLOBAL</c>
    /// set git reads no such file (0.3), and this still names where it would lie.
    /// </summary>
    public static string? XdgConfigFile(Func<string, string?> environment, Func<string, bool> directoryExists, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(directoryExists);
        if (environment(XdgVariable) is { Length: > 0 } xdgHome)
        {
            return Join(Join(xdgHome, "git", isWindows), "config", isWindows);
        }

        return Home(environment, directoryExists, isWindows) is { } home ? XdgFile(environment, home, isWindows) : null;
    }

    /// <summary>The home folder git uses (see <see cref="GlobalFile"/>), or null.</summary>
    public static string? Home(Func<string, string?> environment, Func<string, bool> directoryExists, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(directoryExists);
        if (environment("HOME") is { } home)
        {
            return Native(home, isWindows);
        }

        if (!isWindows)
        {
            return null;
        }

        // Git for Windows: the home share first (a domain's H:), USERPROFILE when it is missing.
        if (environment("HOMEDRIVE") is { } drive && environment("HOMEPATH") is { } path)
        {
            var share = Native(drive + path, isWindows);
            if (directoryExists(share))
            {
                return share;
            }
        }

        return environment("USERPROFILE") is { } profile ? Native(profile, isWindows) : null;
    }

    private static string XdgFile(Func<string, string?> environment, string home, bool isWindows) =>
        environment(XdgVariable) is { Length: > 0 } xdgHome
            ? Join(Join(xdgHome, "git", isWindows), "config", isWindows)
            : Join(Join(Join(home, ".config", isWindows), "git", isWindows), "config", isWindows);

    private static string Join(string folder, string name, bool isWindows)
    {
        var separator = isWindows ? '\\' : '/';
        var trimmed = folder.TrimEnd('/', isWindows ? '\\' : '/');
        return Native(trimmed, isWindows) + separator + name;
    }

    /// <summary>On Windows with backslashes only, as the app's paths are; elsewhere as given.</summary>
    private static string Native(string path, bool isWindows) => isWindows ? path.Replace('/', '\\') : path;
}
