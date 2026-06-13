using System.Drawing;

namespace Shigure;

/// <summary>
/// 动态单位 / 数量字段的编辑弹窗: 选择类别(单位/数量)与选择器, 按所选类型动态显隐参数控件。
/// 光环候选来自当前职业/专精的 group 字段。校验名称非空、唯一、非纯数字、不含 '.'/'$'。
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

    private static readonly DispelTypeOption[] DispelTypeOptions =
    [
        new("1: 魔法", 1),
        new("2: 疾病", 2),
        new("3: 诅咒", 3),
        new("4: 中毒", 4)
    ];

    private static readonly LowestHealthAuraFilterItem[] LowestHealthAuraFilterOptions =
    [
        new("不筛选光环", LowestHealthAuraFilterKind.None),
        new("带任一光环", LowestHealthAuraFilterKind.WithAnyAura),
        new("不带某光环", LowestHealthAuraFilterKind.WithoutAura),
        new("带某光环", LowestHealthAuraFilterKind.WithAura),
        new("某光环值等于", LowestHealthAuraFilterKind.WithAuraCount)
    ];

    private static readonly SelectorItem[] UnitSelectors =
    [
        new("生命值最低", UnitSelectorKind.LowestHealth),
        new("按职责", UnitSelectorKind.UnitWithRole),
        new("按职责且不带某光环", UnitSelectorKind.UnitWithRoleWithoutAura),
        new("带某光环(持续最久)", UnitSelectorKind.UnitWithAura),
        new("带某驱散类型", UnitSelectorKind.UnitWithDispelType)
    ];

    private static readonly CountItem[] CountSelectors =
    [
        new("低于阈值的人数", CountKind.UnitsBelowHealth),
        new("不带某光环且低血的人数", CountKind.UnitsWithoutAuraBelowHealth),
        new("拥有某光环的人数", CountKind.UnitsWithAura)
    ];

    private readonly IReadOnlyList<string> _auraFields;
    private readonly HashSet<string> _takenNames;

    private readonly Label _healthNameLabel = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _healthNameBox = new();
    private readonly ComboBox _categoryBox = new();
    private readonly ComboBox _selectorBox = new();
    private readonly ComboBox _lowestHealthAuraFilterBox = new();
    private readonly FlowLayoutPanel _paramPanel = new();

    private readonly NumericUpDown _thresholdBox = new();
    private readonly ComboBox _roleBox = new();
    private readonly CheckBox _reverseBox = new();
    private readonly ComboBox _auraBox = new();
    private readonly CheckedListBox _aurasBox = new();
    private readonly NumericUpDown _auraCountBox = new();
    private readonly ComboBox _dispelTypeBox = new();

    private Panel _thresholdRow = null!;
    private Panel _lowestHealthAuraFilterRow = null!;
    private Panel _roleRow = null!;
    private Panel _reverseRow = null!;
    private Panel _auraRow = null!;
    private Panel _aurasRow = null!;
    private Panel _auraCountRow = null!;
    private Panel _dispelRow = null!;

    public ModuleUnit? ResultUnit { get; private set; }
    public ModuleCountField? ResultCount { get; private set; }

    public UnitEditorForm(
        IReadOnlyList<string> auraFields,
        IReadOnlyCollection<string> takenNames,
        ModuleUnit? existingUnit,
        ModuleCountField? existingCount)
    {
        _auraFields = auraFields;
        _takenNames = new HashSet<string>(takenNames, StringComparer.OrdinalIgnoreCase);
        InitializeComponent();
        Seed(existingUnit, existingCount);
        UpdateParamVisibility();
        UpdateHealthNameState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.ApplyDarkTitleBar(this);
    }

    private void InitializeComponent()
    {
        Text = "编辑单位";
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        ClientSize = new Size(RowWidth + 36, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Background,
            Padding = new Padding(14, 12, 14, 12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        UiTheme.StyleComboBox(_categoryBox);
        _categoryBox.DropDownWidth = 180;
        _categoryBox.Items.AddRange(["单位 (可作目标)", "数量 (仅条件)"]);
        _categoryBox.SelectedIndex = 0;
        _categoryBox.SelectedIndexChanged += (_, _) =>
        {
            PopulateSelectors();
            UpdateParamVisibility();
            UpdateHealthNameState();
        };

        UiTheme.StyleComboBox(_selectorBox);
        _selectorBox.DropDownWidth = 360;
        _selectorBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateParamVisibility();
            UpdateHealthNameState();
        };

        root.Controls.Add(BuildSplitRow("类别", _categoryBox, "选择器", _selectorBox), 0, 0);
        root.Controls.Add(BuildNameRow(), 0, 1);

        _paramPanel.Dock = DockStyle.Fill;
        _paramPanel.BackColor = UiTheme.SurfaceRaised;
        _paramPanel.FlowDirection = FlowDirection.TopDown;
        _paramPanel.WrapContents = false;
        _paramPanel.AutoScroll = true;
        _paramPanel.Margin = new Padding(0, 8, 0, 6);
        _paramPanel.Padding = new Padding(8, 6, 8, 6);
        BuildParamRows();
        root.Controls.Add(_paramPanel, 0, 2);

        root.Controls.Add(BuildActionRow(), 0, 3);

        PopulateSelectors();
    }

    private void BuildParamRows()
    {
        _thresholdBox.Minimum = 1;
        _thresholdBox.Maximum = int.MaxValue;
        _thresholdBox.Value = 100;
        StyleNumeric(_thresholdBox);

        UiTheme.StyleComboBox(_lowestHealthAuraFilterBox);
        _lowestHealthAuraFilterBox.DropDownWidth = 220;
        _lowestHealthAuraFilterBox.Items.AddRange(LowestHealthAuraFilterOptions.Cast<object>().ToArray());
        _lowestHealthAuraFilterBox.SelectedIndex = 0;
        _lowestHealthAuraFilterBox.SelectedIndexChanged += (_, _) => UpdateParamVisibility();

        UiTheme.StyleComboBox(_roleBox);
        _roleBox.DropDownWidth = 160;
        _roleBox.Items.AddRange(RoleOptions.Cast<object>().ToArray());
        _roleBox.SelectedIndex = 0;

        _reverseBox.Text = "取逆序最后一个匹配单位";
        _reverseBox.ForeColor = UiTheme.Text;
        _reverseBox.BackColor = UiTheme.SurfaceRaised;
        _reverseBox.AutoSize = false;
        _reverseBox.TextAlign = ContentAlignment.MiddleLeft;

        UiTheme.StyleComboBox(_auraBox);
        _auraBox.DropDownWidth = 360;
        foreach (var aura in _auraFields)
        {
            _auraBox.Items.Add(aura);
        }

        if (_auraBox.Items.Count > 0)
        {
            _auraBox.SelectedIndex = 0;
        }

        UiTheme.StyleCheckedListBox(_aurasBox);
        foreach (var aura in _auraFields)
        {
            _aurasBox.Items.Add(aura);
        }

        _auraCountBox.Minimum = 0;
        _auraCountBox.Maximum = 100;
        _auraCountBox.Value = 1;
        StyleNumeric(_auraCountBox);

        UiTheme.StyleComboBox(_dispelTypeBox);
        _dispelTypeBox.DropDownWidth = 180;
        _dispelTypeBox.Items.AddRange(DispelTypeOptions.Cast<object>().ToArray());
        _dispelTypeBox.SelectedIndex = 0;

        _thresholdRow = BuildLabeledRow("血量阈值 (<)", _thresholdBox);
        _lowestHealthAuraFilterRow = BuildLabeledRow("光环筛选", _lowestHealthAuraFilterBox);
        _roleRow = BuildLabeledRow("职责", _roleBox);
        _reverseRow = BuildLabeledRow(string.Empty, _reverseBox);
        _auraRow = BuildLabeledRow("光环", _auraBox);
        _aurasRow = BuildLabeledRow("光环 (可多选)", _aurasBox, 116);
        _auraCountRow = BuildLabeledRow("光环值", _auraCountBox);
        _dispelRow = BuildLabeledRow("驱散类型", _dispelTypeBox);

        _paramPanel.Controls.AddRange([_thresholdRow, _lowestHealthAuraFilterRow, _roleRow, _reverseRow, _auraRow, _aurasRow, _auraCountRow, _dispelRow]);
    }

    private Control BuildActionRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = UiTheme.Background,
            Margin = new Padding(0)
        };

        var okButton = UiTheme.CreateButton("确定", UiTheme.Accent, Color.Black);
        okButton.Width = 76;
        okButton.Height = 30;
        okButton.Margin = new Padding(6, 6, 0, 0);
        okButton.Click += (_, _) => OnConfirm();

        var cancelButton = UiTheme.CreateButton("取消", UiTheme.Field, UiTheme.Text);
        cancelButton.Width = 76;
        cancelButton.Height = 30;
        cancelButton.Margin = new Padding(6, 6, 0, 0);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        row.Controls.Add(okButton);
        row.Controls.Add(cancelButton);
        CancelButton = cancelButton;
        return row;
    }

    private void PopulateSelectors()
    {
        _selectorBox.Items.Clear();
        if (IsCountCategory)
        {
            _selectorBox.Items.AddRange(CountSelectors.Cast<object>().ToArray());
        }
        else
        {
            _selectorBox.Items.AddRange(UnitSelectors.Cast<object>().ToArray());
        }

        if (_selectorBox.Items.Count > 0)
        {
            _selectorBox.SelectedIndex = 0;
        }
    }

    // 值名称只对"生命值最低"单位有意义(把该单位的 生命值 暴露成数值条件字段)。
    private void UpdateHealthNameState()
    {
        var visible = SupportsHealthName();
        _healthNameLabel.Visible = visible;
        _healthNameBox.Visible = visible;
        _healthNameBox.Enabled = visible;
        if (!visible)
        {
            _healthNameBox.Text = string.Empty;
        }
    }

    private void UpdateParamVisibility()
    {
        bool threshold = false, lowestHealthAuraFilter = false, role = false, reverse = false, auraSingle = false, auraMulti = false, auraCount = false, dispel = false;

        if (IsCountCategory)
        {
            switch ((_selectorBox.SelectedItem as CountItem)?.Kind)
            {
                case CountKind.UnitsBelowHealth:
                    threshold = true;
                    break;
                case CountKind.UnitsWithoutAuraBelowHealth:
                    threshold = auraSingle = true;
                    break;
                case CountKind.UnitsWithAura:
                    auraSingle = true;
                    break;
            }
        }
        else
        {
            switch ((_selectorBox.SelectedItem as SelectorItem)?.Kind)
            {
                case UnitSelectorKind.LowestHealth:
                    threshold = lowestHealthAuraFilter = true;
                    switch (SelectedLowestHealthAuraFilter())
                    {
                        case LowestHealthAuraFilterKind.WithAnyAura:
                            auraMulti = true;
                            break;
                        case LowestHealthAuraFilterKind.WithoutAura:
                        case LowestHealthAuraFilterKind.WithAura:
                            auraSingle = true;
                            break;
                        case LowestHealthAuraFilterKind.WithAuraCount:
                            auraSingle = auraCount = true;
                            break;
                    }

                    break;
                case UnitSelectorKind.LowestHealthWithAnyAura:
                    threshold = auraMulti = true;
                    break;
                case UnitSelectorKind.LowestHealthWithoutAura:
                case UnitSelectorKind.LowestHealthWithAura:
                    threshold = auraSingle = true;
                    break;
                case UnitSelectorKind.LowestHealthWithAuraCount:
                    threshold = auraSingle = auraCount = true;
                    break;
                case UnitSelectorKind.UnitWithRole:
                    role = reverse = true;
                    break;
                case UnitSelectorKind.UnitWithRoleWithoutAura:
                    role = reverse = auraSingle = true;
                    break;
                case UnitSelectorKind.UnitWithAura:
                    auraSingle = true;
                    break;
                case UnitSelectorKind.UnitWithDispelType:
                    dispel = true;
                    break;
            }
        }

        _thresholdRow.Visible = threshold;
        _lowestHealthAuraFilterRow.Visible = lowestHealthAuraFilter;
        _roleRow.Visible = role;
        _reverseRow.Visible = reverse;
        _auraRow.Visible = auraSingle;
        _aurasRow.Visible = auraMulti;
        _auraCountRow.Visible = auraCount;
        _dispelRow.Visible = dispel;
    }

    private void Seed(ModuleUnit? unit, ModuleCountField? count)
    {
        if (count is not null)
        {
            _nameBox.Text = count.Name;
            _categoryBox.SelectedIndex = 1;
            PopulateSelectors();
            SelectSelector(count.Kind);
            if (count.HealthThreshold is { } th)
            {
                _thresholdBox.Value = Clamp(th, _thresholdBox);
            }

            SelectAura(_auraBox, count.AuraName);
            return;
        }

        if (unit is not null)
        {
            _nameBox.Text = unit.Name;
            _healthNameBox.Text = unit.HealthName ?? string.Empty;
            _categoryBox.SelectedIndex = 0;
            PopulateSelectors();
            SelectSelector(DisplaySelectorKind(unit.Kind));
            SelectLowestHealthAuraFilter(unit.Kind);
            if (unit.HealthThreshold is { } th)
            {
                _thresholdBox.Value = Clamp(th, _thresholdBox);
            }

            if (unit.Role is { } r)
            {
                SelectRole(r);
            }

            _reverseBox.Checked = unit.Reverse;
            SelectAura(_auraBox, unit.AuraNames is { Count: > 0 } ? unit.AuraNames[0] : null);
            CheckAuras(unit.AuraNames);
            if (unit.AuraCount is { } ac)
            {
                _auraCountBox.Value = Clamp(ac, _auraCountBox);
            }

            if (unit.DispelType is { } dt)
            {
                SelectDispelType(dt);
            }
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

        if (IsCountCategory)
        {
            var kind = (_selectorBox.SelectedItem as CountItem)?.Kind ?? CountKind.UnitsBelowHealth;
            var count = new ModuleCountField { Name = name, Kind = kind };
            switch (kind)
            {
                case CountKind.UnitsBelowHealth:
                    count.HealthThreshold = (int)_thresholdBox.Value;
                    break;
                case CountKind.UnitsWithoutAuraBelowHealth:
                    count.HealthThreshold = (int)_thresholdBox.Value;
                    count.AuraName = SelectedAura();
                    break;
                case CountKind.UnitsWithAura:
                    count.AuraName = SelectedAura();
                    break;
            }

            if (RequiresAura(kind) && string.IsNullOrEmpty(count.AuraName))
            {
                MessageBox.Show("请选择光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultCount = count;
            DialogResult = DialogResult.OK;
            return;
        }

        var selectorKind = (_selectorBox.SelectedItem as SelectorItem)?.Kind ?? UnitSelectorKind.LowestHealth;
        var moduleUnit = new ModuleUnit { Name = name, Kind = selectorKind };
        switch (selectorKind)
        {
            case UnitSelectorKind.LowestHealth:
                moduleUnit.HealthThreshold = (int)_thresholdBox.Value;
                switch (SelectedLowestHealthAuraFilter())
                {
                    case LowestHealthAuraFilterKind.WithAnyAura:
                        moduleUnit.Kind = UnitSelectorKind.LowestHealthWithAnyAura;
                        moduleUnit.AuraNames = CheckedAuras();
                        if (moduleUnit.AuraNames.Count == 0)
                        {
                            MessageBox.Show("请至少勾选一个光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        break;
                    case LowestHealthAuraFilterKind.WithoutAura:
                        moduleUnit.Kind = UnitSelectorKind.LowestHealthWithoutAura;
                        moduleUnit.AuraNames = SingleAuraList();
                        break;
                    case LowestHealthAuraFilterKind.WithAura:
                        moduleUnit.Kind = UnitSelectorKind.LowestHealthWithAura;
                        moduleUnit.AuraNames = SingleAuraList();
                        break;
                    case LowestHealthAuraFilterKind.WithAuraCount:
                        moduleUnit.Kind = UnitSelectorKind.LowestHealthWithAuraCount;
                        moduleUnit.AuraNames = SingleAuraList();
                        moduleUnit.AuraCount = (int)_auraCountBox.Value;
                        break;
                }

                break;
            case UnitSelectorKind.LowestHealthWithAnyAura:
                moduleUnit.HealthThreshold = (int)_thresholdBox.Value;
                moduleUnit.AuraNames = CheckedAuras();
                if (moduleUnit.AuraNames.Count == 0)
                {
                    MessageBox.Show("请至少勾选一个光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                break;
            case UnitSelectorKind.LowestHealthWithoutAura:
            case UnitSelectorKind.LowestHealthWithAura:
                moduleUnit.HealthThreshold = (int)_thresholdBox.Value;
                moduleUnit.AuraNames = SingleAuraList();
                break;
            case UnitSelectorKind.LowestHealthWithAuraCount:
                moduleUnit.HealthThreshold = (int)_thresholdBox.Value;
                moduleUnit.AuraNames = SingleAuraList();
                moduleUnit.AuraCount = (int)_auraCountBox.Value;
                break;
            case UnitSelectorKind.UnitWithRole:
                moduleUnit.Role = SelectedRole();
                moduleUnit.Reverse = _reverseBox.Checked;
                break;
            case UnitSelectorKind.UnitWithRoleWithoutAura:
                moduleUnit.Role = SelectedRole();
                moduleUnit.Reverse = _reverseBox.Checked;
                moduleUnit.AuraNames = SingleAuraList();
                break;
            case UnitSelectorKind.UnitWithAura:
                moduleUnit.AuraNames = SingleAuraList();
                break;
            case UnitSelectorKind.UnitWithDispelType:
                moduleUnit.DispelType = SelectedDispelType();
                break;
        }

        if (UnitRequiresAura(moduleUnit.Kind) && (moduleUnit.AuraNames is null || moduleUnit.AuraNames.Count == 0))
        {
            MessageBox.Show("请选择光环。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var healthName = SupportsHealthName() ? _healthNameBox.Text.Trim() : string.Empty;
        if (healthName.Length > 0)
        {
            if (string.Equals(healthName, name, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("值名称不能与名称相同。", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ValidateName(healthName, out var healthMessage))
            {
                MessageBox.Show($"值名称: {healthMessage}", "Shigure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        moduleUnit.HealthName = healthName.Length == 0 ? null : healthName;
        ResultUnit = moduleUnit;
        DialogResult = DialogResult.OK;
    }

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

    private bool IsCountCategory => _categoryBox.SelectedIndex == 1;

    private bool SupportsHealthName()
        => !IsCountCategory
            && (_selectorBox.SelectedItem as SelectorItem)?.Kind == UnitSelectorKind.LowestHealth;

    private static bool RequiresAura(CountKind kind)
        => kind is CountKind.UnitsWithoutAuraBelowHealth or CountKind.UnitsWithAura;

    private static bool UnitRequiresAura(UnitSelectorKind kind)
        => kind is UnitSelectorKind.LowestHealthWithAnyAura
            or UnitSelectorKind.LowestHealthWithoutAura
            or UnitSelectorKind.LowestHealthWithAura
            or UnitSelectorKind.LowestHealthWithAuraCount
            or UnitSelectorKind.UnitWithRoleWithoutAura
            or UnitSelectorKind.UnitWithAura;

    private string? SelectedAura() => _auraBox.SelectedItem?.ToString();

    private LowestHealthAuraFilterKind SelectedLowestHealthAuraFilter()
        => (_lowestHealthAuraFilterBox.SelectedItem as LowestHealthAuraFilterItem)?.Kind
            ?? LowestHealthAuraFilterKind.None;

    private List<string> SingleAuraList()
    {
        var aura = SelectedAura();
        return string.IsNullOrEmpty(aura) ? new List<string>() : new List<string> { aura };
    }

    private List<string> CheckedAuras()
    {
        var list = new List<string>();
        foreach (var item in _aurasBox.CheckedItems)
        {
            if (item?.ToString() is { Length: > 0 } name)
            {
                list.Add(name);
            }
        }

        return list;
    }

    private int SelectedRole() => (_roleBox.SelectedItem as RoleOption)?.Value ?? 1;

    private int SelectedDispelType() => (_dispelTypeBox.SelectedItem as DispelTypeOption)?.Value ?? 1;

    private static UnitSelectorKind DisplaySelectorKind(UnitSelectorKind kind)
        => kind is UnitSelectorKind.LowestHealthWithAnyAura
            or UnitSelectorKind.LowestHealthWithoutAura
            or UnitSelectorKind.LowestHealthWithAura
            or UnitSelectorKind.LowestHealthWithAuraCount
                ? UnitSelectorKind.LowestHealth
                : kind;

    private void SelectLowestHealthAuraFilter(UnitSelectorKind kind)
    {
        var filter = kind switch
        {
            UnitSelectorKind.LowestHealthWithAnyAura => LowestHealthAuraFilterKind.WithAnyAura,
            UnitSelectorKind.LowestHealthWithoutAura => LowestHealthAuraFilterKind.WithoutAura,
            UnitSelectorKind.LowestHealthWithAura => LowestHealthAuraFilterKind.WithAura,
            UnitSelectorKind.LowestHealthWithAuraCount => LowestHealthAuraFilterKind.WithAuraCount,
            _ => LowestHealthAuraFilterKind.None
        };

        for (var i = 0; i < _lowestHealthAuraFilterBox.Items.Count; i++)
        {
            if (_lowestHealthAuraFilterBox.Items[i] is LowestHealthAuraFilterItem item && item.Kind == filter)
            {
                _lowestHealthAuraFilterBox.SelectedIndex = i;
                return;
            }
        }

        _lowestHealthAuraFilterBox.SelectedIndex = 0;
    }

    private void SelectSelector(UnitSelectorKind kind)
    {
        for (var i = 0; i < _selectorBox.Items.Count; i++)
        {
            if (_selectorBox.Items[i] is SelectorItem item && item.Kind == kind)
            {
                _selectorBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void SelectSelector(CountKind kind)
    {
        for (var i = 0; i < _selectorBox.Items.Count; i++)
        {
            if (_selectorBox.Items[i] is CountItem item && item.Kind == kind)
            {
                _selectorBox.SelectedIndex = i;
                return;
            }
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

    private void SelectDispelType(int dispelType)
    {
        for (var i = 0; i < _dispelTypeBox.Items.Count; i++)
        {
            if (_dispelTypeBox.Items[i] is DispelTypeOption option && option.Value == dispelType)
            {
                _dispelTypeBox.SelectedIndex = i;
                return;
            }
        }

        _dispelTypeBox.SelectedIndex = 0;
    }

    private static void SelectAura(ComboBox box, string? aura)
    {
        if (string.IsNullOrEmpty(aura))
        {
            return;
        }

        var index = box.Items.IndexOf(aura);
        if (index < 0)
        {
            box.Items.Add(aura);
            index = box.Items.Count - 1;
        }

        box.SelectedIndex = index;
    }

    private void CheckAuras(List<string>? auras)
    {
        if (auras is null)
        {
            return;
        }

        foreach (var aura in auras)
        {
            var index = _aurasBox.Items.IndexOf(aura);
            if (index < 0)
            {
                index = _aurasBox.Items.Add(aura);
            }

            _aurasBox.SetItemChecked(index, true);
        }
    }

    private static decimal Clamp(int value, NumericUpDown box)
    {
        return Math.Clamp(value, (int)box.Minimum, (int)box.Maximum);
    }

    private static Panel BuildLabeledRow(string label, Control control, int height = 32)
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
            Bounds = new Rectangle(0, 4, LabelWidth, 24),
            AutoEllipsis = true
        };

        control.Bounds = new Rectangle(ControlLeft, 3, RowWidth - ControlLeft, height - 6);
        panel.Controls.Add(control);
        panel.Controls.Add(labelControl);
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

        var labelAControl = new Label { Text = labelA, ForeColor = UiTheme.Muted, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(0, 5, 72, 28), AutoEllipsis = true };
        controlA.Bounds = new Rectangle(80, 5, 230, 28);
        var labelBControl = new Label { Text = labelB, ForeColor = UiTheme.Muted, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(330, 5, 130, 28), AutoEllipsis = true };
        controlB.Bounds = new Rectangle(466, 5, RowWidth - 466, 28);

        panel.Controls.Add(controlA);
        panel.Controls.Add(labelAControl);
        panel.Controls.Add(controlB);
        panel.Controls.Add(labelBControl);
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

        var nameLabel = new Label { Text = "名称", ForeColor = UiTheme.Muted, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(0, 5, 72, 28), AutoEllipsis = true };
        StyleTextBox(_nameBox);
        _nameBox.Bounds = new Rectangle(80, 5, 230, 28);
        _healthNameLabel.Text = "值名称";
        _healthNameLabel.ForeColor = UiTheme.Muted;
        _healthNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        _healthNameLabel.Bounds = new Rectangle(330, 5, 130, 28);
        _healthNameLabel.AutoEllipsis = true;
        StyleTextBox(_healthNameBox);
        _healthNameBox.Bounds = new Rectangle(466, 5, RowWidth - 466, 28);

        panel.Controls.Add(_nameBox);
        panel.Controls.Add(nameLabel);
        panel.Controls.Add(_healthNameBox);
        panel.Controls.Add(_healthNameLabel);
        return panel;
    }

    private static TextBox StyleTextBox(TextBox box)
    {
        UiTheme.StyleTextBox(box);
        return box;
    }

    private static void StyleNumeric(NumericUpDown box)
    {
        UiTheme.StyleNumericUpDown(box);
    }

    private sealed record SelectorItem(string Text, UnitSelectorKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record LowestHealthAuraFilterItem(string Text, LowestHealthAuraFilterKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record CountItem(string Text, CountKind Kind)
    {
        public override string ToString() => Text;
    }

    private sealed record RoleOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }

    private sealed record DispelTypeOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }

    private enum LowestHealthAuraFilterKind
    {
        None,
        WithAnyAura,
        WithoutAura,
        WithAura,
        WithAuraCount
    }
}
