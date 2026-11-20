using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The first-run screen's state of a search's result (<see cref="GitSetupStates.Of"/>): git's own
/// states first, the helper's only with a usable git, and Searching before any search has ended. Pure.
/// </summary>
public class GitSetupStatesTests
{
    private const string HelperPath = "/home/demo/visualcommit/visualcommit-askpass";

    private static readonly GitVersion Old = new(2, 29, 0);

    [Fact]
    public void Before_any_search_has_ended_the_state_is_Searching_whatever_the_helper()
    {
        Assert.Equal(GitSetupState.Searching, GitSetupStates.Of(null, null));
        Assert.Equal(GitSetupState.Searching, GitSetupStates.Of(null, HelperPath));
    }

    [Fact]
    public void A_usable_git_is_Found_and_HelperMissing_while_the_helper_is_missing()
    {
        var found = new GitDetection(GitAvailability.Available, "/usr/bin/git", GitVersion.Minimum, null);

        Assert.Equal(GitSetupState.Found, GitSetupStates.Of(found, null));
        Assert.Equal(GitSetupState.HelperMissing, GitSetupStates.Of(found, HelperPath));
        Assert.Equal(GitSetupState.HelperMissing, GitSetupStates.Of(found with { Source = GitSource.Setting }, HelperPath));
    }

    [Fact]
    public void No_git_is_Missing_and_a_chosen_git_that_is_gone_ChosenMissing_with_or_without_a_candidate()
    {
        var none = new GitDetection(GitAvailability.NotFound, null, null, null);

        Assert.Equal(GitSetupState.Missing, GitSetupStates.Of(none, null));
        Assert.Equal(GitSetupState.Missing, GitSetupStates.Of(none with { CommandLineToolsMissing = true }, null));
        Assert.Equal(GitSetupState.ChosenMissing, GitSetupStates.Of(none with { ChosenMissing = "/home/demo/git/bin/git" }, null));
        Assert.Equal(GitSetupState.ChosenMissing, GitSetupStates.Of(none with { ChosenMissing = "/home/demo/git/bin/git", PathCandidate = "/usr/bin/git" }, null));
    }

    [Fact]
    public void Git_states_come_before_the_helpers_so_a_missing_helper_shows_only_with_a_usable_git()
    {
        var tooOld = new GitDetection(GitAvailability.TooOld, "/usr/bin/git", Old, null);
        var broken = new GitDetection(GitAvailability.Broken, "/usr/bin/git", null, null) { ErrorText = "fatal: bad config line 1 in file /home/demo/.gitconfig", ExitCode = 128, ConfigFault = new GitConfigFault("/home/demo/.gitconfig", 1) };
        var notStarted = new GitDetection(GitAvailability.Broken, "/usr/bin/git", null, null) { ErrorText = "Permission denied" };

        Assert.Equal(GitSetupState.TooOld, GitSetupStates.Of(tooOld, null));
        Assert.Equal(GitSetupState.TooOld, GitSetupStates.Of(tooOld, HelperPath));
        Assert.Equal(GitSetupState.NotWorking, GitSetupStates.Of(broken, null));
        Assert.Equal(GitSetupState.NotWorking, GitSetupStates.Of(broken, HelperPath));
        Assert.Equal(GitSetupState.NotWorking, GitSetupStates.Of(notStarted, null));
        Assert.Equal(GitSetupState.Missing, GitSetupStates.Of(new GitDetection(GitAvailability.NotFound, null, null, null), HelperPath));
    }

    [Fact]
    public void A_detections_new_members_default_to_a_git_from_the_PATH_with_nothing_more_to_say()
    {
        var detection = new GitDetection(GitAvailability.Available, "/usr/bin/git", GitVersion.Minimum, null);

        Assert.Equal(GitSource.Path, detection.Source);
        Assert.Null(detection.ErrorText);
        Assert.Null(detection.ExitCode);
        Assert.Null(detection.ConfigFault);
        Assert.Null(detection.ChosenMissing);
        Assert.Null(detection.PathCandidate);
        Assert.False(detection.CommandLineToolsMissing);
    }
}
