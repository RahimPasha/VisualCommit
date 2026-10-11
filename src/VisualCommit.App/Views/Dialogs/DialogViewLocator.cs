using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// Finds the view of a dialog's (or a slot's) view model by its type (docs/phase-3-design.md 4f),
/// so that no package adds templates to a shared XAML file for its dialogs. A view registered
/// with <see cref="Register{TViewModel}"/> wins; otherwise the view is found by name: the view
/// model's full name with <c>.ViewModels.</c> made <c>.Views.</c> and <c>ViewModel</c> at the
/// end made <c>View</c> (<c>ViewModels.Dialogs.ConfirmationViewModel</c> →
/// <c>Views.Dialogs.ConfirmationView</c>). A view model without a view shows a line that says so.
/// </summary>
public sealed class DialogViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, Func<Control>> Registered = new();
    private static readonly ConcurrentDictionary<Type, Type?> ByName = new();

    /// <summary>Registers the view that shows a view model of type <typeparamref name="TViewModel"/>.</summary>
    public static void Register<TViewModel>(Func<Control> createView)
    {
        ArgumentNullException.ThrowIfNull(createView);
        Registered[typeof(TViewModel)] = createView;
    }

    public bool Match(object? data) => data is not null && FindFactory(data.GetType()) is not null;

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var factory = FindFactory(param.GetType());
        return factory is null
            ? new TextBlock { Text = $"No view for {param.GetType().Name}." }
            : factory();
    }

    private static Func<Control>? FindFactory(Type viewModelType)
    {
        for (var type = viewModelType; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (Registered.TryGetValue(type, out var registered))
            {
                return registered;
            }

            if (ByName.GetOrAdd(type, FindViewByName) is { } view)
            {
                return () => (Control)Activator.CreateInstance(view)!;
            }
        }

        return null;
    }

    private static Type? FindViewByName(Type viewModelType)
    {
        var name = viewModelType.FullName;
        if (name is null || !name.Contains(".ViewModels.", StringComparison.Ordinal) || !name.EndsWith("ViewModel", StringComparison.Ordinal))
        {
            return null;
        }

        var viewName = name.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)[..^"Model".Length];
        var view = viewModelType.Assembly.GetType(viewName);
        return view is not null && typeof(Control).IsAssignableFrom(view) && view.GetConstructor(Type.EmptyTypes) is not null ? view : null;
    }
}
