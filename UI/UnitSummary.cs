namespace Shigure;

/// <summary>
/// 把动态单位 / 数量字段渲染成人类可读摘要 (如 "带[X]且血最低 (&lt;80)")。
/// 单位列表的"摘要"列与单位编辑器的实时预览共用同一套措辞, 避免两处描述漂移。
/// </summary>
internal static class UnitSummary
{
    public static string Describe(ModuleUnit unit, Func<long, string?>? resolveAuraName = null)
    {
        if (unit.FilterVersion == ModuleUnit.CurrentFilterVersion)
        {
            return DescribeFilteredUnit(unit, resolveAuraName);
        }

        var threshold = DescribeThreshold(
            unit.HealthThreshold,
            unit.HealthThresholdField,
            IsHealingAbsorbKind(unit.Kind) ? 0 : 100);
        var aura = unit.AuraSpellIds is { Count: > 0 } ? FormatAura(unit.AuraSpellIds[0], resolveAuraName) : "?";
        var auras = unit.AuraSpellIds is { Count: > 0 }
            ? string.Join("/", unit.AuraSpellIds.Select(id => FormatAura(id, resolveAuraName)))
            : "?";
        var dir = unit.Reverse ? "逆序" : "正序";
        var roleFilter = DescribeRoleFilter(unit);
        return roleFilter + (unit.Kind switch
        {
            UnitSelectorKind.LowestHealth => $"血量最低 (<{threshold})",
            UnitSelectorKind.LowestHealthWithAnyAura => $"带任一[{auras}]且血最低 (<{threshold})",
            UnitSelectorKind.LowestHealthWithoutAnyAura => $"不带任一[{auras}]且血最低 (<{threshold})",
            UnitSelectorKind.LowestHealthWithoutAura => $"不带[{aura}]且血最低 (<{threshold})",
            UnitSelectorKind.LowestHealthWithAura => $"带[{aura}]且血最低 (<{threshold})",
            UnitSelectorKind.LowestHealthWithAuraCount => $"[{aura}]={unit.AuraCount}且血最低 (<{threshold})",
            UnitSelectorKind.UnitWithRole => $"职责={unit.Role} {dir}首个",
            UnitSelectorKind.UnitWithRoleWithoutAura => $"职责={unit.Role}且不带[{aura}] {dir}",
            UnitSelectorKind.UnitWithAura => $"带[{aura}] 持续最久",
            UnitSelectorKind.UnitWithAuraShortest => $"带[{aura}] 持续最短",
            UnitSelectorKind.UnitWithDispelType => $"驱散类型={unit.DispelType}",
            UnitSelectorKind.HighestHealingAbsorb => $"治疗吸收最高 (>{threshold})",
            UnitSelectorKind.HighestHealingAbsorbWithAnyAura => $"带任一[{auras}]且治疗吸收最高 (>{threshold})",
            UnitSelectorKind.HighestHealingAbsorbWithoutAnyAura => $"不带任一[{auras}]且治疗吸收最高 (>{threshold})",
            UnitSelectorKind.HighestHealingAbsorbWithoutAura => $"不带[{aura}]且治疗吸收最高 (>{threshold})",
            UnitSelectorKind.HighestHealingAbsorbWithAura => $"带[{aura}]且治疗吸收最高 (>{threshold})",
            UnitSelectorKind.HighestHealingAbsorbWithAuraCount => $"[{aura}]={unit.AuraCount}且治疗吸收最高 (>{threshold})",
            _ => unit.Kind.ToString()
        });
    }

    public static string Describe(ModuleCountField count, Func<long, string?>? resolveAuraName = null)
        => DescribeCountGroups(count.FilterGroups, "队友人数", resolveAuraName);

    private static string DescribeFilteredUnit(
        ModuleUnit unit,
        Func<long, string?>? resolveAuraName)
    {
        var parts = new List<string>();
        if (unit.HealthFilter != EnemyThresholdFilterKind.None)
        {
            parts.Add($"血量{ThresholdOperator(unit.HealthFilter)}{DescribeThreshold(unit.HealthThreshold, unit.HealthThresholdField, 0)}");
        }

        if (unit.HealingAbsorbFilter != EnemyThresholdFilterKind.None)
        {
            parts.Add($"治疗吸收{ThresholdOperator(unit.HealingAbsorbFilter)}{DescribeThreshold(unit.HealingAbsorbThreshold, unit.HealingAbsorbThresholdField, 0)}");
        }

        if (unit.RoleFilter is not null)
        {
            parts.Add(unit.RoleFilter == UnitRoleFilterKind.Include
                ? $"职责={unit.Role}"
                : $"职责!={unit.Role}");
        }

        if (unit.DispelFilter != AllyDispelFilterKind.None)
        {
            parts.Add(unit.DispelFilter == AllyDispelFilterKind.WithType
                ? $"驱散类型={unit.DispelType}"
                : $"驱散类型!={unit.DispelType}");
        }

        var ids = unit.AuraSpellIds ?? [];
        var aura = ids.Count > 0 ? FormatAura(ids[0], resolveAuraName) : "?";
        var auras = ids.Count > 0
            ? string.Join("/", ids.Select(id => FormatAura(id, resolveAuraName)))
            : "?";
        if (unit.AuraFilter != EnemyAuraFilterKind.None)
        {
            parts.Add(unit.AuraFilter switch
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

        if (unit.AuraDurationFilter != AuraDurationFilterKind.None)
        {
            var durationAuraId = unit.AuraDurationSpellId
                ?? (ids.Count > 0 ? ids[0] : (long?)null);
            var durationAura = durationAuraId is { } id
                ? FormatAura(id, resolveAuraName)
                : "?";
            var durationText = unit.AuraDurationFilter switch
            {
                AuraDurationFilterKind.Longest => $"[{durationAura}]持续最长",
                AuraDurationFilterKind.Shortest => $"[{durationAura}]持续最短",
                AuraDurationFilterKind.Above => $"[{durationAura}]持续时间>{DescribeThreshold(unit.AuraDurationThreshold, unit.AuraDurationThresholdField, 0)}",
                AuraDurationFilterKind.Below => $"[{durationAura}]持续时间<{DescribeThreshold(unit.AuraDurationThreshold, unit.AuraDurationThresholdField, 0)}",
                AuraDurationFilterKind.Equal => $"[{durationAura}]持续时间={DescribeThreshold(unit.AuraDurationThreshold, unit.AuraDurationThresholdField, 0)}",
                _ => string.Empty
            };
            parts.Add(durationText);
        }

        var selector = unit.Kind switch
        {
            UnitSelectorKind.LowestHealth => "生命值最低",
            UnitSelectorKind.HighestHealingAbsorb => "治疗吸收最高",
            UnitSelectorKind.UnitWithRole => unit.Reverse ? "逆序首个" : "正序首个",
            UnitSelectorKind.UnitWithAura => $"[{aura}]持续最久",
            UnitSelectorKind.UnitWithAuraShortest => $"[{aura}]持续最短",
            _ => unit.Kind.ToString()
        };
        return parts.Count == 0 ? selector : $"{string.Join("且", parts)} → {selector}";
    }

    public static string Describe(ModuleEnemyCountField count, Func<long, string?>? resolveAuraName = null)
        => DescribeCountGroups(count.FilterGroups, "敌人数", resolveAuraName);

    private static string DescribeCountGroups(
        IReadOnlyList<ModuleCountConditionGroup>? groups,
        string suffix,
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

        return descriptions.Count == 0 ? suffix : $"{string.Join(" 且 ", descriptions)} 的{suffix}";
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

    private static string DescribeRoleFilter(ModuleUnit unit)
    {
        if (!IsLowestHealthKind(unit.Kind) || unit.RoleFilter is null)
        {
            return string.Empty;
        }

        return unit.RoleFilter == UnitRoleFilterKind.Include
            ? $"职责={unit.Role}且"
            : $"职责!={unit.Role}且";
    }

    private static bool IsLowestHealthKind(UnitSelectorKind kind)
        => kind is UnitSelectorKind.LowestHealth
            or UnitSelectorKind.LowestHealthWithAnyAura
            or UnitSelectorKind.LowestHealthWithoutAnyAura
            or UnitSelectorKind.LowestHealthWithoutAura
            or UnitSelectorKind.LowestHealthWithAura
            or UnitSelectorKind.LowestHealthWithAuraCount;

    private static bool IsHealingAbsorbKind(UnitSelectorKind kind)
        => kind is UnitSelectorKind.HighestHealingAbsorb
            or UnitSelectorKind.HighestHealingAbsorbWithAnyAura
            or UnitSelectorKind.HighestHealingAbsorbWithoutAnyAura
            or UnitSelectorKind.HighestHealingAbsorbWithoutAura
            or UnitSelectorKind.HighestHealingAbsorbWithAura
            or UnitSelectorKind.HighestHealingAbsorbWithAuraCount;

}
