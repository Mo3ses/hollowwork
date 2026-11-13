using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Hollowwork.App.Controls;

public sealed class MinimapPreview : Control
{
    public static readonly StyledProperty<IBrush?> ExploredBrushProperty =
        AvaloniaProperty.Register<MinimapPreview, IBrush?>(nameof(ExploredBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<MinimapPreview, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> UnexploredBrushProperty =
        AvaloniaProperty.Register<MinimapPreview, IBrush?>(nameof(UnexploredBrush));

    static MinimapPreview()
    {
        AffectsRender<MinimapPreview>(ExploredBrushProperty, EdgeBrushProperty, UnexploredBrushProperty);
    }

    public IBrush? ExploredBrush
    {
        get => GetValue(ExploredBrushProperty);
        set => SetValue(ExploredBrushProperty, value);
    }

    public IBrush? EdgeBrush
    {
        get => GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    public IBrush? UnexploredBrush
    {
        get => GetValue(UnexploredBrushProperty);
        set => SetValue(UnexploredBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var terrain = new SolidColorBrush(Color.Parse("#1B2418"));
        context.FillRectangle(terrain, bounds);

        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#26301F")), 1);
        for (var x = 0d; x < bounds.Width; x += 16)
        {
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (var y = 0d; y < bounds.Height; y += 16)
        {
            context.DrawLine(gridPen, new Point(0, y), new Point(bounds.Width, y));
        }

        if (UnexploredBrush is { } unexplored)
        {
            context.FillRectangle(unexplored, bounds);
        }

        DrawArea(context, terrain, new Rect(12, 55, 62, 45), 4);
        DrawArea(context, terrain, new Rect(72, 72, 56, 14), 3);
        DrawArea(context, terrain, new Rect(126, 38, 58, 48), 4);
        DrawArea(context, terrain, new Rect(148, 84, 36, 12), 3);

        var player = new SolidColorBrush(Color.Parse("#E8E6D9"));
        context.DrawEllipse(player, null, new Point(43, 77), 3, 3);
    }

    private void DrawArea(DrawingContext context, IBrush terrain, Rect rect, double radius)
    {
        context.FillRectangle(terrain, rect, (float)radius);
        if (ExploredBrush is { } explored)
        {
            context.FillRectangle(explored, rect, (float)radius);
        }

        if (EdgeBrush is { } edge)
        {
            context.DrawRectangle(null, new Pen(edge, 2), rect, radius, radius);
        }
    }
}
