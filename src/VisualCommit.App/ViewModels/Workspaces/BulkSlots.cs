namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The window's slots for the rows of bulk runs (docs/phase-6-design.md 4.3, D210): at most
/// <see cref="Count"/> at once over every run, never two at once to one common git folder, nor one
/// to a common folder another queue is fetching from; first come, first served.
/// <c>TabServices.BulkSlots</c>, one per window.
/// <para>A stub: work package WP-B builds it. Until then no slot is ever out.</para>
/// </summary>
public sealed class BulkSlots
{
    /// <summary>How many rows of all the window's runs run at once.</summary>
    public const int Count = 4;

    /// <summary>Whether a slot is out on the common git folder <paramref name="commonKey"/> (auto-fetch skips a tick then, 4.3).</summary>
    public bool IsHeldFor(string commonKey) => false;
}
