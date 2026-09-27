using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Shigure;

/// <summary>
/// 顶部横向专精图标条：固定格宽，置于职业条正下方；图标尺寸与 ClassIconStrip 一致。
/// </summary>
internal sealed class SpecIconStrip : Panel
{
    public const int IconSize = UiTheme.ClassSpecIconSize;
    public const int CellSize = UiTheme.ClassSpecIconCellSize;
    public const int CellGap = 6;
    public const int StripPadding = 8;

    private readonly FlowLayoutPanel _flow;
    private readonly ToolTip _toolTip = new();
    private readonly List<SpecIconButton> _buttons = new();
    private int _selectedIndex = -1;
    private bool _suppressSelection;

    public SpecIconStrip()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        Padding = new Padding(0);
        ApplyScaledMetrics();

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(StripPadding)
        };
        Controls.Add(_flow);
    }

    public static int StripHeight => CellSize + (StripPadding * 2);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex => _selectedIndex;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public (int ClassId, int SpecId)? SelectedSpec
        => _selectedIndex >= 0 && _selectedIndex < _buttons.Count
            ? (_buttons[_selectedIndex].ClassId, _buttons[_selectedIndex].SpecId)
            : null;

    public event EventHandler? SelectionChanged;

    public void SetItems(IReadOnlyList<(int ClassId, int SpecId, string Name)> items)
    {
        _suppressSelection = true;
        try
        {
            _flow.SuspendLayout();
            _flow.Controls.Clear();
            _buttons.Clear();
            _selectedIndex = -1;

            var cell = ScaledCellSize();
            var gap = UiTheme.Scale(this, CellGap);
            var pad = UiTheme.Scale(this, StripPadding);
            _flow.Padding = new Padding(pad);

            for (var i = 0; i < items.Count; i++)
            {
                var (classId, specId, name) = items[i];
                var index = i;
                var button = new SpecIconButton(classId, specId, name)
                {
                    Margin = new Padding(0, 0, gap, 0),
                    Size = new Size(cell, cell)
                };
                button.Click += (_, _) => SelectIndex(index, raiseEvent: true);
                _toolTip.SetToolTip(button, name);
                _buttons.Add(button);
                _flow.Controls.Add(button);
            }

            ApplySelectionVisuals();
            _flow.ResumeLayout(true);
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    public void SelectIndex(int index, bool raiseEvent = false)
    {
        if (index < -1 || index >= _buttons.Count)
        {
            return;
        }

        var changed = _selectedIndex != index;
        _selectedIndex = index;
        ApplySelectionVisuals();

        if (raiseEvent && changed && !_suppressSelection)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectSpecId(int? specId, bool raiseEvent = false)
    {
        if (specId is null)
        {
            SelectIndex(-1, raiseEvent);
            return;
        }

        for (var i = 0; i < _buttons.Count; i++)
        {
            if (_buttons[i].SpecId == specId)
            {
                SelectIndex(i, raiseEvent);
                return;
            }
        }
    }

    public void Clear()
    {
        _suppressSelection = true;
        try
        {
            _flow.Controls.Clear();
            _buttons.Clear();
            _selectedIndex = -1;
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyScaledMetrics();
        RescaleButtons();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyScaledMetrics();
        RescaleButtons();
    }

    private void ApplyScaledMetrics()
    {
        var height = ScaledStripHeight();
        Height = height;
        MinimumSize = new Size(0, height);
        MaximumSize = new Size(int.MaxValue, height);
    }

    private void RescaleButtons()
    {
        var cell = ScaledCellSize();
        var gap = UiTheme.Scale(this, CellGap);
        var pad = UiTheme.Scale(this, StripPadding);
        _flow.Padding = new Padding(pad);
        foreach (var button in _buttons)
        {
            button.Size = new Size(cell, cell);
            button.Margin = new Padding(0, 0, gap, 0);
            button.Invalidate();
        }
    }

    private int ScaledCellSize() => UiTheme.Scale(this, CellSize);

    private int ScaledStripHeight()
        => ScaledCellSize() + (UiTheme.Scale(this, StripPadding) * 2);

    public int ScaledHeight => ScaledStripHeight();

    private void ApplySelectionVisuals()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Selected = i == _selectedIndex;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class SpecIconButton : Control
    {
        private bool _selected;
        private bool _hovered;

        public SpecIconButton(int classId, int specId, string name)
        {
            ClassId = classId;
            SpecId = specId;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = name;
        }

        public int ClassId { get; }
        public int SpecId { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                Invalidate();
            }
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var bounds = ClientRectangle;
            var fill = _selected
                ? UiTheme.Hover
                : _hovered
                    ? UiTheme.Field
                    : UiTheme.SurfaceRaised;
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            var drawnIconSize = Math.Min(
                UiTheme.Scale(this, IconSize),
                bounds.Height - UiTheme.Scale(this, 12));
            drawnIconSize = Math.Max(8, drawnIconSize);

            if (_selected || _hovered)
            {
                using var indicator = new SolidBrush(_selected ? Color.White : UiTheme.Muted);
                var indicatorInset = Math.Max(6, (bounds.Height - drawnIconSize) / 2);
                g.FillRectangle(indicator, 0, indicatorInset, 3, bounds.Height - indicatorInset * 2);
            }

            var iconBounds = new Rectangle(
                (bounds.Width - drawnIconSize) / 2,
                (bounds.Height - drawnIconSize) / 2,
                drawnIconSize,
                drawnIconSize);

            var icon = UiTheme.GetSpecIcon(ClassId, SpecId);
            if (icon is not null)
            {
                g.DrawImage(icon, iconBounds);
            }
            else
            {
                using var placeholder = new SolidBrush(UiTheme.Field);
                g.FillRectangle(placeholder, iconBounds);
                TextRenderer.DrawText(
                    g,
                    "?",
                    Font,
                    iconBounds,
                    _selected ? UiTheme.Text : UiTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using var border = new Pen(_selected ? UiTheme.Accent : UiTheme.Border);
            g.DrawRectangle(
                border,
                iconBounds.X,
                iconBounds.Y,
                iconBounds.Width - 1,
                iconBounds.Height - 1);
        }
    }
}
