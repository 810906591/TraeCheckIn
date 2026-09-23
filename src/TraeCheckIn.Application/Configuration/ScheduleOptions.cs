namespace TraeCheckIn.Application.Configuration;

/// <summary>每日定时触发配置</summary>
public sealed class ScheduleOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "Schedule";

    /// <summary>每日签到触发时间点列表（HH:mm 24 小时制），可配置多个时间点提高容错</summary>
    public string[] Times { get; set; } = ["09:30"];
}
