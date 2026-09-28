using System.Drawing;

namespace Shigure;

/// <summary>覆盖原生竖向滚动条的暗色外观，滚动位置仍由原控件管理。</summary>
internal sealed class UiDarkScrollBar : Control
{
    private int _total;
    private int _visibleCount;
    private int _value;
    private int _dragOffset = -1;
    private bool _hovered;

    public event Action<int>? ScrollRequested;

    public UiDarkScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.Surface;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    public void SetMetrics(int total, int visibleCount, int value)
    {
        total = Math.Max(0, total);
        visibleCount = Math.Max(1, visibleCount);
        value = Math.Clamp(value, 0, Math.Max(0, total - visibleCount));
        var scrollable = total > visibleCount;
        if (_total == total && _visibleCount == visibleCount && _value == value && Visible == scrollable)
        {
            return;
        }

        _total = total;
        _visibleCount = visibleCount;
        _value = value;
        Visible = scrollable;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(UiTheme.Surface);
        using var track = new SolidBrush(UiTheme.Field);
        e.Graphics.FillRectangle(track, 2, 0, Math.Max(0, Width - 4), Height);

        var thumb = ThumbBounds();
        using var fill = new SolidBrush(_hovered || _dragOffset >= 0 ? UiTheme.Text : UiTheme.Muted);
        e.Graphics.FillRectangle(fill, thumb);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var thumb = ThumbBounds();
        if (thumb.Contains(e.Location))
        {
            _dragOffset = e.Y - thumb.Top;
            Capture = true;
        }
        else
        {
            RequestScroll(_value + (e.Y < thumb.Top ? -_visibleCount : _visibleCount));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hovered = ThumbBounds().Contains(e.Location);
        if (_hovered != hovered)
        {
            _hovered = hovered;
            Invalidate();
        }

        if (_dragOffset < 0)
        {
            return;
        }

        var travel = Math.Max(1, Height - 4 - ThumbBounds().Height);
        var top = Math.Clamp(e.Y - _dragOffset - 2, 0, travel);
        var maximum = Math.Max(0, _total - _visibleCount);
        RequestScroll((int)Math.Round((double)top * maximum / travel));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragOffset = -1;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        RequestScroll(_value - Math.Sign(e.Delta) * 3);
    }

    private Rectangle ThumbBounds()
    {
        var trackHeight = Math.Max(1, Height - 4);
        var thumbHeight = Math.Clamp(
            (int)Math.Round((double)trackHeight * _visibleCount / Math.Max(1, _total)),
            Math.Min(28, trackHeight),
            trackHeight);
        var maximum = Math.Max(1, _total - _visibleCount);
        var top = 2 + (int)Math.Round((double)(trackHeight - thumbHeight) * _value / maximum);
        return new Rectangle(4, top, Math.Max(1, Width - 8), thumbHeight);
    }

    private void RequestScroll(int value)
    {
        value = Math.Clamp(value, 0, Math.Max(0, _total - _visibleCount));
        if (value != _value)
        {
            _value = value;
            Invalidate();
            ScrollRequested?.Invoke(value);
        }
    }
}
