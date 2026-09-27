using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Shigure;

/// <summary>
/// 配置页左侧职业/专精树：顶部可缩窄；职业行点击展开专精子行。
/// </summary>
internal sealed class ClassSpecTreeSidebar : Panel
{
    public const int ExpandedWidth = UiTheme.ConfigSidebarWidth;
    public const int CollapsedWidth = UiTheme.ConfigSidebarCollapsedWidth;
    private const int HeaderHeight = 36;
    private const int RowHeight = 36;
    private const int IconSize = 22;
    private const int CaretSize = 12;
    private const int SpecIndent = 22;

    private readonly ToolTip _toolTip = new();
    private readonly Button _collapseButton = new();
    private readonly ListBox _list = new();
    private readonly List<ClassNode> _classes = new();
    private readonly List<VisibleRow> _rows = new();
    private readonly HashSet<int> _expandedClassIds = new();
    private bool _collapsed;
    private bool _suppressSelection;
    private int? _selectedClassId;
    private int? _selectedSpecId;

    public ClassSpecTreeSidebar()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.SurfaceRaised;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _collapseButton.FlatStyle = FlatStyle.Flat;
        _collapseButton.FlatAppearance.BorderSize = 0;
        _collapseButton.FlatAppearance.MouseOverBackColor = UiTheme.Hover;
        _collapseButton.FlatAppearance.MouseDownBackColor = UiTheme.Pressed;
        _collapseButton.BackColor = Color.Transparent;
        _collapseButton.ForeColor = UiTheme.Muted;
        _collapseButton.Cursor = Cursors.Hand;
        _collapseButton.TabStop = false;
        _collapseButton.Text = string.Empty;
        _collapseButton.AccessibleName = "缩窄侧栏";
        _collapseButton.Click += (_, _) => SetCollapsed(!_collapsed, raiseEvent: true);
        _collapseButton.Paint += PaintCollapseButton;
        _toolTip.SetToolTip(_collapseButton, "缩窄/展开侧栏");

        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = UiTheme.SurfaceRaised;
        _list.ForeColor = UiTheme.Text;
        _list.IntegralHeight = false;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = RowHeight;
        _list.Dock = DockStyle.None;
        _list.HandleCreated += (_, _) =>
        {
            _list.ItemHeight = Math.Max(UiTheme.Scale(this, RowHeight), Font.Height + UiTheme.Scale(this, 10));
            RebuildVisibleRows();
        };
        _list.DrawItem += DrawRow;
        _list.MouseMove += OnListMouseMove;
        _list.MouseLeave += (_, _) =>
        {
            if (_hoveredIndex >= 0)
            {
                var previous = _hoveredIndex;
                _hoveredIndex = -1;
                InvalidateRow(previous);
            }
        };
        _list.MouseClick += OnListMouseClick;
        _list.SelectedIndexChanged += OnListSelectedIndexChanged;

        Controls.Add(_collapseButton);
        Controls.Add(_list);
        ApplyCollapsedVisuals(layoutOnly: true);
    }

    private int _hoveredIndex = -1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Collapsed => _collapsed;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredWidth => _collapsed ? CollapsedWidth : ExpandedWidth;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? SelectedClassId => _selectedClassId;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int? SelectedSpecId => _selectedSpecId;

    public event EventHandler? CollapseChanged;
    public event EventHandler? SelectionChanged;

    public void SetCollapsed(bool collapsed, bool raiseEvent = false)
    {
        if (_collapsed == collapsed)
        {
            return;
        }

        _collapsed = collapsed;
        ApplyCollapsedVisuals(layoutOnly: false);
        if (raiseEvent)
        {
            CollapseChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetClasses(IReadOnlyList<(int ClassId, string Name)> classes)
    {
        _suppressSelection = true;
        try
        {
            var previousExpanded = new HashSet<int>(_expandedClassIds);
            var previousClassId = _selectedClassId;
            var previousSpecId = _selectedSpecId;

            _classes.Clear();
            _expandedClassIds.Clear();
            foreach (var entry in classes)
            {
                _classes.Add(new ClassNode(entry.ClassId, entry.Name, []));
                if (previousExpanded.Contains(entry.ClassId))
                {
                    _expandedClassIds.Add(entry.ClassId);
                }
            }

            _selectedClassId = null;
            _selectedSpecId = null;
            RebuildVisibleRows();

            if (previousClassId is { } classId
                && _classes.Any(node => node.ClassId == classId))
            {
                _selectedClassId = classId;
                if (previousSpecId is { } specId)
                {
                    var node = _classes.First(item => item.ClassId == classId);
                    if (node.Specs.Any(spec => spec.SpecId == specId))
                    {
                        _selectedSpecId = specId;
                    }
                }

                SyncListSelection();
            }
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    public void SetSpecs(int classId, IReadOnlyList<(int SpecId, string Name)> specs)
    {
        var index = _classes.FindIndex(node => node.ClassId == classId);
        if (index < 0)
        {
            return;
        }

        var existing = _classes[index];
        _classes[index] = existing with
        {
            Specs = specs.Select(spec => new SpecNode(spec.SpecId, spec.Name)).ToList()
        };
        _expandedClassIds.Add(classId);
        RebuildVisibleRows();
        SyncListSelection();
    }

    public void ClearSpecs()
    {
        for (var i = 0; i < _classes.Count; i++)
        {
            _classes[i] = _classes[i] with { Specs = [] };
        }

        RebuildVisibleRows();
    }

    public void SelectClass(int classId, bool expand = true, bool raiseEvent = false)
    {
        if (_classes.All(node => node.ClassId != classId))
        {
            return;
        }

        if (expand)
        {
            _expandedClassIds.Add(classId);
        }

        var changed = _selectedClassId != classId || _selectedSpecId is not null;
        _selectedClassId = classId;
        _selectedSpecId = null;
        RebuildVisibleRows();
        SyncListSelection();
        if (raiseEvent && changed && !_suppressSelection)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectSpec(int classId, int specId, bool raiseEvent = false)
    {
        var node = _classes.FirstOrDefault(item => item.ClassId == classId);
        if (node is null || node.Specs.All(spec => spec.SpecId != specId))
        {
            return;
        }

        _expandedClassIds.Add(classId);
        var changed = _selectedClassId != classId || _selectedSpecId != specId;
        _selectedClassId = classId;
        _selectedSpecId = specId;
        RebuildVisibleRows();
        SyncListSelection();
        if (raiseEvent && changed && !_suppressSelection)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearSelection()
    {
        _selectedClassId = null;
        _selectedSpecId = null;
        SyncListSelection();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChildren();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _list.ItemHeight = Math.Max(UiTheme.Scale(this, RowHeight), Font.Height + UiTheme.Scale(this, 10));
        LayoutChildren();
        RebuildVisibleRows();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _list.ItemHeight = Math.Max(UiTheme.Scale(this, RowHeight), Font.Height + UiTheme.Scale(this, 10));
        LayoutChildren();
        RebuildVisibleRows();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ApplyCollapsedVisuals(bool layoutOnly)
    {
        Width = PreferredWidth;
        MinimumSize = new Size(PreferredWidth, 0);
        MaximumSize = new Size(PreferredWidth, int.MaxValue);
        _collapseButton.AccessibleName = _collapsed ? "展开侧栏" : "缩窄侧栏";
        _toolTip.SetToolTip(_collapseButton, _collapsed ? "展开侧栏" : "缩窄侧栏");
        _collapseButton.Invalidate();
        if (!layoutOnly)
        {
            RebuildVisibleRows();
        }

        LayoutChildren();
    }

    private void LayoutChildren()
    {
        var pad = UiTheme.Scale(this, 6);
        var header = UiTheme.Scale(this, HeaderHeight);
        var buttonSize = UiTheme.Scale(this, 28);
        _collapseButton.SetBounds(pad, Math.Max(0, (header - buttonSize) / 2), buttonSize, buttonSize);
        var listTop = header;
        _list.SetBounds(0, listTop, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height - listTop));
    }

    private void RebuildVisibleRows()
    {
        _rows.Clear();
        foreach (var node in _classes)
        {
            var expanded = _expandedClassIds.Contains(node.ClassId);
            _rows.Add(VisibleRow.Class(node.ClassId, node.Name, expanded));
            if (!expanded)
            {
                continue;
            }

            foreach (var spec in node.Specs)
            {
                _rows.Add(VisibleRow.Spec(node.ClassId, spec.SpecId, spec.Name));
            }
        }

        _suppressSelection = true;
        try
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in _rows)
            {
                _list.Items.Add(row);
            }

            _list.EndUpdate();
            SyncListSelection();
        }
        finally
        {
            _suppressSelection = false;
        }

        _list.Invalidate();
    }

    private void SyncListSelection()
    {
        var index = -1;
        if (_selectedSpecId is { } specId && _selectedClassId is { } classId)
        {
            index = _rows.FindIndex(row => row.IsSpec && row.ClassId == classId && row.SpecId == specId);
        }
        else if (_selectedClassId is { } onlyClassId)
        {
            index = _rows.FindIndex(row => !row.IsSpec && row.ClassId == onlyClassId);
        }

        if (_list.SelectedIndex != index)
        {
            _list.SelectedIndex = index;
        }
    }

    private void OnListSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressSelection || _list.SelectedIndex < 0 || _list.SelectedIndex >= _rows.Count)
        {
            return;
        }

        // 选择由 MouseClick 驱动，避免键盘焦点误触切换。
    }

    private void OnListMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var index = _list.IndexFromPoint(e.Location);
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        var row = _rows[index];
        if (!row.IsSpec)
        {
            var wasExpanded = _expandedClassIds.Contains(row.ClassId);
            if (wasExpanded)
            {
                _expandedClassIds.Remove(row.ClassId);
                RebuildVisibleRows();
                // 折叠不改变当前选中专精，避免右侧编辑区闪烁重载。
                SyncListSelection();
                return;
            }

            _expandedClassIds.Add(row.ClassId);
            RebuildVisibleRows();
            var node = _classes.First(item => item.ClassId == row.ClassId);
            if (node.Specs.Count > 0)
            {
                var preferSpec = _selectedClassId == row.ClassId && _selectedSpecId is { } currentSpec
                    && node.Specs.Any(spec => spec.SpecId == currentSpec)
                    ? currentSpec
                    : node.Specs[0].SpecId;
                ApplySelection(row.ClassId, preferSpec, raiseEvent: true);
            }
            else
            {
                ApplySelection(row.ClassId, null, raiseEvent: true);
            }

            return;
        }

        ApplySelection(row.ClassId, row.SpecId, raiseEvent: true);
    }

    private void ApplySelection(int classId, int? specId, bool raiseEvent)
    {
        var changed = _selectedClassId != classId || _selectedSpecId != specId;
        _selectedClassId = classId;
        _selectedSpecId = specId;
        if (specId is not null)
        {
            _expandedClassIds.Add(classId);
            RebuildVisibleRows();
        }
        else
        {
            SyncListSelection();
            _list.Invalidate();
        }

        if (raiseEvent && changed && !_suppressSelection)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnListMouseMove(object? sender, MouseEventArgs e)
    {
        var index = _list.IndexFromPoint(e.Location);
        if (index == _hoveredIndex)
        {
            return;
        }

        var previous = _hoveredIndex;
        _hoveredIndex = index;
        InvalidateRow(previous);
        InvalidateRow(_hoveredIndex);

        if (index >= 0 && index < _rows.Count)
        {
            var row = _rows[index];
            _toolTip.SetToolTip(_list, row.Name);
        }
        else
        {
            _toolTip.SetToolTip(_list, string.Empty);
        }
    }

    private void InvalidateRow(int index)
    {
        if (index < 0 || index >= _list.Items.Count)
        {
            return;
        }

        var bounds = _list.GetItemRectangle(index);
        _list.Invalidate(bounds);
    }

    private void PaintCollapseButton(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = _collapseButton.ClientRectangle;
        var fill = _collapseButton.ClientRectangle.Contains(
            _collapseButton.PointToClient(Cursor.Position))
            ? UiTheme.Hover
            : Color.Transparent;
        if (fill.A > 0)
        {
            using var brush = new SolidBrush(fill);
            g.FillRectangle(brush, bounds);
        }

        var iconSize = Math.Min(UiTheme.Scale(this, 16), Math.Min(bounds.Width, bounds.Height) - 8);
        if (iconSize <= 0)
        {
            return;
        }

        UiIconCatalog.Draw(
            g,
            "window-sidebar",
            new Rectangle(
                (bounds.Width - iconSize) / 2,
                (bounds.Height - iconSize) / 2,
                iconSize,
                iconSize),
            UiTheme.Muted);
    }

    private void DrawRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _rows.Count)
        {
            return;
        }

        var row = _rows[e.Index];
        var selected = row.IsSpec
            ? _selectedClassId == row.ClassId && _selectedSpecId == row.SpecId
            : _selectedClassId == row.ClassId && _selectedSpecId is null;
        var hovered = e.Index == _hoveredIndex;
        var background = selected
            ? UiTheme.Hover
            : hovered
                ? UiTheme.Field
                : UiTheme.SurfaceRaised;
        using (var brush = new SolidBrush(background))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        if (selected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            e.Graphics.FillRectangle(
                accent,
                e.Bounds.Left,
                e.Bounds.Top + 6,
                3,
                e.Bounds.Height - 12);
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var pad = UiTheme.Scale(this, 8);
        var caret = UiTheme.Scale(this, CaretSize);
        var icon = UiTheme.Scale(this, IconSize);
        var x = e.Bounds.Left + pad;
        if (row.IsSpec)
        {
            x += _collapsed ? UiTheme.Scale(this, 6) : UiTheme.Scale(this, SpecIndent);
        }
        else if (!_collapsed)
        {
            var caretBounds = new Rectangle(
                x,
                e.Bounds.Top + (e.Bounds.Height - caret) / 2,
                caret,
                caret);
            UiIconCatalog.Draw(
                g,
                row.Expanded ? "caret-down" : "caret-right",
                caretBounds,
                selected ? UiTheme.Text : UiTheme.Muted);
            x += caret + UiTheme.Scale(this, 4);
        }
        else
        {
            x = e.Bounds.Left + Math.Max(pad, (e.Bounds.Width - icon) / 2);
        }

        var iconBounds = new Rectangle(
            x,
            e.Bounds.Top + (e.Bounds.Height - icon) / 2,
            icon,
            icon);
        var image = row.IsSpec
            ? UiTheme.GetSpecIcon(row.ClassId, row.SpecId!.Value)
            : UiTheme.GetClassIcon(row.ClassId);
        if (image is not null)
        {
            g.DrawImage(image, iconBounds);
        }
        else
        {
            using var placeholder = new SolidBrush(UiTheme.Field);
            g.FillRectangle(placeholder, iconBounds);
        }

        if (_collapsed)
        {
            return;
        }

        var textLeft = iconBounds.Right + UiTheme.Scale(this, 8);
        var textBounds = new Rectangle(
            textLeft,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Right - textLeft - pad),
            e.Bounds.Height);
        TextRenderer.DrawText(
            g,
            row.Name,
            Font,
            textBounds,
            selected ? UiTheme.Text : UiTheme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private sealed record SpecNode(int SpecId, string Name);

    private sealed record ClassNode(int ClassId, string Name, IReadOnlyList<SpecNode> Specs);

    private sealed record VisibleRow(int ClassId, int? SpecId, string Name, bool Expanded)
    {
        public bool IsSpec => SpecId is not null;

        public static VisibleRow Class(int classId, string name, bool expanded)
            => new(classId, null, name, expanded);

        public static VisibleRow Spec(int classId, int specId, string name)
            => new(classId, specId, name, false);
    }
}
