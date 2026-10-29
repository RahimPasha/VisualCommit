namespace VisualCommit.Hosting;

public sealed partial class RepositoryHosting
{
    /// <summary>Authors of graph commits on GitHub remotes' branches (D311): built in the next step.</summary>
    public void ShowAvatarsFor(IReadOnlyCollection<string> emails) => ArgumentNullException.ThrowIfNull(emails);

    private void StopAvatars()
    {
    }

    private void DisposeAvatars()
    {
    }
}
