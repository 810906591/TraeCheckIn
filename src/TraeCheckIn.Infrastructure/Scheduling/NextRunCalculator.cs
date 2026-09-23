namespace TraeCheckIn.Infrastructure.Scheduling;

/// <summary>每日定时触发时间计算器：根据配置的时间点推算下一次触发时刻</summary>
public sealed class NextRunCalculator
{
    /// <summary>计算下一次触发时刻（严格晚于 <paramref name="now"/>）</summary>
    /// <param name="now">当前时间</param>
    /// <param name="dailyTimes">每日触发时间点集合</param>
    /// <returns>下一次触发时刻；时间点集合为空时返回 null</returns>
    public DateTime? GetNextOccurrence(DateTime now, IReadOnlyList<TimeOnly> dailyTimes)
    {
        if (dailyTimes.Count == 0)
        {
            return null;
        }

        // 今日尚未过期的时间点中取最早的一个
        foreach (var time in dailyTimes.OrderBy(t => t))
        {
            var candidate = now.Date + time.ToTimeSpan();
            if (candidate > now)
            {
                return candidate;
            }
        }

        // 今日时间点均已过，取明日最早时间点
        return now.Date.AddDays(1) + dailyTimes.Min().ToTimeSpan();
    }
}
