using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Evection.Detector;
using Evection.GUI.Services;

namespace Evection.GUI.Controls;

/// <summary>
/// A game's Steam artwork (cover, header or icon). Games without Steam art get a plain tile with their first letter.
/// </summary>
public sealed class GameImage : Border
{
    public static readonly StyledProperty<GameInfo?> GameProperty = AvaloniaProperty.Register<GameImage, GameInfo?>(nameof(Game));
    public static readonly StyledProperty<SteamArtKind> KindProperty = AvaloniaProperty.Register<GameImage, SteamArtKind>(nameof(Kind), SteamArtKind.Cover);

    private readonly Image image = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock letter = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeight.SemiBold,
    };
    private int loadVersion;

    public GameImage()
    {
        ClipToBounds = true;
        CornerRadius = new CornerRadius(6);
        this[!BackgroundProperty] = this.GetResourceObservable("BgMuted").ToBinding();
        letter[!TextBlock.ForegroundProperty] = this.GetResourceObservable("TextTertiary").ToBinding();
        Child = new Panel { Children = { letter, image } };
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public GameInfo? Game { get => GetValue(GameProperty); set => SetValue(GameProperty, value); }
    public SteamArtKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty || change.Property == KindProperty)
            _ = LoadAsync();
        else if (change.Property == BoundsProperty)
            letter.FontSize = Math.Max(10, Math.Min(Bounds.Width, Bounds.Height) * 0.4);
    }

    private async Task LoadAsync()
    {
        var version = ++loadVersion;
        image.Source = null;
        letter.Text = Game?.Name is { Length: > 0 } name ? name[..1].ToLowerInvariant() : "";
        if (Game?.SteamAppId is not { } appId)
            return;
        var bitmap = await ArtCache.GetAsync(appId, Kind);
        if (version != loadVersion)
            return;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            image.Source = bitmap;
            letter.IsVisible = bitmap == null;
        });
    }
}
