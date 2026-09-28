using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Evection.GUI.ViewModels;

namespace Evection.GUI.Views;

public sealed partial class CoverView : UserControl
{
    /// <summary>Set by the screenshot tool to render the finished state without waiting for animations.</summary>
    public static bool SkipAnimations { get; set; }

    private bool leaving;

    public CoverView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            var reduce = SkipAnimations || (DataContext as CoverViewModel)?.ReduceMotion == true;
            Stars.Animate = !reduce;
            if (reduce)
                DisableTransitions(this);
            // Let the first frame render in the start state, then transition to the shown state.
            DispatcherTimer.RunOnce(() => Classes.Add("shown"), TimeSpan.FromMilliseconds(reduce ? 0 : 60));
            Focus();
        };
        SizeChanged += (_, e) => LayoutMoon(e.NewSize);
        PointerMoved += OnPointerMoved;
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
                OnStart(this, new RoutedEventArgs());
        };
    }

    private void LayoutMoon(Size size)
    {
        // The dome is ~82% of the image width; show its top ~45% above the bottom edge, like the Figma cover.
        var width = Math.Max(size.Width * 1.02, 900);
        MoonRise.Width = width;
        MoonRise.Height = width * 810 / 1920;
        MoonRise.Margin = new Thickness(0, 0, 0, -MoonRise.Height * 0.38);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!Stars.Animate)
            return;
        var p = e.GetPosition(this);
        var dx = (p.X / Math.Max(1, Bounds.Width)) - 0.5;
        var dy = (p.Y / Math.Max(1, Bounds.Height)) - 0.5;
        Stars.Parallax = new Vector(-dx * 18, -dy * 12);
        MoonParallax.RenderTransform = new TranslateTransform(-dx * 10, -dy * 6);
    }

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (leaving)
            return;
        leaving = true;
        Classes.Add("leaving");
        if (!SkipAnimations && (DataContext as CoverViewModel)?.ReduceMotion != true)
            await Task.Delay(420);
        (DataContext as CoverViewModel)?.GetStartedCommand.Execute(null);
    }

    private static void DisableTransitions(Visual root)
    {
        foreach (var v in root.GetSelfAndVisualDescendantsSafe())
        {
            if (v is Avalonia.Animation.Animatable a)
                a.Transitions = null;
        }
    }
}

internal static class VisualTreeExtensions
{
    public static IEnumerable<Visual> GetSelfAndVisualDescendantsSafe(this Visual root)
    {
        yield return root;
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
            yield return child;
    }
}
