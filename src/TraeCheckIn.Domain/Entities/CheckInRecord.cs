using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Domain.Entities;

/// <summary>签到执行记录（一次签到动作的完整快照，用于日志与通知）</summary>
public sealed class CheckInRecord
{
    /// <summary>签到执行时间（本地时间）</summary>
    public DateTime Time { get; init; } = DateTime.Now;

    /// <summary>签到结果状态</summary>
    public CheckInStatus Status { get; init; }

    /// <summary>结果摘要信息</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>详细信息（原始响应或异常内容），用于日志与通知</summary>
    public string? Detail { get; init; }
}
