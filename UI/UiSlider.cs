using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Shigure;

/// <summary>
/// 暗色细轨 + 圆点拇指 + 中点标记的滑动条（样式贴近 Cursor 参考）。
/// </summary>
internal sealed class UiSlider : Control
{
    private int _minimum = 50;
    private int _maximum = 150;
    private int _value = 100;
    private bool _dragging;
    private bool _hovered;

    public UiSlider()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        ForeColor = UiTheme.Text;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(260, UiTheme.ActionButtonHeight);
        MinimumSize = new Size(80, UiTheme.ActionButtonHeight);
    }

    [DefaultValue(50)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
            {
                return;
            }

            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(150)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value)
            {
                return;
            }

            _maximum = Math.Max(value, _minimum);
            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    public int Value
    {
        get => _value;
        set
        {
            var next = Math.Clamp(value, _minimum, _maximum);
            if (_value == next)
            {
                return;
            }

            _value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled)
        {
            return;
        }

        Focus();
        _dragging = true;
        Capture = true;
        SetValueFromPointer(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SetValueFromPointer(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled)
        {
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value = _value - 1;
                e.Handled = true;
                break;
            case Keys.Right:
            case Keys.Up:
                Value = _value + 1;
                e.Handled = true;
                break;
            case Keys.Home:
                Value = _minimum;
                e.Handled = true;
                break;
            case Keys.End:
                Value = _maximum;
                e.Handled = true;
                break;
            case Keys.PageDown:
                Value = _value - 10;
                e.Handled = true;
                break;
            case Keys.PageUp:
                Value = _value + 10;
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (BackColor.A > 0)
        {
            using var clear = new SolidBrush(BackColor);
            graphics.FillRectangle(clear, ClientRectangle);
        }

        var scale = Math.Max(1f, DeviceDpi / 96f);
        var trackHeight = Math.Max(3, (int)Math.Round(4 * scale));
        var thumbDiameter = Math.Max(12, (int)Math.Round(14 * scale));
        var markDiameter = Math.Max(3, (int)Math.Round(4 * scale));
        var track = GetTrackBounds(thumbDiameter, trackHeight);
        var thumbCenter = GetThumbCenter(track, thumbDiameter);

        var trackColor = Enabled
            ? (_hovered || _dragging ? UiTheme.Hover : UiTheme.Border)
            : UiTheme.Surface;
        using (var trackBrush = new SolidBrush(trackColor))
        using (var trackPath = CreatePill(track))
        {
            graphics.FillPath(trackBrush, trackPath);
        }

        // 中点标记（默认 100 对应轨中央）。
        var midX = track.Left + track.Width / 2f;
        var midY = track.Top + track.Height / 2f;
        using (var markBrush = new SolidBrush(UiTheme.Background))
        {
            graphics.FillEllipse(
                markBrush,
                midX - markDiameter / 2f,
                midY - markDiameter / 2f,
                markDiameter,
                markDiameter);
        }

        var thumbColor = Enabled
            ? (_dragging || Focused ? UiTheme.Text : Color.FromArgb(230, UiTheme.Text))
            : UiTheme.Muted;
        using (var thumbBrush = new SolidBrush(thumbColor))
        {
            graphics.FillEllipse(
                thumbBrush,
                thumbCenter.X - thumbDiameter / 2f,
                thumbCenter.Y - thumbDiameter / 2f,
                thumbDiameter,
                thumbDiameter);
        }

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(ClientRectangle, -1, -1);
            ControlPaint.DrawFocusRectangle(graphics, focus, UiTheme.Text, BackColor);
        }
    }

    private void SetValueFromPointer(int x)
    {
        var scale = Math.Max(1f, DeviceDpi / 96f);
        var thumbDiameter = Math.Max(12, (int)Math.Round(14 * scale));
        var trackHeight = Math.Max(3, (int)Math.Round(4 * scale));
        var track = GetTrackBounds(thumbDiameter, trackHeight);
        if (track.Width <= 0)
        {
            return;
        }

        var ratio = Math.Clamp((x - track.Left) / (float)track.Width, 0f, 1f);
        var span = _maximum - _minimum;
        Value = _minimum + (int)Math.Round(ratio * span);
    }

    private Rectangle GetTrackBounds(int thumbDiameter, int trackHeight)
    {
        var horizontalPad = Math.Max(thumbDiameter / 2, 2);
        var y = Math.Max(0, (ClientSize.Height - trackHeight) / 2);
        var width = Math.Max(1, ClientSize.Width - horizontalPad * 2);
        return new Rectangle(horizontalPad, y, width, trackHeight);
    }

    private PointF GetThumbCenter(Rectangle track, int thumbDiameter)
    {
        var span = Math.Max(1, _maximum - _minimum);
        var ratio = (_value - _minimum) / (float)span;
        var x = track.Left + ratio * track.Width;
        var y = track.Top + track.Height / 2f;
        return new PointF(x, y);
    }

    private static GraphicsPath CreatePill(Rectangle bounds)
    {
        var path = new GraphicsPath();
        var diameter = bounds.Height;
        var radius = diameter / 2f;
        if (bounds.Width <= diameter)
        {
            path.AddEllipse(bounds.Left, bounds.Top, diameter, diameter);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }
}
