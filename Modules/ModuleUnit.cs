namespace Shigure;

/// <summary>队友单位最终选取时比较的目标字段。</summary>
public enum UnitTargetFieldKind
{
    /// <summary>生命值：最低 / 最高。</summary>
    Health,

    /// <summary>治疗吸收：最低 / 最高。</summary>
    HealingAbsorb,

    /// <summary>职责：正序 / 倒序（按队伍槽位）。</summary>
    Role,

    /// <summary>驱散：正序 / 倒序（按队伍槽位）。</summary>
    Dispel,

    /// <summary>光环剩余时间：最长 / 最短。</summary>
    Aura
}

/// <summary>队友单位在筛选后的唯一选取方式。</summary>
public enum UnitSelectionMode
{
    /// <summary>数值最低（生命值 / 治疗吸收）。</summary>
    Lowest,

    /// <summary>数值最高（生命值 / 治疗吸收）。</summary>
    Highest,

    /// <summary>正序：取最小队伍槽位（职责 / 驱散）。</summary>
    Ascending,

    /// <summary>倒序：取最大队伍槽位（职责 / 驱散）。</summary>
    Descending,

    /// <summary>目标光环剩余时间最长。</summary>
    Longest,

    /// <summary>目标光环剩余时间最短。</summary>
    Shortest
}

/// <summary>
/// 模块内定义的命名动态单位。运行时先按条件组筛选有效队伍单位，
/// 再按目标字段选出唯一槽位("1".."40")。
/// </summary>
public sealed class ModuleUnit
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 可选的值名称：非空时把所选单位的目标字段值暴露成同名数值条件字段。
    /// </summary>
    public string? ValueName { get; set; }

    /// <summary>与队友数量共用的条件组列表；组内全部/任一，组间按且。</summary>
    public List<ModuleCountConditionGroup> FilterGroups { get; set; } = new();

    /// <summary>筛选完成后用于比较/排序的目标字段。</summary>
    public UnitTargetFieldKind TargetField { get; set; } = UnitTargetFieldKind.Health;

    /// <summary>目标字段对应的选取方式。</summary>
    public UnitSelectionMode SelectionMode { get; set; } = UnitSelectionMode.Lowest;

    /// <summary>目标字段为光环时必须指定的队伍光环 spellId。</summary>
    public long? TargetAuraSpellId { get; set; }

    public ModuleUnit Clone()
    {
        return new ModuleUnit
        {
            Name = Name,
            ValueName = ValueName,
            FilterGroups = FilterGroups.Select(group => group.Clone()).ToList(),
            TargetField = TargetField,
            SelectionMode = SelectionMode,
            TargetAuraSpellId = TargetAuraSpellId
        };
    }
}

/// <summary>敌人数量的阈值筛选方式(生命值 / 距离共用)。</summary>
public enum EnemyThresholdFilterKind
{
    /// <summary>不筛选。</summary>
    None,

    /// <summary>大于阈值。</summary>
    Above,

    /// <summary>小于阈值。</summary>
    Below
}

/// <summary>敌人数量的战斗状态筛选方式(对应插件的 UnitAffectingCombat)。</summary>
public enum EnemyCombatFilterKind
{
    /// <summary>不筛选。</summary>
    None,

    /// <summary>战斗中。</summary>
    InCombat,

    /// <summary>不在战斗中。</summary>
    NotInCombat
}

/// <summary>单位与数量字段的光环筛选方式。</summary>
public enum EnemyAuraFilterKind
{
    /// <summary>不筛选。</summary>
    None,

    /// <summary>带指定光环。</summary>
    WithAura,

    /// <summary>不带指定光环。</summary>
    WithoutAura,

    /// <summary>至少带有所选任一光环；至少需要两个光环。</summary>
    HasAnyAura,

    /// <summary>同时带有所选全部光环；至少需要两个光环。</summary>
    HasAllAuras,

    /// <summary>至少缺少一个所选光环；至少需要两个光环。</summary>
    MissingAnyAura,

    /// <summary>所选光环全部不存在；至少需要两个光环。</summary>
    MissingAllAuras
}

/// <summary>数量筛选条件组的匹配方式。</summary>
public enum CountConditionGroupMode
{
    All,
    Any
}

/// <summary>数量筛选可读取的单位字段。</summary>
public enum CountConditionFieldKind
{
    Health,
    HealingAbsorb,
    Role,
    Dispel,
    Range,
    Combat,
    Aura
}

/// <summary>数量筛选的比较方式。</summary>
public enum CountConditionComparisonKind
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

/// <summary>数量筛选右值的来源。</summary>
public enum CountConditionValueKind
{
    Constant,
    StateField
}

/// <summary>一条通用数量筛选条件。</summary>
public sealed class ModuleCountCondition
{
    public bool Enabled { get; set; } = true;
    public CountConditionFieldKind Field { get; set; }
    public long? AuraSpellId { get; set; }
    public CountConditionComparisonKind Comparison { get; set; }
    public CountConditionValueKind ValueKind { get; set; }
    public int Value { get; set; }
    public string? ValueField { get; set; }

    public ModuleCountCondition Clone()
    {
        return new ModuleCountCondition
        {
            Enabled = Enabled,
            Field = Field,
            AuraSpellId = AuraSpellId,
            Comparison = Comparison,
            ValueKind = ValueKind,
            Value = Value,
            ValueField = ValueField
        };
    }
}

/// <summary>一组通用数量筛选条件；组内按 Mode 组合，组与组之间始终按且组合。</summary>
public sealed class ModuleCountConditionGroup
{
    public CountConditionGroupMode Mode { get; set; } = CountConditionGroupMode.All;
    public List<ModuleCountCondition> Conditions { get; set; } = new();

    public ModuleCountConditionGroup Clone()
    {
        return new ModuleCountConditionGroup
        {
            Mode = Mode,
            Conditions = Conditions.Select(condition => condition.Clone()).ToList()
        };
    }
}

/// <summary>最低生命值选择器使用的职责筛选方式。</summary>
public enum UnitRoleFilterKind
{
    /// <summary>只包含指定职责。</summary>
    Include,

    /// <summary>排除指定职责。</summary>
    Exclude
}

/// <summary>
/// 模块内定义的命名敌人数量字段。统计姓名板(nameplates)中满足筛选条件的敌人数,
/// 仅用于条件(如 近身敌人数 &gt;= 3), 不能作为目标。
/// </summary>
public sealed class ModuleEnemyCountField
{
    public string Name { get; set; } = string.Empty;
    public List<ModuleCountConditionGroup> FilterGroups { get; set; } = new();

    public ModuleEnemyCountField Clone()
    {
        return new ModuleEnemyCountField
        {
            Name = Name,
            FilterGroups = FilterGroups.Select(group => group.Clone()).ToList()
        };
    }
}

/// <summary>平均血量的统计对象。</summary>
public enum AverageHealthTargetKind
{
    Allies,
    Enemies
}

/// <summary>
/// 模块内定义的命名平均血量字段。队友使用生命值 / 光环 / 职责筛选，
/// 敌人使用生命值 / 光环 / 距离 / 战斗筛选；无匹配单位时结果为 0。
/// </summary>
public sealed class ModuleAverageHealthField
{
    public string Name { get; set; } = string.Empty;
    public AverageHealthTargetKind Target { get; set; } = AverageHealthTargetKind.Allies;

    public EnemyThresholdFilterKind HealthFilter { get; set; } = EnemyThresholdFilterKind.None;
    public int? HealthThreshold { get; set; }
    public string? HealthThresholdField { get; set; }

    public EnemyAuraFilterKind AuraFilter { get; set; } = EnemyAuraFilterKind.None;
    public List<long>? AuraSpellIds { get; set; }

    public UnitRoleFilterKind? RoleFilter { get; set; }
    public int? Role { get; set; }

    public EnemyThresholdFilterKind RangeFilter { get; set; } = EnemyThresholdFilterKind.None;
    public int? RangeThreshold { get; set; }
    public string? RangeThresholdField { get; set; }

    public EnemyCombatFilterKind CombatFilter { get; set; } = EnemyCombatFilterKind.None;

    public ModuleAverageHealthField Clone()
    {
        return new ModuleAverageHealthField
        {
            Name = Name,
            Target = Target,
            HealthFilter = HealthFilter,
            HealthThreshold = HealthThreshold,
            HealthThresholdField = HealthThresholdField,
            AuraFilter = AuraFilter,
            AuraSpellIds = AuraSpellIds is null ? null : new List<long>(AuraSpellIds),
            RoleFilter = RoleFilter,
            Role = Role,
            RangeFilter = RangeFilter,
            RangeThreshold = RangeThreshold,
            RangeThresholdField = RangeThresholdField,
            CombatFilter = CombatFilter
        };
    }
}

/// <summary>
/// 模块内定义的命名数量字段。仅用于条件(如 低血量人数 &gt;= 3), 不能作为目标。
/// </summary>
public sealed class ModuleCountField
{
    public string Name { get; set; } = string.Empty;
    public List<ModuleCountConditionGroup> FilterGroups { get; set; } = new();

    public ModuleCountField Clone()
    {
        return new ModuleCountField
        {
            Name = Name,
            FilterGroups = FilterGroups.Select(group => group.Clone()).ToList()
        };
    }
}
