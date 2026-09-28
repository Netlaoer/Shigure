namespace Shigure;

/// <summary>
/// 为每个姓名板槽位维护最近 5 秒生命值样本，用线性回归估算掉血速度并计算 TTD。
/// 目标/焦点/首领 TTD 只转发映射槽位结果，不维护第二份历史。
/// </summary>
internal sealed class NameplateTtdTracker
{
    private const double HistorySeconds = 5;
    private const double MinSpanSeconds = 1;

    private readonly Dictionary<int, List<HpSample>> _history = new();

    public void Clear() => _history.Clear();

    public void Apply(GameState state, DateTimeOffset now)
    {
        var nameplates = EnsureMutableNameplates(state);
        for (var slot = 1; slot <= NameplateStateLayout.SlotCount; slot++)
        {
            var key = slot.ToString();
            if (!nameplates.TryGetValue(key, out var plate) || plate is not Dictionary<string, object?> values)
            {
                ClearSlot(slot);
                continue;
            }

            var present = values.TryGetValue("存在", out var presentObj) && presentObj is true;
            var health = values.TryGetValue("生命值", out var healthObj) && healthObj is int hp ? hp : 0;
            if (!present || health <= 0)
            {
                ClearSlot(slot);
                values.Remove("TTD");
                continue;
            }

            var ttd = UpdateSlot(slot, health, now);
            if (ttd is int seconds)
            {
                values["TTD"] = seconds;
            }
            else
            {
                values.Remove("TTD");
            }
        }

        ForwardUnitAliases(state, nameplates);
    }

    private void ForwardUnitAliases(
        GameState state,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> nameplates)
    {
        foreach (var alias in NameplateStateLayout.UnitTtdAliases)
        {
            state.Values.Remove(alias.TtdField);
            var mappedSlot = state.GetInt(alias.MappingField, 0);
            if (mappedSlot is < 1 or > NameplateStateLayout.SlotCount)
            {
                continue;
            }

            if (!nameplates.TryGetValue(mappedSlot.ToString(), out var plate)
                || !plate.TryGetValue("TTD", out var ttd)
                || ttd is not int seconds)
            {
                continue;
            }

            state.Values[alias.TtdField] = seconds;
        }
    }

    private int? UpdateSlot(int slot, int health, DateTimeOffset now)
    {
        if (!_history.TryGetValue(slot, out var samples))
        {
            samples = [];
            _history[slot] = samples;
        }

        samples.Add(new HpSample(now, health));
        var cutoff = now - TimeSpan.FromSeconds(HistorySeconds);
        samples.RemoveAll(sample => sample.At < cutoff);
        return TryComputeTtd(samples, health);
    }

    private void ClearSlot(int slot) => _history.Remove(slot);

    private static int? TryComputeTtd(IReadOnlyList<HpSample> samples, int currentHealth)
    {
        if (samples.Count < 2 || currentHealth <= 0)
        {
            return null;
        }

        var first = samples[0];
        var last = samples[^1];
        var spanSeconds = (last.At - first.At).TotalSeconds;
        if (spanSeconds < MinSpanSeconds)
        {
            return null;
        }

        // 净掉血：窗口首尾生命值必须下降；回血会计入净趋势，持平或上升则不可用。
        if (last.Health >= first.Health)
        {
            return null;
        }

        double n = samples.Count;
        double sumX = 0;
        double sumY = 0;
        double sumXy = 0;
        double sumXx = 0;
        var origin = first.At;
        foreach (var sample in samples)
        {
            var x = (sample.At - origin).TotalSeconds;
            var y = sample.Health;
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumXx += x * x;
        }

        var denominator = n * sumXx - sumX * sumX;
        if (Math.Abs(denominator) < double.Epsilon)
        {
            return null;
        }

        var slope = (n * sumXy - sumX * sumY) / denominator;
        if (slope >= 0)
        {
            return null;
        }

        var dps = -slope;
        if (dps <= double.Epsilon)
        {
            return null;
        }

        return (int)Math.Ceiling(currentHealth / dps);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, object?>> EnsureMutableNameplates(GameState state)
    {
        if (state.Values.TryGetValue("nameplates", out var existing)
            && existing is Dictionary<string, IReadOnlyDictionary<string, object?>> current)
        {
            return current;
        }

        var created = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        state.Values["nameplates"] = created;
        return created;
    }

    private readonly record struct HpSample(DateTimeOffset At, int Health);
}
