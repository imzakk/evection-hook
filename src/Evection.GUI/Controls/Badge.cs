using Avalonia;
using Avalonia.Controls.Primitives;

namespace Evection.GUI.Controls;

/// <summary>The Badge component: a pill with an optional dot. Colour via classes: gray (default), blue, green, amber, red, inverse.</summary>
public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<Badge, bool>(nameof(ShowDot), true);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool ShowDot
    {
        get => GetValue(ShowDotProperty);
        set => SetValue(ShowDotProperty, value);
    }
}
