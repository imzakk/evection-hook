using Avalonia.Data.Converters;
using Avalonia.Media;
using Evection.GUI.Services;

namespace Evection.GUI.Views;

public static class Converters
{
    /// <summary>True when the value's string equals the converter parameter (sidebar highlighting, tab matching).</summary>
    public static readonly IValueConverter IsEqual = new FuncValueConverter<object?, object?, bool>((value, parameter) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal));

    public static readonly IValueConverter SignInLabel = new FuncValueConverter<bool, string>(signedIn => signedIn ? "account" : "sign in");

    public static readonly IValueConverter ToastIconBrush = new FuncValueConverter<ToastKind, IBrush?>(kind =>
    {
        var key = kind switch
        {
            ToastKind.Warning or ToastKind.Error => "ToastWarning",
            _ => "TextInverse",
        };
        return Avalonia.Application.Current!.TryGetResource(key, Avalonia.Application.Current.ActualThemeVariant, out var brush) ? brush as IBrush : null;
    });

    /// <summary>Height for a Steam library cover (600×900) at the given width.</summary>
    public static readonly IValueConverter CoverHeight = new FuncValueConverter<double, double>(width => width > 0 ? width * 1.5 : 150);

    public static readonly IValueConverter NotZero = new FuncValueConverter<int, bool>(n => n != 0);
    public static readonly IValueConverter IsZero = new FuncValueConverter<int, bool>(n => n == 0);
}
