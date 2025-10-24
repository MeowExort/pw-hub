using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Draws an insertion marker (horizontal line) at the top or bottom edge of a TreeViewItem during drag.
/// </summary>
public sealed class InsertionAdorner : Adorner
{
    private readonly Pen _pen;
    private readonly Pen _shadowPen;
    private readonly double _margin; // indent from left to align under avatar

    public bool PositionAbove { get; set; }

    public InsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        // Accent color from app resources if available
        var brush = TryFindResource("AccentHighlightBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF));
        var shadow = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
        _pen = new Pen(brush, 2.0);
        _pen.Freeze();
        _shadowPen = new Pen(shadow, 4.0);
        _shadowPen.Freeze();
        _margin = 24 + 32 + 10; // squad left indent + avatar size + spacing (approx)
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        // Y position: top or bottom edge (inside bounds)
        var y = PositionAbove ? 0.5 : height - 0.5;
        var start = new Point(_margin, y);
        var end = new Point(width - 8, y);

        // soft shadow line for better visibility on light backgrounds
        dc.DrawLine(_shadowPen, new Point(start.X, y + (PositionAbove ? 1 : -1)), new Point(end.X, y + (PositionAbove ? 1 : -1)));
        // main line
        dc.DrawLine(_pen, start, end);

        // draw small bracket at left for clarity
        var bracketSize = 6;
        var bY1 = PositionAbove ? y + 1 : y - 1;
        dc.DrawLine(_pen, new Point(start.X, bY1), new Point(start.X, bY1 + (PositionAbove ? bracketSize : -bracketSize)));
    }
}
