namespace VisualCommit.App.Views;

/// <summary>
/// How wide the two side panels are drawn. The user sets each panel's preferred width by
/// dragging its edge; when the window is too narrow for both at those widths and the graph at its
/// minimum, the panels give way, each in proportion to how far it is above its own minimum, and
/// they take their preferred widths back when the window grows again (phase 1, from phase 0's
/// known issues).
/// </summary>
public static class PanelLayout
{
    public const double LeftDefault = 260;
    public const double LeftMinimum = 180;
    public const double LeftMaximum = 520;
    public const double RightDefault = 400;
    public const double RightMinimum = 280;
    public const double RightMaximum = 720;
    public const double GraphMinimum = 320;

    /// <summary>
    /// The widths to draw the panels at in a main area <paramref name="available"/> wide, for the
    /// preferred widths <paramref name="left"/> and <paramref name="right"/>. Preferred widths
    /// outside a panel's range are brought into it first. When even the minimums do not leave
    /// the graph its minimum, the panels stay at their minimums.
    /// </summary>
    public static (double Left, double Right) Fit(double available, double left, double right)
    {
        left = Math.Clamp(left, LeftMinimum, LeftMaximum);
        right = Math.Clamp(right, RightMinimum, RightMaximum);

        var excess = left + right + GraphMinimum - available;
        if (excess <= 0)
        {
            return (left, right);
        }

        var leftRoom = left - LeftMinimum;
        var rightRoom = right - RightMinimum;
        var room = leftRoom + rightRoom;
        if (room <= excess)
        {
            return (LeftMinimum, RightMinimum);
        }

        return (left - (excess * leftRoom / room), right - (excess * rightRoom / room));
    }
}
