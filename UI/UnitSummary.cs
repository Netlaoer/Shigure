namespace Shigure;

/// <summary>
/// 把动态单位 / 数量字段渲染成人类可读摘要 (如 "(生命值&lt;80 且 [恢复]!=0) → 生命值最低")。
/// 单位列表的"摘要"列与单位编辑器的实时预览共用同一套措辞, 避免两处描述漂移。
/// </summary>
internal static class UnitSummary
{
    public static string Describe(ModuleUnit unit, Func<long, string?>? resolveAuraName = null)
    {
        var filterText = DescribeFilterGroupsBody(unit.FilterGroups, resolveAuraName);
        var selection = DescribeUnitSelection(unit, resolveAuraName);
        return string.IsNullOrEmpty(filterText)
            ? selection
            : $"{filterText} → {selection}";
    }

    public static string Describe(ModuleCountField count, Func<long, string?>? resolveAuraName = null)
        => DescribeCountGroups(count.FilterGroups, "队友人数", resolveAuraName);

    public static string Describe(ModuleEnemyCountField count, Func<long, string?>? resolveAuraName = null)
        => DescribeCountGroups(count.FilterGroups, "敌人数", resolveAuraName);

    public static string DescribeTargetField(UnitTargetFieldKind field)
        => field switch
        {
            UnitTargetFieldKind.Health => "生命值",
            UnitTargetFieldKind.HealingAbsorb => "治疗吸收",
            UnitTargetFieldKind.Role => "职责",
            UnitTargetFieldKind.Dispel => "驱散",
            UnitTargetFieldKind.Aura => "光环",
            _ => "?"
        };

    public static string DescribeSelectionMode(UnitTargetFieldKind field, UnitSelectionMode mode)
        => (field, mode) switch
        {
            (UnitTargetFieldKind.Health or UnitTargetFieldKind.HealingAbsorb, UnitSelectionMode.Lowest) => "最低",
            (UnitTargetFieldKind.Health or UnitTargetFieldKind.HealingAbsorb, UnitSelectionMode.Highest) => "最高",
            (UnitTargetFieldKind.Role or UnitTargetFieldKind.Dispel, UnitSelectionMode.Ascending) => "正序",
            (UnitTargetFieldKind.Role or UnitTargetFieldKind.Dispel, UnitSelectionMode.Descending) => "倒序",
            (UnitTargetFieldKind.Aura, UnitSelectionMode.Longest) => "最长",
            (UnitTargetFieldKind.Aura, UnitSelectionMode.Shortest) => "最短",
            _ => mode.ToString()
        };

    private static string DescribeUnitSelection(ModuleUnit unit, Func<long, string?>? resolveAuraName)
    {
        var field = DescribeTargetField(unit.TargetField);
        var mode = DescribeSelectionMode(unit.TargetField, unit.SelectionMode);
        if (unit.TargetField == UnitTargetFieldKind.Aura)
        {
            var aura = unit.TargetAuraSpellId is { } id
                ? FormatAura(id, resolveAuraName)
                : "?";
            return $"[{aura}]{mode}";
        }

        return $"{field}{mode}";
    }

    private static string DescribeCountGroups(
        IReadOnlyList<ModuleCountConditionGroup>? groups,
        string suffix,
        Func<long, string?>? resolveAuraName)
    {
        var body = DescribeFilterGroupsBody(groups, resolveAuraName);
        return string.IsNullOrEmpty(body) ? suffix : $"{body} 的{suffix}";
    }

    private static string DescribeFilterGroupsBody(
        IReadOnlyList<ModuleCountConditionGroup>? groups,
        Func<long, string?>? resolveAuraName)
    {
        var descriptions = new List<string>();
        foreach (var group in groups ?? [])
        {
            var enabled = (group.Conditions ?? []).Where(condition => condition.Enabled).ToArray();
            if (enabled.Length == 0)
            {
                continue;
            }

            var separator = group.Mode == CountConditionGroupMode.Any ? " 或 " : " 且 ";
            var body = string.Join(separator, enabled.Select(condition => DescribeCountCondition(condition, resolveAuraName)));
            descriptions.Add(enabled.Length > 1 ? $"({body})" : body);
        }

        if (descriptions.Count == 0)
        {
            return string.Empty;
        }

        // 多组时整体加括号，使 “→ 目标选择” 的关系更清晰。
        var joined = string.Join(" 且 ", descriptions);
        return descriptions.Count > 1 || joined.StartsWith('(') ? joined : $"({joined})";
    }

    private static string DescribeCountCondition(
        ModuleCountCondition condition,
        Func<long, string?>? resolveAuraName)
    {
        var field = condition.Field switch
        {
            CountConditionFieldKind.Health => "生命值",
            CountConditionFieldKind.HealingAbsorb => "治疗吸收",
            CountConditionFieldKind.Role => "职责",
            CountConditionFieldKind.Dispel => "驱散",
            CountConditionFieldKind.Range => "距离",
            CountConditionFieldKind.Combat => "战斗",
            CountConditionFieldKind.Aura => $"[{FormatAura(condition.AuraSpellId.GetValueOrDefault(), resolveAuraName)}]",
            _ => "?"
        };
        var value = condition.ValueKind == CountConditionValueKind.StateField
            ? $"[{condition.ValueField}]"
            : DescribeCountValue(condition);
        return $"{field}{CountComparisonOperator(condition.Comparison)}{value}";
    }

    private static string DescribeCountValue(ModuleCountCondition condition)
    {
        return condition.Field switch
        {
            CountConditionFieldKind.Role => condition.Value switch
            {
                1 => "坦克",
                2 => "治疗",
                3 => "输出",
                _ => condition.Value.ToString()
            },
            CountConditionFieldKind.Combat => condition.Value == 0 ? "不在战斗中" : "战斗中",
            _ => condition.Value.ToString()
        };
    }

    private static string CountComparisonOperator(CountConditionComparisonKind comparison)
        => comparison switch
        {
            CountConditionComparisonKind.Equal => "==",
            CountConditionComparisonKind.NotEqual => "!=",
            CountConditionComparisonKind.GreaterThan => ">",
            CountConditionComparisonKind.LessThan => "<",
            CountConditionComparisonKind.GreaterThanOrEqual => ">=",
            CountConditionComparisonKind.LessThanOrEqual => "<=",
            _ => "?"
        };

    public static string Describe(ModuleAverageHealthField field, Func<long, string?>? resolveAuraName = null)
    {
        var parts = new List<string>();
        if (field.HealthFilter != EnemyThresholdFilterKind.None)
        {
            parts.Add($"血量{ThresholdOperator(field.HealthFilter)}{DescribeThreshold(field.HealthThreshold, field.HealthThresholdField, 0)}");
        }

        if (field.AuraFilter != EnemyAuraFilterKind.None)
        {
            var ids = field.AuraSpellIds ?? [];
            var aura = ids.Count > 0 ? FormatAura(ids[0], resolveAuraName) : "?";
            var auras = ids.Count > 0
                ? string.Join("/", ids.Select(id => FormatAura(id, resolveAuraName)))
                : "?";
            parts.Add(field.AuraFilter switch
            {
                EnemyAuraFilterKind.WithAura => $"带[{aura}]",
                EnemyAuraFilterKind.WithoutAura => $"不带[{aura}]",
                EnemyAuraFilterKind.HasAnyAura => $"拥有任一[{auras}]",
                EnemyAuraFilterKind.HasAllAuras => $"拥有全部[{auras}]",
                EnemyAuraFilterKind.MissingAnyAura => $"缺少任一[{auras}]",
                EnemyAuraFilterKind.MissingAllAuras => $"缺少全部[{auras}]",
                _ => string.Empty
            });

        }

        if (field.Target == AverageHealthTargetKind.Allies && field.RoleFilter is not null)
        {
            parts.Add(field.RoleFilter == UnitRoleFilterKind.Include
                ? $"职责={field.Role}"
                : $"职责!={field.Role}");
        }

        if (field.Target == AverageHealthTargetKind.Enemies)
        {
            if (field.RangeFilter != EnemyThresholdFilterKind.None)
            {
                parts.Add($"距离{ThresholdOperator(field.RangeFilter)}{DescribeThreshold(field.RangeThreshold, field.RangeThresholdField, 0)}");
            }

            if (field.CombatFilter != EnemyCombatFilterKind.None)
            {
                parts.Add(field.CombatFilter == EnemyCombatFilterKind.InCombat ? "战斗中" : "不在战斗中");
            }
        }

        var target = field.Target == AverageHealthTargetKind.Enemies ? "敌人" : "队友";
        return parts.Count == 0
            ? $"{target}平均血量"
            : $"{string.Join("且", parts)} 的{target}平均血量";
    }

    private static string ThresholdOperator(EnemyThresholdFilterKind filter)
        => filter == EnemyThresholdFilterKind.Above ? ">" : "<";

    private static string FormatAura(long spellId, Func<long, string?>? resolveAuraName)
    {
        var name = resolveAuraName?.Invoke(spellId);
        return string.IsNullOrWhiteSpace(name) ? spellId.ToString() : $"{name} / {spellId}";
    }

    private static string DescribeThreshold(int? fixedValue, string? field, int defaultValue = 100)
    {
        return string.IsNullOrWhiteSpace(field)
            ? (fixedValue ?? defaultValue).ToString()
            : $"动态:{field.Trim()}";
    }
}
