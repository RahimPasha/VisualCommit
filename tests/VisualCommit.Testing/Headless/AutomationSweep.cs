using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using VisualCommit.App.Views.Dialogs;

namespace VisualCommit.Testing.Headless;

/// <summary>One control a screen reader cannot tell by its name, in one state of the app.</summary>
/// <param name="State">The state the sweep was run in, as the test names it.</param>
/// <param name="AutomationId">The control's automation id, or null.</param>
/// <param name="ClassName">The class name UI Automation gives it (its peer's).</param>
/// <param name="ControlType">Its control type.</param>
/// <param name="Name">The name UI Automation reads, or null.</param>
/// <param name="Rule">The rule its name breaks: <see cref="AutomationSweep.Empty"/>, <see cref="AutomationSweep.TypeName"/>, <see cref="AutomationSweep.KeyText"/> or <see cref="AutomationSweep.Duplicate"/>.</param>
public sealed record AutomationProblem(string State, string? AutomationId, string ClassName, AutomationControlType ControlType, string? Name, string Rule)
{
    /// <summary>The problem in one line: its rule, the control's type, id and class, and its name.</summary>
    public override string ToString() =>
        $"{Rule}: {ControlType} {AutomationId ?? "(no id)"} (class {ClassName}), named {(Name is null ? "nothing" : "\"" + Name + "\"")}";
}

/// <summary>The sweep's failure: every control of the window without a usable name, one per line.</summary>
public sealed class AutomationSweepException : Exception
{
    public AutomationSweepException()
    {
    }

    public AutomationSweepException(string message)
        : base(message)
    {
    }

    public AutomationSweepException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The sweep of names (docs/phase-8a-design.md 1.12, 9.3.8): walks the automation peers of a
/// window depth first from its own peer, as a client reads them, and reports each interactive
/// control element whose name a screen reader cannot use. Interactive is a control type a user
/// acts on (button, check box, radio button, edit, combo box, list item, menu item, tab item,
/// hyperlink, slider, thumb) or a pattern to act through (invoke, toggle, selection item, expand
/// and collapse, range value, a writable value). A control that is not shown
/// (<see cref="Avalonia.Visual.IsVisible"/> false on it or above it) is not walked; one drawn at
/// opacity 0 is, as UI Automation keeps it. A scroll bar is not walked either (D456).
/// </summary>
public static partial class AutomationSweep
{
    /// <summary>The rule for a name that is null or blank.</summary>
    public const string Empty = "empty";

    /// <summary>The rule for a name that is a .NET type's full name, as a panel given as content reads.</summary>
    public const string TypeName = "type name";

    /// <summary>The rule for a name that ends in its keys in parentheses: the key is the accelerator key's, never the name's.</summary>
    public const string KeyText = "key text";

    /// <summary>The rule for two controls of one list, or of one dialog outside its lists, with one automation id and one name.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>
    /// The one control exempt from <see cref="Duplicate"/>, by its automation id, under the list
    /// that holds it. A row of the branch list is told apart by its place under its section and its
    /// folders alone (two <c>main</c> rows, under Local branches and under a remote): its full name
    /// is the tooltip of its name's text (<c>RefName</c>), not of the row's button, so the row's peer
    /// gives no help text; the left panel as a tree with levels is the backlog's. Naming the rows by
    /// their full names instead would change every lookup that finds a row by its text.
    /// </summary>
    private static readonly (string AutomationId, string ListAutomationId) DuplicateExemption = ("RefItem", "RefList");

    private static readonly HashSet<AutomationControlType> InteractiveTypes =
    [
        AutomationControlType.Button, AutomationControlType.CheckBox, AutomationControlType.RadioButton, AutomationControlType.Edit,
        AutomationControlType.ComboBox, AutomationControlType.ListItem, AutomationControlType.MenuItem, AutomationControlType.TabItem,
        AutomationControlType.Hyperlink, AutomationControlType.Slider, AutomationControlType.Thumb,
    ];

    /// <summary>A key a key text may name without a modifier.</summary>
    private static readonly HashSet<string> BareKeys = new(StringComparer.Ordinal)
    {
        "Esc", "Enter", "Tab", "Space", "Delete", "Backspace", "Home", "End", "PageUp", "PageDown", "Up", "Down", "Left", "Right", "Menu",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
    };

    /// <summary>The modifiers a key text starts with, in the words of Windows and of macOS.</summary>
    private static readonly string[] Modifiers = ["Ctrl+", "Shift+", "Alt+", "Cmd+", "Option+"];

    /// <summary>The problems of every interactive control in <paramref name="root"/>, in the walk's order, the duplicates last.</summary>
    public static IReadOnlyList<AutomationProblem> FindProblems(TopLevel root, string state)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(state);
        var problems = new List<AutomationProblem>();
        var checkedPeers = new List<(AutomationPeer Peer, AutomationPeer? List, string? Id, string? Name)>();
        Walk(ControlAutomationPeer.CreatePeerForElement(root), list: null);

        // Duplicates: within one list, the members of each group of one id and one name, groups in
        // the order their first member was met.
        foreach (var group in checkedPeers
            .Where(entry => entry.List is not null && !string.IsNullOrWhiteSpace(entry.Name) && !IsExempt(entry.Id, entry.List))
            .GroupBy(entry => (entry.List, entry.Id, entry.Name))
            .Where(group => group.Count() > 1))
        {
            problems.AddRange(group.Select(entry => Problem(state, entry.Peer, entry.Name, Duplicate)));
        }

        return problems;

        void Walk(AutomationPeer peer, AutomationPeer? list)
        {
            if (peer is ControlAutomationPeer { Owner: { } owner } && !owner.IsVisible)
            {
                return;
            }

            // A scroll bar and what lies inside it are not checked (D456 (1)): it is a part of a
            // scrolling control's template; a screen reader announces it by its type and value, and
            // a reader scrolls with the keys of the control that holds it.
            if (peer.GetAutomationControlType() == AutomationControlType.ScrollBar)
            {
                return;
            }

            if (peer.IsControlElement() && IsInteractive(peer))
            {
                var name = peer.GetName();
                var rule = RuleBroken(name, peer.GetAcceleratorKey());
                if (rule is not null)
                {
                    problems.Add(Problem(state, peer, name, rule));
                }

                checkedPeers.Add((peer, list, peer.GetAutomationId(), name));
            }

            // A control's container is the nearest list above it, the search ending at the root of
            // the dialog it lies in (D456 (2)): two dialogs that lie over each other in the window's
            // dialog layer (a sign-in dialog over the settings screen) each have a Cancel, told
            // apart by their dialogs.
            var childList = peer is ControlAutomationPeer { Owner: ItemsControl or DialogView } ? peer : list;
            foreach (var child in peer.GetChildren())
            {
                Walk(child, childList);
            }
        }
    }

    /// <summary>Fails, naming every problem one per line, when <paramref name="root"/> has any.</summary>
    /// <exception cref="AutomationSweepException">The window has a control without a usable name.</exception>
    public static void AssertNamed(TopLevel root, string state)
    {
        var problems = FindProblems(root, state);
        if (problems.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.Append(CultureInfo.InvariantCulture, $"The sweep found {problems.Count} control{(problems.Count == 1 ? string.Empty : "s")} without a usable name in {state}:");
        foreach (var problem in problems)
        {
            message.Append("\n  ").Append(problem);
        }

        throw new AutomationSweepException(message.ToString());
    }

    /// <summary>The rule <paramref name="name"/> breaks, or null: empty, then a type's name, then its keys at its end.</summary>
    public static string? RuleBroken(string? name, string? acceleratorKey)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Empty;
        }

        if (TypeNamePattern().IsMatch(name))
        {
            return TypeName;
        }

        return EndsInKeys(name, acceleratorKey) ? KeyText : null;
    }

    /// <summary>
    /// Whether the name ends in a parenthesis that holds its own accelerator key, or whose parts,
    /// split at ", ", are each a key text: "Close (Esc)", "Previous match (Shift+Enter, Shift+F3)";
    /// not "Pull requests (3)".
    /// </summary>
    private static bool EndsInKeys(string name, string? acceleratorKey)
    {
        var match = TrailingParenthesis().Match(name);
        if (!match.Success)
        {
            return false;
        }

        var inside = match.Groups["inside"].Value;
        if (!string.IsNullOrWhiteSpace(acceleratorKey) && inside == acceleratorKey)
        {
            return true;
        }

        return inside.Split(", ").All(IsKeyRange);
    }

    /// <summary>
    /// A key text, or two joined by " to " (<c>Ctrl+1 to Ctrl+8</c>), the form in which the app's
    /// own key texts give a run of keys (<c>ShortcutText.FormatAll</c>), which design 9.3.8's
    /// wording leaves out.
    /// </summary>
    private static bool IsKeyRange(string part)
    {
        var ends = part.Split(" to ");
        return ends.Length is 1 or 2 && ends.All(IsKeyText);
    }

    /// <summary>
    /// <c>Ctrl+</c>, <c>Shift+</c>, <c>Alt+</c>, <c>Cmd+</c> or <c>Option+</c>, any of them in a
    /// row, and a key (a character, or a key's word); without a modifier only the keys a key text
    /// names alone (<see cref="BareKeys"/>).
    /// </summary>
    private static bool IsKeyText(string text)
    {
        var rest = text;
        var modified = false;
        while (Modifiers.FirstOrDefault(modifier => rest.StartsWith(modifier, StringComparison.Ordinal)) is { } modifier && rest.Length > modifier.Length)
        {
            rest = rest[modifier.Length..];
            modified = true;
        }

        if (!modified)
        {
            return BareKeys.Contains(rest);
        }

        return rest.Length == 1 || BareKeys.Contains(rest) || KeyWord().IsMatch(rest);
    }

    private static bool IsInteractive(AutomationPeer peer)
    {
        if (InteractiveTypes.Contains(peer.GetAutomationControlType()))
        {
            return true;
        }

        return peer.GetProvider<IInvokeProvider>() is not null
            || peer.GetProvider<IToggleProvider>() is not null
            || peer.GetProvider<ISelectionItemProvider>() is not null
            || peer.GetProvider<IExpandCollapseProvider>() is not null
            || peer.GetProvider<IRangeValueProvider>() is not null
            || peer.GetProvider<IValueProvider>() is { IsReadOnly: false };
    }

    private static bool IsExempt(string? automationId, AutomationPeer? list) =>
        automationId == DuplicateExemption.AutomationId && list?.GetAutomationId() == DuplicateExemption.ListAutomationId;

    private static AutomationProblem Problem(string state, AutomationPeer peer, string? name, string rule) =>
        new(state, NullIfEmpty(peer.GetAutomationId()), peer.GetClassName(), peer.GetAutomationControlType(), name, rule);

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    [GeneratedRegex(@"^(Avalonia|VisualCommit|System|Microsoft)(\.[A-Za-z_][\w`]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex TypeNamePattern();

    [GeneratedRegex(@"\((?<inside>[^()]+)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingParenthesis();

    /// <summary>A key's word after a modifier: a letter or digit key, F1 to F24, or words such as <c>Forward Delete</c>.</summary>
    [GeneratedRegex(@"^([A-Za-z0-9]+|F\d{1,2}|Forward Delete)$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyWord();
}
