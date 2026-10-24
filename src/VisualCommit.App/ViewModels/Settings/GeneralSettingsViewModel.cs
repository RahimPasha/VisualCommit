using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Settings;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The settings screen's General section (docs/phase-6-design.md 7.3): the theme, auto-fetch's
/// minutes, Keep ignored files, the stash's untracked files and the extensions Open file asks
/// about. Its fields start from the settings as they are; nothing is applied until Save
/// (<see cref="Apply"/>, 7.8), and the theme only through the shell's <c>UseTheme</c>. An error
/// shows once its box has been edited (D114) or after a Save (<see cref="ShowErrors"/>).
/// </summary>
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    /// <summary>The minutes the box shows while auto-fetch is off (7.3).</summary>
    public const int DefaultMinutes = 5;

    /// <summary>The shortest and longest interval the screen accepts (7.3).</summary>
    public const int MinMinutes = 1;

    public const int MaxMinutes = 60;

    private readonly AppSettings _start;
    private bool _minutesEdited;
    private bool _extensionsEdited;
    private bool _showErrors;

    /// <param name="settings">The settings as they are when the screen opens.</param>
    /// <param name="theme">The theme as chosen now (the shell's <c>Theme</c>).</param>
    /// <param name="pathExtensions">The <c>PATHEXT</c> value of the session's catalogues (12.9), or null off Windows.</param>
    public GeneralSettingsViewModel(AppSettings settings, AppTheme theme, string? pathExtensions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _start = settings;
        StartTheme = theme;
        Theme = theme;

        // From the file (7.3): 0 or less unchecked at 5; 1 to 60 checked with it; above 60 checked
        // with it and no error until the box is edited, and Save keeps it.
        IsAutoFetchOn = settings.AutoFetchMinutes > 0;
        MinutesText = (settings.AutoFetchMinutes > 0 ? settings.AutoFetchMinutes : DefaultMinutes).ToString(CultureInfo.InvariantCulture);
        KeepIgnoredFiles = settings.KeepIgnoredFiles;
        StashIncludeUntracked = settings.StashIncludeUntracked;
        ExtraExtensionsText = settings.ExtraRunnableExtensions;

        RunnableList = string.Join(' ', RepositoryOperations.FixedRunnableExtensions);
        var fromPathExt = RepositoryOperations.PathExtExtensions(pathExtensions)
            .Where(extension => !RepositoryOperations.FixedRunnableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            .ToList();
        PathExtText = fromPathExt.Count == 0 ? null : Texts.FromPathExt(string.Join(' ', fromPathExt));

        // The constructor's own sets are no edits.
        _minutesEdited = false;
        _extensionsEdited = false;
    }

    /// <summary>The theme as chosen when the screen opened.</summary>
    public AppTheme StartTheme { get; }

    /// <summary>The theme radio checked now (<c>SettingsThemeDark</c>, <c>SettingsThemeLight</c>, <c>SettingsThemeSystem</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDark), nameof(IsLight), nameof(IsSystem))]
    public partial AppTheme Theme { get; set; }

    public bool IsDark
    {
        get => Theme == AppTheme.Dark;
        set
        {
            if (value)
            {
                Theme = AppTheme.Dark;
            }
        }
    }

    public bool IsLight
    {
        get => Theme == AppTheme.Light;
        set
        {
            if (value)
            {
                Theme = AppTheme.Light;
            }
        }
    }

    public bool IsSystem
    {
        get => Theme == AppTheme.System;
        set
        {
            if (value)
            {
                Theme = AppTheme.System;
            }
        }
    }

    /// <summary><c>AutoFetchCheckBox</c>: unchecked, the box is disabled and keeps its number (7.3); no edit of the box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoFetchError), nameof(HasError))]
    public partial bool IsAutoFetchOn { get; set; }

    /// <summary><c>AutoFetchMinutesBox</c>'s text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoFetchError), nameof(HasError))]
    public partial string MinutesText { get; set; } = string.Empty;

    /// <summary><c>KeepIgnoredCheckBox</c> (D193).</summary>
    [ObservableProperty]
    public partial bool KeepIgnoredFiles { get; set; }

    /// <summary><c>StashUntrackedCheckBox</c>.</summary>
    [ObservableProperty]
    public partial bool StashIncludeUntracked { get; set; }

    /// <summary><c>RunnableExtraBox</c>'s text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunnableError), nameof(HasError))]
    public partial string ExtraExtensionsText { get; set; } = string.Empty;

    /// <summary><c>RunnableList</c>: the fixed list, space-separated, in its order (12.8).</summary>
    public string RunnableList { get; }

    /// <summary>The fixed list's extensions, as <c>RunnableList</c> draws them, a word each.</summary>
    public IReadOnlyList<string> RunnableExtensions => RepositoryOperations.FixedRunnableExtensions;

    /// <summary><c>RunnablePathExt</c>: <c>From PATHEXT: {extensions}</c>, lower case, only those the list lacks; null hides it (none, or off Windows).</summary>
    public string? PathExtText { get; }

    public bool ShowsPathExt => PathExtText is not null;

    /// <summary><c>AutoFetchError</c>, or null; shown once the box was edited or after a Save.</summary>
    public string? AutoFetchError => (_minutesEdited || _showErrors) && MinutesError() is { } error ? error : null;

    /// <summary><c>RunnableError</c>, or null; shown once the box was edited or after a Save.</summary>
    public string? RunnableError => (_extensionsEdited || _showErrors) && ExtensionsError(ExtraExtensionsText) is { } error ? error : null;

    /// <summary>The section shows an error (<c>SettingsNavError</c> after a Save, SD9).</summary>
    public bool HasError => AutoFetchError is not null || RunnableError is not null;

    /// <summary>The first field with an error, by automation id, for the focus after a refused Save; null for none.</summary>
    public string? FirstErrorField => MinutesError() is not null ? "AutoFetchMinutesBox" : ExtensionsError(ExtraExtensionsText) is not null ? "RunnableExtraBox" : null;

    /// <summary>Shows every error of the section, edited or not (7.8 step 1); returns whether there is one.</summary>
    public bool ShowErrors()
    {
        _showErrors = true;
        OnPropertyChanged(nameof(AutoFetchError));
        OnPropertyChanged(nameof(RunnableError));
        OnPropertyChanged(nameof(HasError));
        return FirstErrorField is not null;
    }

    /// <summary>
    /// The settings with this section's changes (7.8 step 2), but the theme, which Save applies
    /// through <c>UseTheme</c>; call it only when there is no error.
    /// </summary>
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            AutoFetchMinutes = SavedMinutes(),
            KeepIgnoredFiles = KeepIgnoredFiles,
            StashIncludeUntracked = StashIncludeUntracked,
            ExtraRunnableExtensions = NormalizeExtensions(ExtraExtensionsText) ?? settings.ExtraRunnableExtensions,
        };
    }

    /// <summary>
    /// The extensions as stored (7.3): each entry of <paramref name="text"/> as
    /// <see cref="RunnableExtensionText.Parse"/> reads it (split at spaces, commas, semicolons and
    /// tabs, lower-cased with its dot), one space apart, duplicates and the fixed list's left out;
    /// null when an entry is not an extension.
    /// </summary>
    public static string? NormalizeExtensions(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (ExtensionsError(text) is not null)
        {
            return null;
        }

        return string.Join(' ', RunnableExtensionText.Parse(text)
            .Where(extension => !RepositoryOperations.FixedRunnableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>The first entry of <paramref name="text"/> that is not an extension, as <c>RunnableError</c> says it; null when all are.</summary>
    public static string? ExtensionsError(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return RunnableExtensionText.FirstInvalid(text) is { } bad ? Texts.NotAnExtension(bad) : null;
    }

    partial void OnMinutesTextChanged(string value) => _minutesEdited = true;

    partial void OnExtraExtensionsTextChanged(string value) => _extensionsEdited = true;

    /// <summary>The minutes box's error while auto-fetch is on, or null; an unedited value above 60 from the file is kept (7.3).</summary>
    private string? MinutesError()
    {
        if (!IsAutoFetchOn)
        {
            return null;
        }

        if (!_minutesEdited && _start.AutoFetchMinutes > MaxMinutes)
        {
            return null;
        }

        return int.TryParse(MinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= MinMinutes and <= MaxMinutes
            ? null
            : Texts.AutoFetchError;
    }

    private int SavedMinutes()
    {
        if (!IsAutoFetchOn)
        {
            return 0;
        }

        if (!_minutesEdited && _start.AutoFetchMinutes > MaxMinutes)
        {
            return _start.AutoFetchMinutes;
        }

        return int.Parse(MinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
