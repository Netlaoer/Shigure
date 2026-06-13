using System.Globalization;

namespace Shigure;

internal static class ModuleSpecialActions
{
    public const string PauseSpell = "暂停";
    public const string FailedSpell = "失败法术";

    private static readonly Dictionary<int, string> FailedSpellMap = new()
    {
        [1] = "心灵尖啸",
        [2] = "群体驱散",
        [3] = "真言术：障",
        [4] = "终极苦修",
        [5] = "神圣化身",
        [6] = "光晕",
        [7] = "神圣赞美诗",
        [8] = "虚空形态",
        [9] = "吸血鬼的拥抱",
        [30] = "真言术：耀",
        [35] = "福音"
    };

    public static IReadOnlyCollection<string> FailedSpellNames => FailedSpellMap.Values;

    public static bool IsPauseSpell(string? spell)
    {
        return string.Equals(spell?.Trim(), PauseSpell, StringComparison.Ordinal);
    }

    public static bool IsFailedSpell(string? spell)
    {
        return string.Equals(spell?.Trim(), FailedSpell, StringComparison.Ordinal);
    }

    public static string? GetFailedSpell(GameState state)
    {
        var failedSpellId = state.GetInt("法术失败");
        if (!FailedSpellMap.TryGetValue(failedSpellId, out var spellName))
        {
            return null;
        }

        return state.Spells.TryGetValue(spellName, out var cooldown)
            && IsZero(cooldown)
                ? spellName
                : null;
    }

    private static bool IsZero(object? value)
    {
        return value switch
        {
            int i => i == 0,
            long l => l == 0,
            float f => Math.Abs(f) < float.Epsilon,
            double d => Math.Abs(d) < double.Epsilon,
            decimal m => m == 0,
            bool b => !b,
            string s => TryParseZero(s, out var isZero) && isZero,
            _ => false
        };
    }

    private static bool TryParseZero(string text, out bool isZero)
    {
        isZero = false;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            return false;
        }

        isZero = Math.Abs(parsed) < double.Epsilon;
        return true;
    }
}
