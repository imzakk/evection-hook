using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Evection.GUI.Controls;

/// <summary>Draws an icon from <see cref="Icons"/> in the inherited foreground colour, like text.</summary>
public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(Kind));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Size), 16);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    static Icon()
    {
        AffectsRender<Icon>(KindProperty, ForegroundProperty, SizeProperty);
        AffectsMeasure<Icon>(SizeProperty);
    }

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var def = Icons.Get(Kind);
        if (def == null || Foreground == null)
            return;

        var scale = Size / def.ViewBox;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (def.Filled)
                context.DrawGeometry(Foreground, null, def.Geometry);
            else
                context.DrawGeometry(null, new Pen(Foreground, def.StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), def.Geometry);
        }
    }
}
