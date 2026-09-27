namespace Shigure;

/// <summary>
/// 动态单位选择器类型, 对应 utils.py 中返回单位槽位的函数。
/// </summary>
public enum UnitSelectorKind
{
    /// <summary>生命值最低的单位。get_lowest_health_unit</summary>
    LowestHealth,

    /// <summary>拥有任一光环且生命值最低。get_lowest_health_unit_with_any_aura</summary>
    LowestHealthWithAnyAura,

    /// <summary>不拥有所选任一光环且生命值最低。</summary>
    LowestHealthWithoutAnyAura,

    /// <summary>不带某光环且生命值最低。get_lowest_health_unit_without_aura</summary>
    LowestHealthWithoutAura,

    /// <summary>带某光环且生命值最低。get_lowest_health_unit_with_aura</summary>
    LowestHealthWithAura,

    /// <summary>某光环值等于指定值且生命值最低。get_lowest_health_unit_with_aura_count</summary>
    LowestHealthWithAuraCount,

    /// <summary>按职责取首个/逆序首个。get_unit_with_role</summary>
    UnitWithRole,

    /// <summary>按职责且不带某光环取首个/逆序首个。get_unit_with_role_and_without_aura_name</summary>
    UnitWithRoleWithoutAura,

    /// <summary>带某光环(取持续最久)。get_unit_with_aura</summary>
    UnitWithAura,

    /// <summary>带某光环(取持续最短)。</summary>
    UnitWithAuraShortest,

    /// <summary>带某驱散类型的首个单位。get_unit_with_dispel_type</summary>
    UnitWithDispelType,

    /// <summary>治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorb,

    /// <summary>拥有任一光环、治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorbWithAnyAura,

    /// <summary>不拥有所选任一光环、治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorbWithoutAnyAura,

    /// <summary>不带某光环、治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorbWithoutAura,

    /// <summary>带某光环、治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorbWithAura,

    /// <summary>某光环值等于指定值、治疗吸收高于阈值且治疗吸收最高的单位。</summary>
    HighestHealingAbsorbWithAuraCount
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
/// 模块内定义的命名动态单位。运行时由 <see cref="UnitSelector"/> 解析为 group 槽位("1".."40")。
/// 单光环类用 AuraSpellIds[0]；多光环类使用整个列表。
/// </summary>
public sealed class ModuleUnit
{
    public const int CurrentFilterVersion = 3;

    public string Name { get; set; } = string.Empty;
    public int? FilterVersion { get; set; }

    /// <summary>
    /// 可选的"生命值名": 非空时把该单位解析出槽位的 生命值 暴露成一个同名数值条件字段,
    /// 例如取名 最低血量 后条件里可直接写 最低血量 &lt; 50, 等价于 单位名.生命值 &lt; 50。
    /// </summary>
    public string? HealthName { get; set; }

    public UnitSelectorKind Kind { get; set; } = UnitSelectorKind.LowestHealth;
    public EnemyThresholdFilterKind HealthFilter { get; set; } = EnemyThresholdFilterKind.None;
    public int? HealthThreshold { get; set; }
    public string? HealthThresholdField { get; set; }
    public EnemyThresholdFilterKind HealingAbsorbFilter { get; set; } = EnemyThresholdFilterKind.None;
    public int? HealingAbsorbThreshold { get; set; }
    public string? HealingAbsorbThresholdField { get; set; }
    public UnitRoleFilterKind? RoleFilter { get; set; }
    public int? Role { get; set; }
    public bool Reverse { get; set; }
    public EnemyAuraFilterKind AuraFilter { get; set; } = EnemyAuraFilterKind.None;
    public List<long>? AuraSpellIds { get; set; }
    public AuraDurationFilterKind AuraDurationFilter { get; set; } = AuraDurationFilterKind.None;
    public long? AuraDurationSpellId { get; set; }
    public int? AuraDurationThreshold { get; set; }
    public string? AuraDurationThresholdField { get; set; }
    // 仅用于读取并迁移旧模块；当前版本保存前必须转换并清空。
    public List<string>? AuraNames { get; set; }
    public int? AuraCount { get; set; }
    public AllyDispelFilterKind DispelFilter { get; set; } = AllyDispelFilterKind.None;
    public int? DispelType { get; set; }

    public ModuleUnit Clone()
    {
        return new ModuleUnit
        {
            Name = Name,
            FilterVersion = FilterVersion,
            HealthName = HealthName,
            Kind = Kind,
            HealthFilter = HealthFilter,
            HealthThreshold = HealthThreshold,
            HealthThresholdField = HealthThresholdField,
            HealingAbsorbFilter = HealingAbsorbFilter,
            HealingAbsorbThreshold = HealingAbsorbThreshold,
            HealingAbsorbThresholdField = HealingAbsorbThresholdField,
            RoleFilter = RoleFilter,
            Role = Role,
            Reverse = Reverse,
            AuraFilter = AuraFilter,
            AuraSpellIds = AuraSpellIds is null ? null : new List<long>(AuraSpellIds),
            AuraDurationFilter = AuraDurationFilter,
            AuraDurationSpellId = AuraDurationSpellId,
            AuraDurationThreshold = AuraDurationThreshold,
            AuraDurationThresholdField = AuraDurationThresholdField,
            AuraNames = AuraNames is null ? null : new List<string>(AuraNames),
            AuraCount = AuraCount,
            DispelFilter = DispelFilter,
            DispelType = DispelType
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

/// <summary>队友驱散类型筛选方式。</summary>
public enum AllyDispelFilterKind
{
    None,
    WithType,
    WithoutType
}

/// <summary>光环持续时间筛选方式。</summary>
public enum AuraDurationFilterKind
{
    None,
    // 兼容短暂使用过的阈值式持续时间筛选；新编辑器不再创建这三种值。
    Above,
    Below,
    Equal,
    Longest,
    Shortest
}

/// <summary>
/// 模块内定义的命名敌人数量字段。统计姓名板(nameplates)中满足筛选条件的敌人数,
/// 仅用于条件(如 近身敌人数 &gt;= 3), 不能作为目标。
/// 四组筛选(生命值 / 光环 / 距离 / 战斗)彼此独立, 同时生效时是「且」关系;
/// 无论如何都只统计距离 &gt; 0 的敌人。
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
