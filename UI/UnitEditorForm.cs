using System.Drawing;

namespace Shigure;

/// <summary>
/// 队友单位、数量与平均血量字段的编辑弹窗：按类别动态显隐筛选控件。
/// 队友单位与队友/敌人数量共用 <see cref="CountFilterEditorControl"/>；
/// 平均血量仍使用生命值/光环/职责/距离/战斗卡片筛选。
/// </summary>
public sealed class UnitEditorForm : Form
{
    private const int RowWidth = 800;
    private const int LabelWidth = 132;
    private const int ControlLeft = LabelWidth + 10;

    private static readonly RoleOption[] RoleOptions =
    [
        new("坦克 (1)", 1),
        new("治疗 (2)", 2),
        new("输出 (3)", 3)
    ];

    private static readonly TargetFieldItem[] TargetFieldOptions =
    [
        new("生命值", UnitTargetFieldKind.Health),
        new("治疗吸收", UnitTargetFieldKind.HealingAbsorb),
        new("职责", UnitTargetFieldKind.Role),
        new("驱散", UnitTargetFieldKind.Dispel),
        new("光环", UnitTargetFieldKind.Aura)
    ];

    private static readonly SelectionModeItem[] HealthSelectionModes =
    [
        new("最低", UnitSelectionMode.Lowest),
        new("最高", UnitSelectionMode.Highest)
    ];

    private static readonly SelectionModeItem[] OrderSelectionModes =
    [
        new("正序", UnitSelectionMode.Ascending),
        new("倒序", UnitSelectionMode.Descending)
    ];

    private static readonly SelectionModeItem[] AuraSelectionModes =
    [
        new("最长", UnitSelectionMode.Longest),
        new("最短", UnitSelectionMode.Shortest)
    ];

    private static readonly LowestHealthRoleFilterItem[] LowestHealthRoleFilterOptions =
    [
        new("不筛选职责", null),
        new("包含某职责", UnitRoleFilterKind.Include),
        new("不含某职责", UnitRoleFilterKind.Exclude)
    ];

    private static readonly ThresholdModeItem[] ThresholdModeOptions =
    [
        new("固定阈值", false),
        new("动态阈值", true)
    ];

    private static readonly EnemyThresholdFilterItem[] EnemyHealthFilterOptions =
    [
        new("不筛选生命值", EnemyThresholdFilterKind.None),
        new("生命值大于", EnemyThresholdFilterKind.Above),
        new("生命值小于", EnemyThresholdFilterKind.Below)
    ];

    private static readonly EnemyThresholdFilterItem[] EnemyRangeFilterOptions =
    [
        new("不筛选距离", EnemyThresholdFilterKind.None),
        new("距离大于", EnemyThresholdFilterKind.Above),
        new("距离小于", EnemyThresholdFilterKind.Below)
    ];

    private static readonly EnemyCombatFilterItem[] EnemyCombatFilterOptions =
    [
        new("不筛选战斗", EnemyCombatFilterKind.None),
        new("战斗中", EnemyCombatFilterKind.InCombat),
        new("不在战斗中", EnemyCombatFilterKind.NotInCombat)
    ];

    private static readonly EnemyAuraFilterItem[] EnemyAuraFilterOptions =
    [
        new("不筛选光环", EnemyAuraFilterKind.None),
        new("拥有指定光环", EnemyAuraFilterKind.WithAura),
        new("缺少指定光环", EnemyAuraFilterKind.WithoutAura),
        new("拥有任一光环 (多选)", EnemyAuraFilterKind.HasAnyAura),
        new("拥有全部光环 (多选)", EnemyAuraFilterKind.HasAllAuras),
        new("缺少任一光环 (多选)", EnemyAuraFilterKind.MissingAnyAura),
        new("缺少全部光环 (多选)", EnemyAuraFilterKind.MissingAllAuras)
    ];

    private static readonly AverageTargetItem[] AverageTargetOptions =
    [
        new("队友", AverageHealthTargetKind.Allies),
        new("敌人", AverageHealthTargetKind.Enemies)
    ];

    private readonly IReadOnlyList<ConditionField> _auraFields;
    private readonly IReadOnlyList<ConditionField> _nameplateAuraFields;
    private readonly IReadOnlyList<string> _thresholdFields;
    private readonly HashSet<string> _takenNames;
    private readonly CountFilterEditorControl _countFilterEditor;

    private readonly Label _valueNameLabel = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _valueNameBox = new();
    private readonly UiDropDown _categoryBox = new();
    private readonly UiDropDown _selectorBox = new();
    private readonly UiDropDown _targetFieldBox = new();
    private readonly UiDropDown _selectionModeBox = new();
    private readonly UiDropDown _targetAuraBox = new();
    private readonly UiDropDown _lowestHealthRoleFilterBox = new();
    private readonly FlowLayoutPanel _paramPanel = new();
    private readonly Label _previewLabel = new();
    private readonly ToolTip _toolTip = new();
    private Button? _okButton;

    private readonly UiDropDown _roleBox = new();
    private readonly UiDropDown _enemyHealthFilterBox = new();
    private readonly UiDropDown _enemyAuraFilterBox = new();
    private readonly UiDropDown _enemyRangeFilterBox = new();
    private readonly UiDropDown _enemyCombatFilterBox = new();
    private readonly UiDropDown _enemyAuraBox = new();
    private readonly CheckedListBox _enemyAurasBox = new();
    private readonly ThresholdGroup _enemyHealthThreshold = new();
    private readonly ThresholdGroup _enemyRangeThreshold = new();

    private Label _selectorLabel = null!;
    private Panel _unitTargetRow = null!;
    private Panel _targetAuraRow = null!;
    private Panel _enemyHealthFilterRow = null!;
    private Panel _enemyAuraFilterRow = null!;
    private Panel _enemyRangeFilterRow = null!;
    private Panel _enemyCombatFilterRow = null!;
    private Panel _enemyAuraRow = null!;
    private Panel _enemyAurasRow = null!;
    private UiCardPanel _countFilterSection = null!;
    private UiCardPanel _enemyHealthSection = null!;
    private UiCardPanel _enemyAuraSection = null!;
    private UiCardPanel _enemyRangeSection = null!;
    private UiCardPanel _enemyCombatSection = null!;
    private UiCardPanel _allyRoleSection = null!;
    private Panel _lowestHealthRoleFilterRow = null!;
    private Panel _roleRow = null!;

    public ModuleUnit? ResultUnit { get; private set; }
    public ModuleCountField? ResultCount { get; private set; }
    public ModuleEnemyCountField? ResultEnemyCount { get; private set; }
    public ModuleAverageHealthField? ResultAverageHealth { get; private set; }

    public UnitEditorForm(
        IReadOnlyList<ConditionField> auraFields,
        IReadOnlyList<ConditionField> nameplateAuraFields,
        IReadOnlyList<string> thresholdFields,
        IReadOnlyCollection<string> takenNames,
        ModuleUnit? existingUnit,
        ModuleCountField? existingCount,
        ModuleEnemyCountField? existingEnemyCount,
        ModuleAverageHealthField? existingAverageHealth = null)
    {
        _auraFields = auraFields;
        _nameplateAuraFields = nameplateAuraFields;
        _thresholdFields = thresholdFields;
        _takenNames = new HashSet<string>(takenNames, StringComparer.OrdinalIgnoreCase);
        _countFilterEditor = new CountFilterEditorControl(auraFields, nameplateAuraFields, thresholdFields);
        _countFilterEditor.Changed += (_, _) => UpdatePreview();
        InitializeComponent();
        Seed(existingUnit, existingCount, existingEnemyCount, existingAverageHealth);
        UpdateParamVisibility();
        UpdateValueNameState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.ApplyDarkTitleBar(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RestoreCachedWindowSize();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        SaveWindowSize();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveWindowSize();
        base.OnFormClosed(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _nameBox.Focus();
        _nameBox.SelectAll();
    }

    private void RestoreCachedWindowSize()
    {
        var cached = UiCacheStore.Load().UnitEditorWindowSize;
        if (cached is null || cached.Width <= 0 || cached.Height <= 0)
        {
            return;
        }

        var workingArea = Owner is not null
            ? Screen.FromControl(Owner).WorkingArea
            : Screen.FromControl(this).WorkingArea;
        var maximumWidth = Math.Max(MinimumSize.Width, workingArea.Width - 40);
        var maximumHeight = Math.Max(MinimumSize.Height, workingArea.Height - 40);
        Size = new Size(
            Math.Clamp(cached.Width, MinimumSize.Width, maximumWidth),
            Math.Clamp(cached.Height, MinimumSize.Height, maximumHeight));

        if (Owner is not null)
        {
            CenterToParent();
        }
        else
        {
            CenterToScreen();
        }
    }

    private void SaveWindowSize()
    {
        if (WindowState != FormWindowState.Normal || Width <= 0 || Height <= 0)
        {
            return;
        }

        var cache = UiCacheStore.Load();
        cache.UnitEditorWindowSize = new WindowSize
        {
            Width = Width,
            Height = Height
        };
        UiCacheStore.Save(cache);
    }

    private void InitializeComponent()
    {
        Text = "编辑单位与统计";
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        ClientSize = new Size(RowWidth + 36, 600);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(RowWidth + 52, 420);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(UiTheme.CardPadding, 12, UiTheme.CardPadding, 12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        UiTheme.StyleComboBox(_categoryBox);
        _categoryBox.DropDownWidth = 180;
        _categoryBox.Items.AddRange(["队友单位", "队友数量", "敌人数量", "平均血量"]);
        _categoryBox.SelectedIndex = 0;
        _categoryBox.SelectedIndexChanged += (_, _) =>
        {
            PopulateSelectors();
            UpdateParamVisibility();
            UpdateValueNameState();
        };

        UiTheme.StyleComboBox(_selectorBox);
        _selectorBox.DropDownWidth = 360;
        _selectorBox.SelectedIndexChanged += (_, _) =>
        {
            if (IsAverageHealthCategory)
            {
                PopulateFilterAuras();
            }

            UpdateParamVisibility();
        };

        var headerCard = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4),
            Margin = new Padding(0, 0, 0, UiTheme.PageGap),
            ColumnCount = 1,
            RowCount = 2
        };
        headerCard.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        headerCard.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        // 类别单独一行；平均血量时右侧显示「统计对象」。
        headerCard.Controls.Add(BuildSplitRow("类别", _categoryBox, "选择器", _selectorBox), 0, 0);
        headerCard.Controls.Add(BuildNameRow(), 0, 1);
        root.Controls.Add(headerCard, 0, 0);

        _paramPanel.Dock = DockStyle.Fill;
        _paramPanel.BackColor = Color.Transparent;
        _paramPanel.FlowDirection = FlowDirection.TopDown;
        _paramPanel.WrapContents = false;
        _paramPanel.AutoScroll = true;
        _paramPanel.Margin = new Padding(0);
        _paramPanel.Padding = new Padding(4, 6, 4, 6);
        BuildParamRows();
        var paramsCard = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4),
            Margin = new Padding(0, 0, 0, UiTheme.PageGap),
            ColumnCount = 1,
            RowCount = 1
        };
        paramsCard.Controls.Add(_paramPanel, 0, 0);
        root.Controls.Add(paramsCard, 0, 1);

        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.ForeColor = UiTheme.Muted;
        _previewLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewLabel.AutoEllipsis = true;
        _previewLabel.Margin = new Padding(0);
        var previewCard = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiTheme.CardPadding, 6, UiTheme.CardPadding, 6),
            Margin = new Padding(0, 0, 0, UiTheme.PageGap),
            ColumnCount = 1,
            RowCount = 1
        };
        previewCard.Controls.Add(_previewLabel, 0, 0);
        root.Controls.Add(previewCard, 0, 2);

        root.Controls.Add(BuildActionRow(), 0, 3);

        PopulateSelectors();
        PopulateTargetFieldOptions();
        PopulateTargetAuras();
    }

    private void BuildParamRows()
    {
        UiTheme.StyleComboBox(_targetFieldBox);
        _targetFieldBox.DropDownWidth = 220;
        _targetFieldBox.SelectedIndexChanged += (_, _) =>
        {
            PopulateSelectionModes(preserveSelection: false);
            UpdateParamVisibility();
        };

        UiTheme.StyleComboBox(_selectionModeBox);
        _selectionModeBox.DropDownWidth = 180;
        _selectionModeBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        UiTheme.StyleComboBox(_targetAuraBox);
        _targetAuraBox.DropDownWidth = 360;
        _targetAuraBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        UiTheme.StyleComboBox(_lowestHealthRoleFilterBox);
        _lowestHealthRoleFilterBox.DropDownWidth = 220;
        _lowestHealthRoleFilterBox.Items.AddRange(LowestHealthRoleFilterOptions.Cast<object>().ToArray());
        _lowestHealthRoleFilterBox.SelectedIndex = 0;
        _lowestHealthRoleFilterBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(_roleBox);
        _roleBox.DropDownWidth = 160;
        _roleBox.Items.AddRange(RoleOptions.Cast<object>().ToArray());
        _roleBox.SelectedIndex = 0;
        _roleBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        _unitTargetRow = BuildParamSplitRow("查找的单位", _targetFieldBox, "目标选择", _selectionModeBox);
        _targetAuraRow = BuildLabeledRow("目标光环", _targetAuraBox);
        _lowestHealthRoleFilterRow = BuildLabeledRow("职责筛选", _lowestHealthRoleFilterBox);
        _roleRow = BuildLabeledRow("职责", _roleBox);

        BuildEnemyParamRows();

        _countFilterSection = BuildFilterSection("列表筛选", _countFilterEditor);
        _allyRoleSection = BuildFilterSection("职责", _lowestHealthRoleFilterRow, _roleRow);

        _paramPanel.Controls.AddRange([
            _unitTargetRow,
            _targetAuraRow,
            _countFilterSection,
            _enemyHealthSection,
            _allyRoleSection,
            _enemyAuraSection,
            _enemyRangeSection,
            _enemyCombatSection
        ]);
    }

    // 平均血量：生命值 / 光环 / 距离 / 战斗 四组筛选彼此独立。
    private void BuildEnemyParamRows()
    {
        UiTheme.StyleComboBox(_enemyHealthFilterBox);
        _enemyHealthFilterBox.DropDownWidth = 220;
        _enemyHealthFilterBox.Items.AddRange(EnemyHealthFilterOptions.Cast<object>().ToArray());
        _enemyHealthFilterBox.SelectedIndex = 0;
        _enemyHealthFilterBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(_enemyAuraFilterBox);
        _enemyAuraFilterBox.DropDownWidth = 240;
        _enemyAuraFilterBox.Items.AddRange(EnemyAuraFilterOptions.Cast<object>().ToArray());
        _enemyAuraFilterBox.SelectedIndex = 0;
        _enemyAuraFilterBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(_enemyRangeFilterBox);
        _enemyRangeFilterBox.DropDownWidth = 220;
        _enemyRangeFilterBox.Items.AddRange(EnemyRangeFilterOptions.Cast<object>().ToArray());
        _enemyRangeFilterBox.SelectedIndex = 0;
        _enemyRangeFilterBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(_enemyCombatFilterBox);
        _enemyCombatFilterBox.DropDownWidth = 220;
        _enemyCombatFilterBox.Items.AddRange(EnemyCombatFilterOptions.Cast<object>().ToArray());
        _enemyCombatFilterBox.SelectedIndex = 0;
        _enemyCombatFilterBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        UiTheme.StyleComboBox(_enemyAuraBox);
        _enemyAuraBox.DropDownWidth = 360;
        _enemyAuraBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        UiTheme.StyleCheckedListBox(_enemyAurasBox);
        _enemyAurasBox.ItemCheck += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(UpdatePreview));
            }
        };

        InitializeThresholdGroup(_enemyHealthThreshold, "生命值阈值", 0);
        InitializeThresholdGroup(_enemyRangeThreshold, "距离阈值", 0);

        _enemyHealthFilterRow = BuildLabeledRow("生命值筛选", _enemyHealthFilterBox);
        _enemyAuraFilterRow = BuildLabeledRow("光环筛选", _enemyAuraFilterBox);
        _enemyRangeFilterRow = BuildLabeledRow("距离筛选", _enemyRangeFilterBox);
        _enemyCombatFilterRow = BuildLabeledRow("战斗筛选", _enemyCombatFilterBox);
        _enemyAuraRow = BuildLabeledRow("光环", _enemyAuraBox);
        _enemyAurasRow = BuildLabeledRow("光环 (可多选)", _enemyAurasBox, 116);

        _enemyHealthSection = BuildFilterSection(
            "生命值",
            _enemyHealthFilterRow,
            _enemyHealthThreshold.ModeRow,
            _enemyHealthThreshold.ValueRow,
            _enemyHealthThreshold.FieldRow);
        _enemyAuraSection = BuildFilterSection(
            "光环",
            _enemyAuraFilterRow,
            _enemyAuraRow,
            _enemyAurasRow);
        _enemyRangeSection = BuildFilterSection(
            "距离",
            _enemyRangeFilterRow,
            _enemyRangeThreshold.ModeRow,
            _enemyRangeThreshold.ValueRow,
            _enemyRangeThreshold.FieldRow);
        _enemyCombatSection = BuildFilterSection("战斗", _enemyCombatFilterRow);
    }

    private void PopulateFilterAuras()
    {
        var source = IsAverageHealthCategory && SelectedAverageTarget() == AverageHealthTargetKind.Enemies
            ? _nameplateAuraFields
            : _auraFields;
        _enemyAuraBox.BeginUpdate();
        _enemyAurasBox.BeginUpdate();
        try
        {
            _enemyAuraBox.Items.Clear();
            _enemyAurasBox.Items.Clear();
            foreach (var aura in source)
            {
                _enemyAuraBox.Items.Add(aura);
                _enemyAurasBox.Items.Add(aura);
            }

            if (_enemyAuraBox.Items.Count > 0)
            {
                _enemyAuraBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _enemyAuraBox.EndUpdate();
            _enemyAurasBox.EndUpdate();
        }
    }

    private void PopulateTargetAuras()
    {
        var selected = TryReadAuraSpellId(_targetAuraBox.SelectedItem, out var selectedId)
            ? selectedId
            : (long?)null;
        _targetAuraBox.BeginUpdate();
        try
        {
            _targetAuraBox.Items.Clear();
            foreach (var aura in _auraFields)
            {
                _targetAuraBox.Items.Add(aura);
            }

            if (selected is { } id)
            {
                SelectAura(_targetAuraBox, id);
            }
            else if (_targetAuraBox.Items.Count > 0)
            {
                _targetAuraBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _targetAuraBox.EndUpdate();
        }
    }

    private static UiCardPanel BuildFilterSection(string title, params Control[] rows)
    {
        var section = new UiCardPanel
        {
            Width = RowWidth + 8,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4, 4, 4, 6),
            Margin = new Padding(0, 3, 0, 7),
            ColumnCount = 1,
            RowCount = 2
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = UiTheme.Text,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0)
        };

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        foreach (var row in rows)
        {
            body.Controls.Add(row);
        }

        section.Controls.Add(titleLabel, 0, 0);
        section.Controls.Add(body, 0, 1);
        return section;
    }

    private void InitializeThresholdGroup(ThresholdGroup group, string valueLabel, int defaultValue)
    {
        group.ValueBox.Minimum = 0;
        group.ValueBox.Maximum = int.MaxValue;
        group.ValueBox.Value = defaultValue;
        UiTheme.StyleNumericUpDown(group.ValueBox);
        group.ValueBox.ValueChanged += (_, _) => UpdatePreview();

        UiTheme.StyleComboBox(group.ModeBox);
        group.ModeBox.DropDownWidth = 160;
        group.ModeBox.Items.AddRange(ThresholdModeOptions.Cast<object>().ToArray());
        group.ModeBox.SelectedIndex = 0;
        group.ModeBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(group.FieldBox);
        group.FieldBox.DropDownWidth = 360;
        foreach (var field in _thresholdFields)
        {
            if (!group.FieldBox.Items.Contains(field))
            {
                group.FieldBox.Items.Add(field);
            }
        }

        if (group.FieldBox.Items.Count > 0)
        {
            group.FieldBox.SelectedIndex = 0;
        }

        group.FieldBox.SelectedIndexChanged += (_, _) => UpdatePreview();

        group.ModeRow = BuildLabeledRow($"{valueLabel}类型", group.ModeBox);
        group.ValueRow = BuildLabeledRow(valueLabel, group.ValueBox);
        group.FieldRow = BuildLabeledRow($"动态{valueLabel}", group.FieldBox);
    }

    private Control BuildActionRow()
    {
        var row = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiTheme.CardPadding, 10, UiTheme.CardPadding, 10),
            Margin = new Padding(0),
            ColumnCount = 2,
            RowCount = 1
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        _okButton = UiTheme.CreateButton("确定", UiTheme.ButtonKind.Primary);
        UiTheme.StyleActionButton(_okButton, 84);
        _okButton.Margin = new Padding(8, 0, 0, 0);
        _okButton.Click += (_, _) => OnConfirm();

        var cancelButton = UiTheme.CreateButton("取消", UiTheme.ButtonKind.Secondary);
        UiTheme.StyleActionButton(cancelButton, 84);
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        actions.Controls.Add(_okButton);
        actions.Controls.Add(cancelButton);
        row.Controls.Add(actions, 1, 0);
        AcceptButton = _okButton;
        CancelButton = cancelButton;
        return row;
    }

    private void PopulateSelectors()
    {
        _selectorBox.Items.Clear();
        // 只有平均血量需要在页头选择统计对象。
        var hasSelector = IsAverageHealthCategory;
        _selectorBox.Visible = hasSelector;
        _categoryBox.Bounds = hasSelector
            ? new Rectangle(80, 5, 230, 28)
            : new Rectangle(80, 5, RowWidth - 80, 28);
        if (_selectorLabel is not null)
        {
            _selectorLabel.Visible = hasSelector;
            _selectorLabel.Text = "统计对象";
        }

        if (!hasSelector)
        {
            PopulateFilterAuras();
            return;
        }

        _selectorBox.Items.AddRange(AverageTargetOptions.Cast<object>().ToArray());
        if (_selectorBox.Items.Count > 0)
        {
            _selectorBox.SelectedIndex = 0;
        }

        PopulateFilterAuras();
    }

    private void PopulateTargetFieldOptions()
    {
        _targetFieldBox.Items.Clear();
        _targetFieldBox.Items.AddRange(TargetFieldOptions.Cast<object>().ToArray());
        _targetFieldBox.SelectedIndex = 0;
        PopulateSelectionModes(preserveSelection: false);
    }

    private void PopulateSelectionModes(bool preserveSelection)
    {
        var previous = SelectedSelectionMode();
        var options = SelectionModesFor(SelectedTargetField());
        _selectionModeBox.BeginUpdate();
        try
        {
            _selectionModeBox.Items.Clear();
            _selectionModeBox.Items.AddRange(options.Cast<object>().ToArray());
            if (preserveSelection)
            {
                SelectSelectionMode(previous);
            }
            else if (_selectionModeBox.Items.Count > 0)
            {
                _selectionModeBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _selectionModeBox.EndUpdate();
        }
    }

    private static SelectionModeItem[] SelectionModesFor(UnitTargetFieldKind field)
        => field switch
        {
            UnitTargetFieldKind.Health or UnitTargetFieldKind.HealingAbsorb => HealthSelectionModes,
            UnitTargetFieldKind.Role or UnitTargetFieldKind.Dispel => OrderSelectionModes,
            UnitTargetFieldKind.Aura => AuraSelectionModes,
            _ => HealthSelectionModes
        };

    // 值名称始终对队友单位可用：导出所选单位的目标字段值。
    private void UpdateValueNameState()
    {
        var visible = IsUnitCategory;
        _valueNameLabel.Visible = visible;
        _valueNameBox.Visible = visible;
        _valueNameBox.Enabled = visible;
        if (!visible)
        {
            _valueNameBox.Text = string.Empty;
        }
    }

    private void UpdateParamVisibility()
    {
        if (IsCountCategory || IsEnemyCountCategory || IsUnitCategory)
        {
            _selectorBox.Visible = false;
            _selectorLabel.Visible = false;
            _countFilterEditor.SetEnemyTarget(IsEnemyCountCategory);
            _countFilterSection.Visible = true;
            _unitTargetRow.Visible = IsUnitCategory;
            _targetAuraRow.Visible = IsUnitCategory && SelectedTargetField() == UnitTargetFieldKind.Aura;
            SetAverageRowsVisible(false);
            UpdatePreview();
            return;
        }

        // 平均血量：隐藏列表筛选与单位目标控件，显示卡片筛选。
        _countFilterSection.Visible = false;
        _unitTargetRow.Visible = false;
        _targetAuraRow.Visible = false;
        _selectorBox.Visible = true;
        _selectorLabel.Visible = true;

        var enemyTarget = SelectedAverageTarget() == AverageHealthTargetKind.Enemies;
        UpdateAverageParamVisibility(enemyTarget);
        UpdatePreview();
    }

    private void UpdateAverageParamVisibility(bool enemyTarget)
    {
        var healthFilter = SelectedEnemyHealthFilter() != EnemyThresholdFilterKind.None;
        var rangeFilter = SelectedEnemyRangeFilter() != EnemyThresholdFilterKind.None;
        var auraFilter = SelectedEnemyAuraFilter();

        _enemyHealthSection.Visible = true;
        _enemyAuraSection.Visible = true;
        _enemyRangeSection.Visible = enemyTarget;
        _enemyCombatSection.Visible = enemyTarget;
        _allyRoleSection.Visible = !enemyTarget;
        _lowestHealthRoleFilterRow.Visible = !enemyTarget;
        _roleRow.Visible = !enemyTarget && SelectedLowestHealthRoleFilter() is not null;

        _enemyHealthFilterRow.Visible = true;
        _enemyAuraFilterRow.Visible = true;
        _enemyRangeFilterRow.Visible = enemyTarget;
        _enemyCombatFilterRow.Visible = enemyTarget;
        SetThresholdGroupVisible(_enemyHealthThreshold, healthFilter);
        SetThresholdGroupVisible(_enemyRangeThreshold, enemyTarget && rangeFilter);
        _enemyAuraRow.Visible = auraFilter is EnemyAuraFilterKind.WithAura or EnemyAuraFilterKind.WithoutAura;
        _enemyAurasRow.Visible = auraFilter is EnemyAuraFilterKind.HasAnyAura
            or EnemyAuraFilterKind.HasAllAuras
            or EnemyAuraFilterKind.MissingAnyAura
            or EnemyAuraFilterKind.MissingAllAuras;
    }

    private void SetAverageRowsVisible(bool visible)
    {
        _enemyHealthSection.Visible = visible;
        _enemyAuraSection.Visible = visible;
        _enemyRangeSection.Visible = visible;
        _enemyCombatSection.Visible = visible;
        _allyRoleSection.Visible = visible;
    }

    private static void SetThresholdGroupVisible(ThresholdGroup group, bool visible)
    {
        group.ModeRow.Visible = visible;
        group.ValueRow.Visible = visible && !group.UsesDynamicField;
        group.FieldRow.Visible = visible && group.UsesDynamicField;
    }

    private void UpdatePreview()
    {
        if ((IsUnitCategory || IsCountCategory || IsEnemyCountCategory)
            && !_countFilterEditor.TryValidate(out var filterMessage))
        {
            _previewLabel.ForeColor = UiTheme.Danger;
            _previewLabel.Text = $"预览: {filterMessage}";
            if (_okButton is not null)
            {
                _okButton.Enabled = false;
            }

            return;
        }

        if (IsAverageHealthCategory
            && IsMultiAuraFilter(SelectedEnemyAuraFilter())
            && _enemyAurasBox.CheckedItems.Count < 2)
        {
            _previewLabel.ForeColor = UiTheme.Danger;
            _previewLabel.Text = "预览: 多选光环至少需要选择 2 个光环";
            if (_okButton is not null)
            {
                _okButton.Enabled = false;
            }

            return;
        }

        _previewLabel.ForeColor = UiTheme.Muted;
        if (_okButton is not null)
        {
            _okButton.Enabled = true;
        }

        var text = BuildPreviewText();
        _previewLabel.Text = string.IsNullOrEmpty(text) ? "预览: -" : $"预览: {text}";
    }

    // 用当前控件状态构造一个宽容的(不校验、不弹框)单位/数量, 复用 UnitSummary 渲染预览。
    private string BuildPreviewText()
    {
        if (IsAverageHealthCategory)
        {
            var field = BuildAverageHealth(_nameBox.Text.Trim());
            Func<long, string?> resolver = field.Target == AverageHealthTargetKind.Enemies
                ? ResolveNameplateAuraName
                : ResolveAuraName;
            return UnitSummary.Describe(field, resolver);
        }

        if (IsEnemyCountCategory)
        {
            return UnitSummary.Describe(BuildEnemyCount(_nameBox.Text.Trim()), ResolveNameplateAuraName);
        }

        if (IsCountCategory)
        {
            return UnitSummary.Describe(BuildAllyCount(_nameBox.Text.Trim()), ResolveAuraName);
        }

        return UnitSummary.Describe(BuildFilteredUnit(_nameBox.Text.Trim()), ResolveAuraName);
    }

    private ModuleEnemyCountField BuildEnemyCount(string name)
    {
        return new ModuleEnemyCountField
        {
            Name = name,
            FilterGroups = _countFilterEditor.ReadGroups()
        };
    }

    private ModuleCountField BuildAllyCount(string name)
    {
        return new ModuleCountField
        {
            Name = name,
            FilterGroups = _countFilterEditor.ReadGroups()
        };
    }

    private ModuleUnit BuildFilteredUnit(string name)
    {
        var targetField = SelectedTargetField();
        return new ModuleUnit
        {
            Name = name,
            ValueName = IsUnitCategory
                ? (string.IsNullOrWhiteSpace(_valueNameBox.Text) ? null : _valueNameBox.Text.Trim())
                : null,
            FilterGroups = _countFilterEditor.ReadGroups(),
            TargetField = targetField,
            SelectionMode = SelectedSelectionMode(),
            TargetAuraSpellId = targetField == UnitTargetFieldKind.Aura
                ? (TryReadAuraSpellId(_targetAuraBox.SelectedItem, out var auraId) ? auraId : null)
                : null
        };
    }

    private ModuleAverageHealthField BuildAverageHealth(string name)
    {
        var target = SelectedAverageTarget();
        var field = new ModuleAverageHealthField
        {
            Name = name,
            Target = target,
            HealthFilter = SelectedEnemyHealthFilter(),
            AuraFilter = SelectedEnemyAuraFilter(),
            RoleFilter = target == AverageHealthTargetKind.Allies ? SelectedLowestHealthRoleFilter() : null,
            Role = target == AverageHealthTargetKind.Allies && SelectedLowestHealthRoleFilter() is not null
                ? SelectedRole()
                : null,
            RangeFilter = target == AverageHealthTargetKind.Enemies
                ? SelectedEnemyRangeFilter()
                : EnemyThresholdFilterKind.None,
            CombatFilter = target == AverageHealthTargetKind.Enemies
                ? SelectedEnemyCombatFilter()
                : EnemyCombatFilterKind.None
        };

        if (field.HealthFilter != EnemyThresholdFilterKind.None)
        {
            ReadThresholdGroup(_enemyHealthThreshold, out var fixedValue, out var dynamicField);
            field.HealthThreshold = fixedValue;
            field.HealthThresholdField = dynamicField;
        }

        if (field.RangeFilter != EnemyThresholdFilterKind.None)
        {
            ReadThresholdGroup(_enemyRangeThreshold, out var fixedValue, out var dynamicField);
            field.RangeThreshold = fixedValue;
            field.RangeThresholdField = dynamicField;
        }

        field.AuraSpellIds = field.AuraFilter switch
        {
            EnemyAuraFilterKind.WithAura or EnemyAuraFilterKind.WithoutAura => SingleAuraList(_enemyAuraBox),
            EnemyAuraFilterKind.HasAnyAura
                or EnemyAuraFilterKind.HasAllAuras
                or EnemyAuraFilterKind.MissingAnyAura
                or EnemyAuraFilterKind.MissingAllAuras => CheckedAuras(_enemyAurasBox),
            _ => null
        };
        return field;
    }

    private static void ReadThresholdGroup(ThresholdGroup group, out int? fixedValue, out string? field)
    {
        if (group.UsesDynamicField)
        {
            fixedValue = null;
            field = group.FieldBox.SelectedItem?.ToString()?.Trim();
            return;
        }

        fixedValue = (int)group.ValueBox.Value;
        field = null;
    }

    private void SeedThresholdGroup(
        ThresholdGroup group,
        EnemyThresholdFilterKind filter,
        int? fixedValue,
        string? field)
    {
        if (filter == EnemyThresholdFilterKind.None)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(field))
        {
            SelectThresholdMode(group.ModeBox, usesDynamicField: true);
            SelectThresholdField(group.FieldBox, field.Trim());
            return;
        }

        SelectThresholdMode(group.ModeBox, usesDynamicField: false);
        if (fixedValue is { } value)
        {
            group.ValueBox.Value = Clamp(value, group.ValueBox);
        }
    }

    private void Seed(
        ModuleUnit? unit,
        ModuleCountField? count,
        ModuleEnemyCountField? enemyCount,
        ModuleAverageHealthField? averageHealth)
    {
        if (averageHealth is not null)
        {
            _nameBox.Text = averageHealth.Name;
            _categoryBox.SelectedIndex = 3;
            PopulateSelectors();
            SelectAverageTarget(averageHealth.Target);
            PopulateFilterAuras();
            SelectEnemyThresholdFilter(_enemyHealthFilterBox, averageHealth.HealthFilter);
            SelectEnemyThresholdFilter(_enemyRangeFilterBox, averageHealth.RangeFilter);
            SelectEnemyAuraFilter(averageHealth.AuraFilter);
            SelectEnemyCombatFilter(averageHealth.CombatFilter);
            SelectLowestHealthRoleFilter(averageHealth.RoleFilter);
            if (averageHealth.Role is { } role)
            {
                SelectRole(role);
            }

            SeedThresholdGroup(
                _enemyHealthThreshold,
                averageHealth.HealthFilter,
                averageHealth.HealthThreshold,
                averageHealth.HealthThresholdField);
            SeedThresholdGroup(
                _enemyRangeThreshold,
                averageHealth.RangeFilter,
                averageHealth.RangeThreshold,
                averageHealth.RangeThresholdField);
            var auraSpellIds = averageHealth.AuraSpellIds ?? [];
            SelectAura(_enemyAuraBox, auraSpellIds.Count > 0 ? auraSpellIds[0] : null);
            CheckAuras(_enemyAurasBox, auraSpellIds);
            return;
        }

        if (enemyCount is not null)
        {
            _nameBox.Text = enemyCount.Name;
            _categoryBox.SelectedIndex = 2;
            PopulateSelectors();
            _countFilterEditor.SetEnemyTarget(true);
            _countFilterEditor.LoadGroups(enemyCount.FilterGroups);
            return;
        }

        if (count is not null)
        {
            _nameBox.Text = count.Name;
            _categoryBox.SelectedIndex = 1;
            PopulateSelectors();
            _countFilterEditor.SetEnemyTarget(false);
            _countFilterEditor.LoadGroups(count.FilterGroups);
            return;
        }

        if (unit is not null)
        {
            _nameBox.Text = unit.Name;
            _valueNameBox.Text = unit.ValueName ?? string.Empty;
            _categoryBox.SelectedIndex = 0;
            PopulateSelectors();
            SelectTargetField(unit.TargetField);
            PopulateSelectionModes(preserveSelection: false);
            SelectSelectionMode(unit.SelectionMode);
            PopulateTargetAuras();
            if (unit.TargetAuraSpellId is { } auraId)
            {
                SelectAura(_targetAuraBox, auraId);
            }

            _countFilterEditor.SetEnemyTarget(false);
            _countFilterEditor.LoadGroups(unit.FilterGroups);
        }
    }

    private void OnConfirm()
    {
        var name = _nameBox.Text.Trim();
        if (!ValidateName(name, out var message))
        {
            MessageBox.Show(message, "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (IsAverageHealthCategory)
        {
            var average = BuildAverageHealth(name);
            if (!ValidateAggregateFilters(
                    average.HealthFilter,
                    average.HealthThreshold,
                    average.HealthThresholdField,
                    average.AuraFilter,
                    average.AuraSpellIds,
                    average.RangeFilter,
                    average.RangeThreshold,
                    average.RangeThresholdField))
            {
                return;
            }

            ResultAverageHealth = average;
            DialogResult = DialogResult.OK;
            return;
        }

        if (IsEnemyCountCategory)
        {
            if (!_countFilterEditor.TryValidate(out var filterMessage))
            {
                MessageBox.Show(filterMessage, "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultEnemyCount = BuildEnemyCount(name);
            DialogResult = DialogResult.OK;
            return;
        }

        if (IsCountCategory)
        {
            if (!_countFilterEditor.TryValidate(out var filterMessage))
            {
                MessageBox.Show(filterMessage, "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultCount = BuildAllyCount(name);
            DialogResult = DialogResult.OK;
            return;
        }

        if (!_countFilterEditor.TryValidate(out var unitFilterMessage))
        {
            MessageBox.Show(unitFilterMessage, "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var valueName = _valueNameBox.Text.Trim();
        if (valueName.Length > 0)
        {
            if (string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("值名称不能与名称相同。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ValidateName(valueName, out var valueMessage))
            {
                MessageBox.Show($"值名称: {valueMessage}", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var unit = BuildFilteredUnit(name);
        unit.ValueName = valueName.Length == 0 ? null : valueName;
        if (!ValidateUnit(unit))
        {
            return;
        }

        ResultUnit = unit;
        DialogResult = DialogResult.OK;
    }

    private static bool ValidateAggregateFilters(
        EnemyThresholdFilterKind healthFilter,
        int? healthThreshold,
        string? healthThresholdField,
        EnemyAuraFilterKind auraFilter,
        IReadOnlyList<long>? auraSpellIds,
        EnemyThresholdFilterKind rangeFilter,
        int? rangeThreshold,
        string? rangeThresholdField)
    {
        if (healthFilter != EnemyThresholdFilterKind.None
            && healthThreshold is null
            && string.IsNullOrWhiteSpace(healthThresholdField))
        {
            MessageBox.Show("请选择动态生命值阈值。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (rangeFilter != EnemyThresholdFilterKind.None
            && rangeThreshold is null
            && string.IsNullOrWhiteSpace(rangeThresholdField))
        {
            MessageBox.Show("请选择动态距离阈值。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (auraFilter != EnemyAuraFilterKind.None
            && (auraSpellIds is null || auraSpellIds.Count == 0))
        {
            MessageBox.Show("请选择光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (IsMultiAuraFilter(auraFilter)
            && auraSpellIds!.Count < 2)
        {
            MessageBox.Show("多选光环至少需要选择 2 个光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private static bool IsMultiAuraFilter(EnemyAuraFilterKind filter)
        => filter is EnemyAuraFilterKind.HasAnyAura
            or EnemyAuraFilterKind.HasAllAuras
            or EnemyAuraFilterKind.MissingAnyAura
            or EnemyAuraFilterKind.MissingAllAuras;

    private bool ValidateUnit(ModuleUnit unit)
    {
        if (!IsSelectionModeValid(unit.TargetField, unit.SelectionMode))
        {
            MessageBox.Show("目标选择与查找的单位不匹配。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (unit.TargetField == UnitTargetFieldKind.Aura && unit.TargetAuraSpellId is not > 0)
        {
            MessageBox.Show("请选择目标光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private static bool IsSelectionModeValid(UnitTargetFieldKind field, UnitSelectionMode mode)
        => field switch
        {
            UnitTargetFieldKind.Health or UnitTargetFieldKind.HealingAbsorb
                => mode is UnitSelectionMode.Lowest or UnitSelectionMode.Highest,
            UnitTargetFieldKind.Role or UnitTargetFieldKind.Dispel
                => mode is UnitSelectionMode.Ascending or UnitSelectionMode.Descending,
            UnitTargetFieldKind.Aura
                => mode is UnitSelectionMode.Longest or UnitSelectionMode.Shortest,
            _ => false
        };

    private bool ValidateName(string name, out string message)
    {
        message = string.Empty;
        if (name.Length == 0)
        {
            message = "名称不能为空。";
            return false;
        }

        if (name.Contains('.') || name.Contains('$'))
        {
            message = "名称不能包含 '.' 或 '$'。";
            return false;
        }

        if (int.TryParse(name, out _))
        {
            message = "名称不能是纯数字(会与单位编号混淆)。";
            return false;
        }

        if (_takenNames.Contains(name))
        {
            message = $"名称“{name}”已被其它单位/字段或状态字段占用。";
            return false;
        }

        return true;
    }

    private bool IsUnitCategory => _categoryBox.SelectedIndex == 0;

    private bool IsCountCategory => _categoryBox.SelectedIndex == 1;

    private bool IsEnemyCountCategory => _categoryBox.SelectedIndex == 2;

    private bool IsAverageHealthCategory => _categoryBox.SelectedIndex == 3;

    private UnitTargetFieldKind SelectedTargetField()
        => (_targetFieldBox.SelectedItem as TargetFieldItem)?.Kind ?? UnitTargetFieldKind.Health;

    private UnitSelectionMode SelectedSelectionMode()
        => (_selectionModeBox.SelectedItem as SelectionModeItem)?.Mode ?? UnitSelectionMode.Lowest;

    private UnitRoleFilterKind? SelectedLowestHealthRoleFilter()
        => (_lowestHealthRoleFilterBox.SelectedItem as LowestHealthRoleFilterItem)?.Kind;

    private EnemyThresholdFilterKind SelectedEnemyHealthFilter()
        => (_enemyHealthFilterBox.SelectedItem as EnemyThresholdFilterItem)?.Kind ?? EnemyThresholdFilterKind.None;

    private EnemyThresholdFilterKind SelectedEnemyRangeFilter()
        => (_enemyRangeFilterBox.SelectedItem as EnemyThresholdFilterItem)?.Kind ?? EnemyThresholdFilterKind.None;

    private EnemyCombatFilterKind SelectedEnemyCombatFilter()
        => (_enemyCombatFilterBox.SelectedItem as EnemyCombatFilterItem)?.Kind ?? EnemyCombatFilterKind.None;

    private EnemyAuraFilterKind SelectedEnemyAuraFilter()
        => (_enemyAuraFilterBox.SelectedItem as EnemyAuraFilterItem)?.Kind ?? EnemyAuraFilterKind.None;

    private AverageHealthTargetKind SelectedAverageTarget()
        => (_selectorBox.SelectedItem as AverageTargetItem)?.Kind ?? AverageHealthTargetKind.Allies;

    private static List<long> SingleAuraList(UiDropDown box)
    {
        return TryReadAuraSpellId(box.SelectedItem, out var spellId)
            ? new List<long> { spellId }
            : new List<long>();
    }

    private static List<long> CheckedAuras(CheckedListBox box)
    {
        var list = new List<long>();
        foreach (var item in box.CheckedItems)
        {
            if (TryReadAuraSpellId(item, out var spellId) && !list.Contains(spellId))
            {
                list.Add(spellId);
            }
        }

        return list;
    }

    private int SelectedRole() => (_roleBox.SelectedItem as RoleOption)?.Value ?? 1;

    private void SelectLowestHealthRoleFilter(UnitRoleFilterKind? kind)
    {
        for (var i = 0; i < _lowestHealthRoleFilterBox.Items.Count; i++)
        {
            if (_lowestHealthRoleFilterBox.Items[i] is LowestHealthRoleFilterItem item && item.Kind == kind)
            {
                _lowestHealthRoleFilterBox.SelectedIndex = i;
                return;
            }
        }

        _lowestHealthRoleFilterBox.SelectedIndex = 0;
    }

    private static void SelectThresholdMode(UiDropDown box, bool usesDynamicField)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ThresholdModeItem item && item.UsesDynamicField == usesDynamicField)
            {
                box.SelectedIndex = i;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private static void SelectThresholdField(UiDropDown box, string field)
    {
        var index = box.Items.IndexOf(field);
        if (index < 0)
        {
            box.Items.Add(field);
            index = box.Items.Count - 1;
        }

        box.SelectedIndex = index;
    }

    private static void SelectEnemyThresholdFilter(UiDropDown box, EnemyThresholdFilterKind kind)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is EnemyThresholdFilterItem item && item.Kind == kind)
            {
                box.SelectedIndex = i;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private void SelectEnemyCombatFilter(EnemyCombatFilterKind kind)
    {
        for (var i = 0; i < _enemyCombatFilterBox.Items.Count; i++)
        {
            if (_enemyCombatFilterBox.Items[i] is EnemyCombatFilterItem item && item.Kind == kind)
            {
                _enemyCombatFilterBox.SelectedIndex = i;
                return;
            }
        }

        _enemyCombatFilterBox.SelectedIndex = 0;
    }

    private void SelectEnemyAuraFilter(EnemyAuraFilterKind kind)
    {
        for (var i = 0; i < _enemyAuraFilterBox.Items.Count; i++)
        {
            if (_enemyAuraFilterBox.Items[i] is EnemyAuraFilterItem item && item.Kind == kind)
            {
                _enemyAuraFilterBox.SelectedIndex = i;
                return;
            }
        }

        _enemyAuraFilterBox.SelectedIndex = 0;
    }

    private void SelectAverageTarget(AverageHealthTargetKind kind)
    {
        for (var i = 0; i < _selectorBox.Items.Count; i++)
        {
            if (_selectorBox.Items[i] is AverageTargetItem item && item.Kind == kind)
            {
                _selectorBox.SelectedIndex = i;
                return;
            }
        }

        _selectorBox.SelectedIndex = 0;
    }

    private void SelectTargetField(UnitTargetFieldKind kind)
    {
        for (var i = 0; i < _targetFieldBox.Items.Count; i++)
        {
            if (_targetFieldBox.Items[i] is TargetFieldItem item && item.Kind == kind)
            {
                _targetFieldBox.SelectedIndex = i;
                return;
            }
        }

        _targetFieldBox.SelectedIndex = 0;
    }

    private void SelectSelectionMode(UnitSelectionMode mode)
    {
        for (var i = 0; i < _selectionModeBox.Items.Count; i++)
        {
            if (_selectionModeBox.Items[i] is SelectionModeItem item && item.Mode == mode)
            {
                _selectionModeBox.SelectedIndex = i;
                return;
            }
        }

        if (_selectionModeBox.Items.Count > 0)
        {
            _selectionModeBox.SelectedIndex = 0;
        }
    }

    private void SelectRole(int role)
    {
        for (var i = 0; i < _roleBox.Items.Count; i++)
        {
            if (_roleBox.Items[i] is RoleOption option && option.Value == role)
            {
                _roleBox.SelectedIndex = i;
                return;
            }
        }
    }

    private static void SelectAura(UiDropDown box, long? auraSpellId)
    {
        if (auraSpellId is null)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (TryReadAuraSpellId(box.Items[i], out var existing) && existing == auraSpellId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            box.Items.Add(UnknownAura(auraSpellId.Value));
            index = box.Items.Count - 1;
        }

        box.SelectedIndex = index;
    }

    private static void CheckAuras(CheckedListBox box, IReadOnlyList<long>? auraSpellIds)
    {
        if (auraSpellIds is null)
        {
            return;
        }

        foreach (var auraSpellId in auraSpellIds)
        {
            var index = -1;
            for (var i = 0; i < box.Items.Count; i++)
            {
                if (TryReadAuraSpellId(box.Items[i], out var existing) && existing == auraSpellId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = box.Items.Add(UnknownAura(auraSpellId));
            }

            box.SetItemChecked(index, true);
        }
    }

    private static ConditionField UnknownAura(long spellId)
        => new(
            SpellFieldKey.AuraMember(spellId),
            $"未知光环 / {spellId}",
            ConditionFieldType.Int,
            ConditionFieldCategory.Aura);

    private static bool TryReadAuraSpellId(object? item, out long spellId)
    {
        var value = item is ConditionField field ? field.Name : item?.ToString();
        foreach (var part in value?.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
        {
            if (long.TryParse(part, out spellId) && spellId > 0)
            {
                return true;
            }
        }

        spellId = 0;
        return false;
    }

    private string? ResolveAuraName(long spellId)
        => _auraFields.FirstOrDefault(field => TryReadAuraSpellId(field, out var id) && id == spellId)
            ?.DisplayName.Split(" / ", 2, StringSplitOptions.TrimEntries)[0];

    private string? ResolveNameplateAuraName(long spellId)
        => _nameplateAuraFields.FirstOrDefault(field => TryReadAuraSpellId(field, out var id) && id == spellId)
            ?.DisplayName.Split(" / ", 2, StringSplitOptions.TrimEntries)[0];

    private static decimal Clamp(int value, NumericUpDown box)
    {
        return Math.Clamp(value, (int)box.Minimum, (int)box.Maximum);
    }

    private static Panel BuildLabeledRow(string label, Control control, int height = 44)
    {
        var panel = new Panel
        {
            Width = RowWidth,
            Height = height,
            BackColor = UiTheme.SurfaceRaised,
            Margin = new Padding(0, 1, 0, 5)
        };

        var labelControl = new Label
        {
            Text = label,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(0, height > 50 ? 4 : Math.Max(0, (height - 24) / 2), LabelWidth, 24),
            AutoEllipsis = true
        };

        control.Bounds = new Rectangle(ControlLeft, 3, RowWidth - ControlLeft, height - 6);
        panel.Controls.Add(control);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private Panel BuildParamSplitRow(string labelA, Control controlA, string labelB, Control controlB)
    {
        var panel = new Panel
        {
            Width = RowWidth,
            Height = 44,
            BackColor = UiTheme.SurfaceRaised,
            Margin = new Padding(0, 1, 0, 5)
        };

        var labelAControl = new Label
        {
            Text = labelA,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(0, 10, LabelWidth, 24),
            AutoEllipsis = true
        };
        controlA.Bounds = new Rectangle(ControlLeft, 8, 230, 28);

        var labelBControl = new Label
        {
            Text = labelB,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(ControlLeft + 246, 10, 90, 24),
            AutoEllipsis = true
        };
        controlB.Bounds = new Rectangle(ControlLeft + 340, 8, RowWidth - (ControlLeft + 340), 28);

        panel.Controls.Add(controlA);
        panel.Controls.Add(labelAControl);
        panel.Controls.Add(controlB);
        panel.Controls.Add(labelBControl);
        return panel;
    }

    private Control BuildSplitRow(string labelA, Control controlA, string labelB, Control controlB)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceRaised,
            Margin = new Padding(0)
        };

        var labelAControl = new Label
        {
            Text = labelA,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(0, 5, 72, 28),
            AutoEllipsis = true
        };
        controlA.Bounds = new Rectangle(80, 5, 230, 28);

        _selectorLabel = new Label
        {
            Text = labelB,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(330, 5, 72, 28),
            AutoEllipsis = true
        };
        controlB.Bounds = new Rectangle(408, 5, RowWidth - 408, 28);

        panel.Controls.Add(controlA);
        panel.Controls.Add(labelAControl);
        panel.Controls.Add(controlB);
        panel.Controls.Add(_selectorLabel);
        return panel;
    }

    private Control BuildNameRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceRaised,
            Margin = new Padding(0)
        };

        var nameLabel = new Label
        {
            Text = "名称",
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(0, 5, 72, 28),
            AutoEllipsis = true
        };
        UiTheme.StyleTextBox(_nameBox);
        _nameBox.Bounds = new Rectangle(80, 5, 230, 28);
        _nameBox.TextChanged += (_, _) => UpdatePreview();

        _valueNameLabel.Text = "值名称";
        _valueNameLabel.ForeColor = UiTheme.Muted;
        _valueNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        _valueNameLabel.Bounds = new Rectangle(330, 5, 130, 28);
        _valueNameLabel.AutoEllipsis = true;
        UiTheme.StyleTextBox(_valueNameBox);
        _valueNameBox.Bounds = new Rectangle(466, 5, RowWidth - 466, 28);
        _valueNameBox.TextChanged += (_, _) => UpdatePreview();
        const string valueTip = "可选：把所选单位的目标字段值暴露为同名数值条件字段";
        _toolTip.SetToolTip(_valueNameBox, valueTip);
        _toolTip.SetToolTip(_valueNameLabel, valueTip);

        panel.Controls.Add(_nameBox);
        panel.Controls.Add(nameLabel);
        panel.Controls.Add(_valueNameBox);
        panel.Controls.Add(_valueNameLabel);
        return panel;
    }

    private sealed record TargetFieldItem(string Text, UnitTargetFieldKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record SelectionModeItem(string Text, UnitSelectionMode Mode)
    {
        public override string ToString() => Text;
    }

    private sealed record LowestHealthRoleFilterItem(string Text, UnitRoleFilterKind? Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record ThresholdModeItem(string Text, bool UsesDynamicField)
    {
        public override string ToString() => Text;
    }

    private sealed record EnemyThresholdFilterItem(string Text, EnemyThresholdFilterKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record EnemyAuraFilterItem(string Text, EnemyAuraFilterKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record EnemyCombatFilterItem(string Text, EnemyCombatFilterKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record AverageTargetItem(string Text, AverageHealthTargetKind Kind)
    {
        public override string ToString() => Text;
    }

    /// <summary>一组「阈值类型 + 固定阈值 + 动态阈值」控件与其所在的三行。</summary>
    private sealed class ThresholdGroup
    {
        public UiDropDown ModeBox { get; } = new();
        public NumericUpDown ValueBox { get; } = new();
        public UiDropDown FieldBox { get; } = new();
        public Panel ModeRow { get; set; } = null!;
        public Panel ValueRow { get; set; } = null!;
        public Panel FieldRow { get; set; } = null!;

        public bool UsesDynamicField => (ModeBox.SelectedItem as ThresholdModeItem)?.UsesDynamicField == true;
    }

    private sealed record RoleOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }
}
