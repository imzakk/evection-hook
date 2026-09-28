using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Evection.GUI.Controls;

/// <summary>The starry sky from the Evection UI cover: small white dots that slowly twinkle and drift with the mouse.</summary>
public sealed class StarField : Control
{
    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<StarField, int>(nameof(Count), 170);
    public static readonly StyledProperty<bool> AnimateProperty = AvaloniaProperty.Register<StarField, bool>(nameof(Animate), true);
    public static readonly StyledProperty<Vector> ParallaxProperty = AvaloniaProperty.Register<StarField, Vector>(nameof(Parallax));
    public static readonly StyledProperty<double> RevealProperty = AvaloniaProperty.Register<StarField, double>(nameof(Reveal), 1);

    private readonly record struct Star(double X, double Y, double Size, double Brightness, double Phase, double Speed, double Depth);

    private Star[] stars = [];
    private TimeSpan time;
    private TimeSpan? lastFrame;
    private bool running;

    static StarField()
    {
        AffectsRender<StarField>(ParallaxProperty, RevealProperty);
    }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public bool Animate { get => GetValue(AnimateProperty); set => SetValue(AnimateProperty, value); }
    public Vector Parallax { get => GetValue(ParallaxProperty); set => SetValue(ParallaxProperty, value); }

    /// <summary>0..1 fade-in of the whole field.</summary>
    public double Reveal { get => GetValue(RevealProperty); set => SetValue(RevealProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var random = new Random(1229490);
        stars = Enumerable.Range(0, Count).Select(_ => new Star(
            random.NextDouble(), random.NextDouble() * 0.78,
            random.NextDouble() < 0.28 ? 2.2 : 1.5,
            0.35 + random.NextDouble() * 0.65,
            random.NextDouble() * Math.PI * 2,
            0.4 + random.NextDouble() * 1.4,
            0.3 + random.NextDouble() * 0.7)).ToArray();
        running = true;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        running = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestFrame()
    {
        if (!running || !Animate)
            return;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(t =>
        {
            if (lastFrame is { } last)
                time += t - last;
            lastFrame = t;
            InvalidateVisual();
            RequestFrame();
        });
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var seconds = time.TotalSeconds;
        foreach (var star in stars)
        {
            var twinkle = Animate ? 0.65 + 0.35 * Math.Sin(seconds * star.Speed + star.Phase) : 1;
            var alpha = star.Brightness * twinkle * Reveal;
            if (alpha < 0.02)
                continue;
            var x = star.X * w + Parallax.X * star.Depth;
            var y = star.Y * h + Parallax.Y * star.Depth;
            var brush = new SolidColorBrush(Colors.White, alpha);
            context.DrawEllipse(brush, null, new Point(x, y), star.Size / 2, star.Size / 2);
        }
    }
}
