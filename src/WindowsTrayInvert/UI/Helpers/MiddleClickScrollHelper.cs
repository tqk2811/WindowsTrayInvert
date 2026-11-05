using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsTrayInvert.UI.Helpers;

/// <summary>
/// Browser-style auto scroll for every window of the app: press the middle button over a vertically scrollable
/// area, then move the mouse up/down; the farther from the anchor, the faster it scrolls.
/// <list type="bullet">
/// <item>Hold and drag: stops when the middle button is released.</item>
/// <item>Quick click without moving: stays on until any click, Escape, or focus loss.</item>
/// </list>
/// </summary>
public sealed class MiddleClickScrollHelper
{
    private const double DeadZone = 12;
    private const double SpeedFactor = 0.12;
    private static readonly TimeSpan StickyClickTime = TimeSpan.FromMilliseconds(300);

    private readonly ScrollViewer _viewer;
    private readonly Point _anchor;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly DispatcherTimer _timer;
    private readonly AnchorAdorner? _adorner;
    private bool _moved;

    private MiddleClickScrollHelper(ScrollViewer viewer, Point anchor)
    {
        _viewer = viewer;
        _anchor = anchor;

        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(viewer);
        if (layer is not null)
        {
            _adorner = new AnchorAdorner(viewer, anchor);
            layer.Add(_adorner);
        }

        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
        _timer.Tick += (_, _) => Step();

        viewer.PreviewMouseDown += OnPreviewMouseDown;
        viewer.PreviewMouseUp += OnPreviewMouseUp;
        viewer.PreviewKeyDown += OnPreviewKeyDown;
        viewer.LostMouseCapture += OnLostMouseCapture;
        viewer.Cursor = Cursors.ScrollNS;
        viewer.CaptureMouse();
        _timer.Start();
    }

    /// <summary>Call once at startup.</summary>
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler(OnWindowPreviewMouseDown));
    }

    private static void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || e.Handled || Mouse.Captured is not null)
            return;

        ScrollViewer? viewer = FindScrollableAncestor(e.OriginalSource as DependencyObject);
        if (viewer is null)
            return;

        _ = new MiddleClickScrollHelper(viewer, e.GetPosition(viewer));
        e.Handled = true;
    }

    /// <summary>Nearest ancestor ScrollViewer that can actually scroll vertically (skips e.g. TextBox internals).</summary>
    private static ScrollViewer? FindScrollableAncestor(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is ScrollViewer viewer && viewer.ScrollableHeight > 0)
                return viewer;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private void Step()
    {
        double dy = Mouse.GetPosition(_viewer).Y - _anchor.Y;
        double distance = Math.Abs(dy) - DeadZone;
        if (distance <= 0)
            return;

        _moved = true;
        // Grows faster than linear so small moves give fine control and big moves scroll quickly.
        double delta = Math.Sign(dy) * (distance + distance * distance * 0.02) * SpeedFactor;
        _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset + delta);
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Any click while in sticky mode ends it and is swallowed (like browsers).
        e.Handled = true;
        Stop();
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;
        e.Handled = true;
        bool quickClick = !_moved && DateTime.UtcNow - _startTime < StickyClickTime;
        if (!quickClick)
            Stop();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Stop();
        }
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e) => Stop();

    private void Stop()
    {
        if (!_timer.IsEnabled)
            return;
        _timer.Stop();
        _viewer.PreviewMouseDown -= OnPreviewMouseDown;
        _viewer.PreviewMouseUp -= OnPreviewMouseUp;
        _viewer.PreviewKeyDown -= OnPreviewKeyDown;
        _viewer.LostMouseCapture -= OnLostMouseCapture;
        _viewer.ClearValue(FrameworkElement.CursorProperty);
        if (_adorner is not null)
            AdornerLayer.GetAdornerLayer(_viewer)?.Remove(_adorner);
        if (_viewer.IsMouseCaptured)
            _viewer.ReleaseMouseCapture();
    }

    /// <summary>The round up/down marker drawn at the anchor point.</summary>
    private sealed class AnchorAdorner : Adorner
    {
        private const double Radius = 14;
        private readonly Point _center;

        public AnchorAdorner(UIElement element, Point center) : base(element)
        {
            _center = center;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            Brush fill = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
            Brush ink = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
            dc.DrawEllipse(fill, new Pen(ink, 1), _center, Radius, Radius);
            dc.DrawEllipse(ink, null, _center, 1.6, 1.6);
            DrawTriangle(dc, ink, -1);
            DrawTriangle(dc, ink, 1);
        }

        private void DrawTriangle(DrawingContext dc, Brush ink, int direction)
        {
            double tipY = _center.Y + direction * (Radius - 3);
            double baseY = _center.Y + direction * (Radius - 8);
            StreamGeometry geometry = new();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(_center.X, tipY), true, true);
                ctx.LineTo(new Point(_center.X - 4.5, baseY), true, false);
                ctx.LineTo(new Point(_center.X + 4.5, baseY), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(ink, null, geometry);
        }
    }
}
