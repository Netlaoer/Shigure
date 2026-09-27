using System.Drawing;

namespace Shigure;

/// <summary>队友/敌人数量共用的分组列表筛选编辑器。</summary>
internal sealed class CountFilterEditorControl : UserControl
{
    private const string NumberColumn = "Number";
    private const string EnabledColumn = "Enabled";
    private const string FieldColumn = "Field";
    private const string ComparisonColumn = "Comparison";
    private const string ValueColumn = "Value";
    private const string DeleteColumn = "Delete";

    private static readonly ComparisonOption[] AllComparisons =
    [
        new("==", CountConditionComparisonKind.Equal),
        new("!=", CountConditionComparisonKind.NotEqual),
        new(">", CountConditionComparisonKind.GreaterThan),
        new("<", CountConditionComparisonKind.LessThan),
        new(">=", CountConditionComparisonKind.GreaterThanOrEqual),
        new("<=", CountConditionComparisonKind.LessThanOrEqual)
    ];

    private static readonly ComparisonOption[] EqualityComparisons = AllComparisons[..2];
    private static readonly ValueOption[] RoleValues =
    [
        new("坦克 (1)", 1),
        new("治疗 (2)", 2),
        new("输出 (3)", 3)
    ];
    private static readonly ValueOption[] DispelValues =
    [
        new("魔法 (1)", 1),
        new("诅咒 (2)", 2),
        new("疾病 (3)", 3),
        new("中毒 (4)", 4),
        new("激怒 (9)", 9),
        new("流血 (11)", 11)
    ];
    private static readonly ValueOption[] CombatValues =
    [
        new("战斗中", 1),
        new("不在战斗中", 0)
    ];

    private readonly IReadOnlyList<ConditionField> _allyAuras;
    private readonly IReadOnlyList<ConditionField> _enemyAuras;
    private readonly HashSet<string> _thresholdFields;
    private readonly FlowLayoutPanel _groupsPanel = new();
    private readonly List<GroupEditor> _groups = new();
    private bool _enemy;
    private bool _loading;

    public event EventHandler? Changed;

    public CountFilterEditorControl(
        IReadOnlyList<ConditionField> allyAuras,
        IReadOnlyList<ConditionField> enemyAuras,
        IReadOnlyList<string> thresholdFields)
    {
        _allyAuras = allyAuras;
        _enemyAuras = enemyAuras;
        _thresholdFields = new HashSet<string>(thresholdFields, StringComparer.Ordinal);
        Width = 800;
        Height = 780;
        Margin = new Padding(0);
        BackColor = Color.Transparent;

        _groupsPanel.Dock = DockStyle.Fill;
        _groupsPanel.FlowDirection = FlowDirection.TopDown;
        _groupsPanel.WrapContents = false;
        _groupsPanel.AutoScroll = true;
        _groupsPanel.BackColor = Color.Transparent;
        _groupsPanel.Padding = new Padding(0, 0, 4, 0);
        Controls.Add(_groupsPanel);

        var addGroupButton = UiTheme.CreateButton("添加条件组", UiTheme.ButtonKind.Secondary);
        UiTheme.StyleActionButton(addGroupButton, 140);
        addGroupButton.Size = new Size(140, 40);
        addGroupButton.Margin = new Padding(0, 8, 0, 8);
        addGroupButton.Click += (_, _) => AddGroup(CountConditionGroupMode.All, []);
        _groupsPanel.Controls.Add(addGroupButton);
        _groupsPanel.Resize += (_, _) => FitGroupWidths();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        FitGroupWidths();
    }

    private void FitGroupWidths()
    {
        var width = _groupsPanel.ClientSize.Width - _groupsPanel.Padding.Horizontal - 1;
        if (width < 200)
        {
            return;
        }

        foreach (var group in _groups)
        {
            if (group.Root.Width != width)
            {
                group.Root.Width = width;
            }
        }
    }

    public void SetEnemyTarget(bool enemy)
    {
        if (_enemy == enemy)
        {
            return;
        }

        _enemy = enemy;
        foreach (var group in _groups)
        {
            group.RefreshFieldOptions();
        }

        OnChanged();
    }

    public void LoadGroups(IReadOnlyList<ModuleCountConditionGroup>? groups)
    {
        _loading = true;
        try
        {
            foreach (var editor in _groups)
            {
                _groupsPanel.Controls.Remove(editor.Root);
                editor.Root.Dispose();
            }
            _groups.Clear();

            foreach (var group in groups ?? [])
            {
                AddGroup(group.Mode, group.Conditions ?? []);
            }

            if (_groups.Count == 0)
            {
                AddGroup(CountConditionGroupMode.All, []);
            }
        }
        finally
        {
            _loading = false;
            RenumberGroups();
            OnChanged();
        }
    }

    public List<ModuleCountConditionGroup> ReadGroups()
        => _groups.Select(group => group.Read()).ToList();

    public bool TryValidate(out string message)
    {
        foreach (var group in _groups)
        {
            if (!group.TryValidate(out message))
            {
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private void AddGroup(
        CountConditionGroupMode mode,
        IReadOnlyList<ModuleCountCondition> conditions)
    {
        var editor = new GroupEditor(this, mode);
        _groups.Add(editor);
        _groupsPanel.Controls.Add(editor.Root);
        _groupsPanel.Controls.SetChildIndex(editor.Root, Math.Max(0, _groupsPanel.Controls.Count - 2));
        foreach (var condition in conditions)
        {
            editor.AddCondition(condition);
        }

        RenumberGroups();
        OnChanged();
    }

    private void DeleteGroup(GroupEditor editor)
    {
        _groups.Remove(editor);
        _groupsPanel.Controls.Remove(editor.Root);
        editor.Root.Dispose();
        if (_groups.Count == 0)
        {
            AddGroup(CountConditionGroupMode.All, []);
        }

        RenumberGroups();
        OnChanged();
    }

    private void RenumberGroups()
    {
        for (var index = 0; index < _groups.Count; index++)
        {
            _groups[index].SetNumber(index + 1);
        }
    }

    private List<FieldOption> CreateFieldOptions(IEnumerable<long>? additionalAuraIds = null)
    {
        var options = _enemy
            ? new List<FieldOption>
            {
                new("生命值", CountConditionFieldKind.Health),
                new("距离", CountConditionFieldKind.Range),
                new("战斗", CountConditionFieldKind.Combat)
            }
            : new List<FieldOption>
            {
                new("生命值", CountConditionFieldKind.Health),
                new("治疗吸收", CountConditionFieldKind.HealingAbsorb),
                new("职责", CountConditionFieldKind.Role),
                new("驱散", CountConditionFieldKind.Dispel)
            };
        foreach (var aura in (_enemy ? _enemyAuras : _allyAuras))
        {
            if (TryReadAuraSpellId(aura, out var spellId)
                && options.All(option => option.AuraSpellId != spellId))
            {
                options.Add(new FieldOption(aura.DisplayName, CountConditionFieldKind.Aura, spellId));
            }
        }

        foreach (var spellId in additionalAuraIds ?? [])
        {
            if (spellId > 0 && options.All(option => option.AuraSpellId != spellId))
            {
                options.Add(new FieldOption($"未知光环 / {spellId}", CountConditionFieldKind.Aura, spellId));
            }
        }

        return options;
    }

    private static bool TryReadAuraSpellId(ConditionField field, out long spellId)
    {
        foreach (var part in field.Name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, out spellId) && spellId > 0)
            {
                return true;
            }
        }

        spellId = 0;
        return false;
    }

    private void OnChanged()
    {
        if (!_loading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class GroupEditor
    {
        private readonly CountFilterEditorControl _owner;
        private readonly Label _title = new();
        private readonly UiDropDown _modeBox = new();
        private readonly DataGridView _grid = new();
        private bool _updating;

        public Panel Root { get; }

        public GroupEditor(CountFilterEditorControl owner, CountConditionGroupMode mode)
        {
            _owner = owner;
            Root = new Panel
            {
                Width = 770,
                Height = 350,
                BackColor = UiTheme.SurfaceRaised,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(8)
            };

            var header = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
            _title.Bounds = new Rectangle(0, 4, 100, 34);
            _title.ForeColor = UiTheme.Text;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            header.Controls.Add(_title);

            UiTheme.StyleComboBox(_modeBox);
            _modeBox.Bounds = new Rectangle(108, 4, 180, 34);
            _modeBox.Items.AddRange([
                new GroupModeOption("全部满足", CountConditionGroupMode.All),
                new GroupModeOption("任一满足", CountConditionGroupMode.Any)
            ]);
            _modeBox.SelectedIndex = mode == CountConditionGroupMode.Any ? 1 : 0;
            _modeBox.SelectedIndexChanged += (_, _) => _owner.OnChanged();
            header.Controls.Add(_modeBox);

            var deleteGroupButton = UiTheme.CreateButton("删除组", UiTheme.ButtonKind.Danger);
            UiTheme.StyleActionButton(deleteGroupButton, 78);
            deleteGroupButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            deleteGroupButton.Bounds = new Rectangle(Root.Width - 94, 4, 78, 34);
            deleteGroupButton.Click += (_, _) => _owner.DeleteGroup(this);
            header.Controls.Add(deleteGroupButton);
            Root.Controls.Add(header);

            ConfigureGrid();
            _grid.Bounds = new Rectangle(8, 50, Root.Width - 16, 252);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Root.Controls.Add(_grid);

            var addButton = UiTheme.CreateButton("添加条件", UiTheme.ButtonKind.Secondary);
            UiTheme.StyleActionButton(addButton, 120);
            addButton.Bounds = new Rectangle(8, 310, 120, 36);
            addButton.Click += (_, _) => AddCondition(null);
            Root.Controls.Add(addButton);
        }

        public void SetNumber(int number) => _title.Text = $"条件组 {number}";

        public void AddCondition(ModuleCountCondition? condition)
        {
            var extraAuraIds = condition?.AuraSpellId is { } spellId ? new[] { spellId } : [];
            RefreshFieldOptions(extraAuraIds);
            var fieldKey = condition is null
                ? FieldOption.KeyFor(CountConditionFieldKind.Health, null)
                : FieldOption.KeyFor(condition.Field, condition.AuraSpellId);
            var rowIndex = _grid.Rows.Add(
                _grid.Rows.Count + 1,
                condition?.Enabled ?? true,
                fieldKey,
                condition?.Comparison ?? CountConditionComparisonKind.GreaterThan,
                string.Empty,
                string.Empty);
            var row = _grid.Rows[rowIndex];
            ConfigureRow(row, condition);
            RenumberRows();
            _owner.OnChanged();
        }

        public void RefreshFieldOptions(IEnumerable<long>? additionalAuraIds = null)
        {
            if (_grid.Columns[FieldColumn] is not DataGridViewComboBoxColumn column)
            {
                return;
            }

            var existingAuraIds = _grid.Rows.Cast<DataGridViewRow>()
                .Select(row => ParseFieldKey(row.Cells[FieldColumn].Value?.ToString()).AuraSpellId)
                .Where(id => id is > 0)
                .Select(id => id!.Value)
                .Concat(additionalAuraIds ?? [])
                .Distinct()
                .ToArray();
            column.DataSource = _owner.CreateFieldOptions(existingAuraIds);

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var option = ParseFieldKey(row.Cells[FieldColumn].Value?.ToString());
                var allowed = _owner.CreateFieldOptions(existingAuraIds)
                    .Any(candidate => candidate.Key == option.Key);
                if (!allowed)
                {
                    row.Cells[FieldColumn].Value = FieldOption.KeyFor(CountConditionFieldKind.Health, null);
                    ConfigureRow(row, null);
                }
            }
        }

        public ModuleCountConditionGroup Read()
        {
            _grid.EndEdit();
            return new ModuleCountConditionGroup
            {
                Mode = (_modeBox.SelectedItem as GroupModeOption)?.Mode ?? CountConditionGroupMode.All,
                Conditions = _grid.Rows.Cast<DataGridViewRow>()
                    .Select(TryReadCondition)
                    .Where(condition => condition is not null)
                    .Cast<ModuleCountCondition>()
                    .ToList()
            };
        }

        public bool TryValidate(out string message)
        {
            _grid.EndEdit();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var enabled = row.Cells[EnabledColumn].Value is bool value && value;
                if (!enabled)
                {
                    continue;
                }

                if (TryReadCondition(row) is null)
                {
                    message = $"{_title.Text}存在未填写完整的启用条件。";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        private void ConfigureGrid()
        {
            UiTheme.StyleDataGridView(_grid);
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = NumberColumn,
                HeaderText = "#",
                Width = 42,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = EnabledColumn,
                HeaderText = "启用",
                Width = 62,
                ValueType = typeof(bool),
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = FieldColumn,
                HeaderText = "字段",
                Width = 340,
                MinimumWidth = 340,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                DataSource = _owner.CreateFieldOptions(),
                DisplayMember = nameof(FieldOption.Text),
                ValueMember = nameof(FieldOption.Key),
                ValueType = typeof(string),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = ComparisonColumn,
                HeaderText = "判断",
                Width = 80,
                DataSource = AllComparisons.ToList(),
                DisplayMember = nameof(ComparisonOption.Text),
                ValueMember = nameof(ComparisonOption.Kind),
                ValueType = typeof(CountConditionComparisonKind),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ValueColumn,
                HeaderText = "值",
                Width = 190,
                MinimumWidth = 190,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = DeleteColumn,
                HeaderText = "删除",
                Text = "×",
                UseColumnTextForButtonValue = true,
                Width = 54,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            ApplyColumnLayout();
            // 通用主题会按 DPI 扩大最小列宽；主题处理后重新分配，值列吸收窗口拖宽后的剩余空间。
            _grid.HandleCreated += (_, _) => ApplyColumnLayout();

            _grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_grid.IsCurrentCellDirty)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.CellValueChanged += (_, e) =>
            {
                if (_updating || e.RowIndex < 0)
                {
                    return;
                }

                if (_grid.Columns[e.ColumnIndex].Name == FieldColumn)
                {
                    ConfigureRow(_grid.Rows[e.RowIndex], null);
                }
                _owner.OnChanged();
            };
            _grid.CellEndEdit += (_, _) => _owner.OnChanged();
            _grid.CellContentClick += (_, e) =>
            {
                if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != DeleteColumn)
                {
                    return;
                }

                _grid.Rows.RemoveAt(e.RowIndex);
                RenumberRows();
                _owner.OnChanged();
            };
            _grid.EditingControlShowing += (_, e) =>
            {
                if (e.Control is TextBox textBox
                    && _grid.CurrentCell?.OwningColumn?.Name == ValueColumn)
                {
                    textBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    var source = new AutoCompleteStringCollection();
                    source.AddRange(_owner._thresholdFields.ToArray());
                    textBox.AutoCompleteCustomSource = source;
                }
            };
            _grid.CellPainting += (_, e) =>
            {
                if (e.RowIndex >= 0
                    && e.ColumnIndex >= 0
                    && _grid.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewComboBoxCell)
                {
                    UiTheme.PaintDataGridViewComboBoxCell(_grid, e);
                }
            };
            _grid.DataError += (_, _) => { };
        }

        private void ConfigureRow(DataGridViewRow row, ModuleCountCondition? seed)
        {
            _updating = true;
            try
            {
                var option = ParseFieldKey(row.Cells[FieldColumn].Value?.ToString());
                var restricted = option.Kind is CountConditionFieldKind.Role
                    or CountConditionFieldKind.Dispel
                    or CountConditionFieldKind.Combat;
                var previousComparison = seed?.Comparison
                    ?? ReadComparison(row.Cells[ComparisonColumn].Value)
                    ?? CountConditionComparisonKind.Equal;
                var comparisonCell = new DataGridViewComboBoxCell
                {
                    DataSource = (restricted ? EqualityComparisons : AllComparisons).ToList(),
                    DisplayMember = nameof(ComparisonOption.Text),
                    ValueMember = nameof(ComparisonOption.Kind),
                    ValueType = typeof(CountConditionComparisonKind),
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                    FlatStyle = FlatStyle.Flat
                };
                row.Cells[ComparisonColumn] = comparisonCell;
                comparisonCell.Value = restricted && previousComparison is not (CountConditionComparisonKind.Equal
                    or CountConditionComparisonKind.NotEqual)
                        ? CountConditionComparisonKind.Equal
                        : previousComparison;

                object? previousValue = seed is null
                    ? null
                    : restricted
                        ? seed.Value
                        : FormatValue(seed);
                DataGridViewCell valueCell = option.Kind switch
                {
                    CountConditionFieldKind.Role => CreateValueComboCell(RoleValues),
                    CountConditionFieldKind.Dispel => CreateValueComboCell(DispelValues),
                    CountConditionFieldKind.Combat => CreateValueComboCell(CombatValues),
                    _ => new DataGridViewTextBoxCell()
                };
                row.Cells[ValueColumn] = valueCell;
                valueCell.Value = previousValue ?? DefaultValue(option.Kind);
            }
            finally
            {
                _updating = false;
            }
        }

        private ModuleCountCondition? TryReadCondition(DataGridViewRow row)
        {
            var option = ParseFieldKey(row.Cells[FieldColumn].Value?.ToString());
            if (option.Kind == CountConditionFieldKind.Aura && option.AuraSpellId is not > 0
                || ReadComparison(row.Cells[ComparisonColumn].Value) is not { } comparison)
            {
                return null;
            }

            var rawValue = row.Cells[ValueColumn].Value?.ToString()?.Trim() ?? string.Empty;
            var typedValue = TryReadInt(row.Cells[ValueColumn].Value, out var constant);
            var isStateField = !typedValue && _owner._thresholdFields.Contains(rawValue);
            if (!typedValue && !isStateField)
            {
                return null;
            }

            return new ModuleCountCondition
            {
                Enabled = row.Cells[EnabledColumn].Value is bool enabled && enabled,
                Field = option.Kind,
                AuraSpellId = option.AuraSpellId,
                Comparison = comparison,
                ValueKind = isStateField ? CountConditionValueKind.StateField : CountConditionValueKind.Constant,
                Value = typedValue ? constant : 0,
                ValueField = isStateField ? rawValue : null
            };
        }

        private void ApplyColumnLayout()
        {
            ConfigureFixedColumn(NumberColumn, 42);
            ConfigureFixedColumn(EnabledColumn, 84);
            ConfigureFixedColumn(FieldColumn, 340);
            ConfigureFixedColumn(ComparisonColumn, 144);
            ConfigureFillColumn(ValueColumn, 190);
            ConfigureFixedColumn(DeleteColumn, 84);

            _grid.ColumnHeadersHeight = Math.Max(38, _grid.Font.Height + 12);
            _grid.RowTemplate.Height = Math.Max(40, _grid.Font.Height + 14);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Height = _grid.RowTemplate.Height;
            }
        }

        private void ConfigureFixedColumn(string name, int width)
        {
            var column = _grid.Columns[name]
                ?? throw new InvalidOperationException($"找不到数量筛选列：{name}");
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = width;
            column.Width = width;
        }

        private void ConfigureFillColumn(string name, int minimumWidth)
        {
            var column = _grid.Columns[name]
                ?? throw new InvalidOperationException($"找不到数量筛选列：{name}");
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.MinimumWidth = minimumWidth;
            column.FillWeight = 100;
        }

        private void RenumberRows()
        {
            for (var index = 0; index < _grid.Rows.Count; index++)
            {
                _grid.Rows[index].Cells[NumberColumn].Value = index + 1;
            }
        }

        private static DataGridViewComboBoxCell CreateValueComboCell(ValueOption[] options)
            => new()
            {
                DataSource = options.ToList(),
                DisplayMember = nameof(ValueOption.Text),
                ValueMember = nameof(ValueOption.Value),
                ValueType = typeof(int),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat
            };

        private static string FormatValue(ModuleCountCondition condition)
            => condition.ValueKind == CountConditionValueKind.StateField
                ? condition.ValueField ?? string.Empty
                : condition.Value.ToString();

        private static object DefaultValue(CountConditionFieldKind kind)
            => kind switch
            {
                CountConditionFieldKind.Role => 1,
                CountConditionFieldKind.Dispel => 1,
                CountConditionFieldKind.Combat => 1,
                _ => "0"
            };

        private static bool TryReadInt(object? value, out int result)
        {
            if (value is int typed)
            {
                result = typed;
                return true;
            }

            return int.TryParse(value?.ToString(), out result);
        }

        private static CountConditionComparisonKind? ReadComparison(object? value)
        {
            if (value is CountConditionComparisonKind typed)
            {
                return typed;
            }

            return Enum.TryParse<CountConditionComparisonKind>(value?.ToString(), out var parsed)
                ? parsed
                : null;
        }

        private static FieldOption ParseFieldKey(string? key)
        {
            if (key?.StartsWith("aura:", StringComparison.Ordinal) == true
                && long.TryParse(key["aura:".Length..], out var spellId))
            {
                return new FieldOption(key, CountConditionFieldKind.Aura, spellId);
            }

            return Enum.TryParse<CountConditionFieldKind>(key, out var kind)
                ? new FieldOption(key ?? string.Empty, kind)
                : new FieldOption(string.Empty, CountConditionFieldKind.Health);
        }
    }

    private sealed record FieldOption(string Text, CountConditionFieldKind Kind, long? AuraSpellId = null)
    {
        public string Key => KeyFor(Kind, AuraSpellId);
        public static string KeyFor(CountConditionFieldKind kind, long? auraSpellId)
            => kind == CountConditionFieldKind.Aura ? $"aura:{auraSpellId.GetValueOrDefault()}" : kind.ToString();
        public override string ToString() => Text;
    }

    private sealed record ComparisonOption(string Text, CountConditionComparisonKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record GroupModeOption(string Text, CountConditionGroupMode Mode)
    {
        public override string ToString() => Text;
    }

    private sealed record ValueOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }
}
