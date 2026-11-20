// Not VisualCommit.GitStandIn: the test projects that reference this program use the type
// VisualCommit.Testing.GitStandIn, which a namespace of that name would hide.
namespace VisualCommit.GitStandInProgram;

/// <summary>
/// <c>visualcommit-git-stand-in</c>: what the app finds as git when the test switch
/// <c>VISUALCOMMIT_GIT_STAND_IN</c> names it (docs/phase-8a-design.md 1.14). It answers, in order:
/// with <c>VISUALCOMMIT_GIT_STAND_IN_FAILURE</c> set, that text on standard error and exit 128,
/// whatever the arguments (a git that does not work, a broken configuration's line); with the one
/// argument <c>--version</c>, the line <c>VISUALCOMMIT_GIT_STAND_IN_VERSION</c> gives (by default
/// <c>git version 2.29.0</c>, older than any git the app supports) and exit 0; otherwise a line of
/// its own on standard error and exit 1. It references nothing, so the variables' names are
/// written here as the tests' <c>GitStandIn</c> writes them.
/// </summary>
internal static class Program
{
    private const string VersionVariable = "VISUALCOMMIT_GIT_STAND_IN_VERSION";
    private const string FailureVariable = "VISUALCOMMIT_GIT_STAND_IN_FAILURE";
    private const string DefaultVersion = "git version 2.29.0";

    private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable(FailureVariable) is { Length: > 0 } failure)
        {
            Console.Error.WriteLine(failure);
            return 128;
        }

        if (args is ["--version"])
        {
            Console.Out.WriteLine(Environment.GetEnvironmentVariable(VersionVariable) is { Length: > 0 } version ? version : DefaultVersion);
            return 0;
        }

        Console.Error.WriteLine("fatal: VisualCommit's stand-in for git answers only --version");
        return 1;
    }
}
