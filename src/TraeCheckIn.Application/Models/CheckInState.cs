using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Models;

/// <summary>签到持久化状态（用于跨进程重启后的当日去重判断）</summary>
public sealed class CheckInState
{
    /// <summary>最近一次完成签到的日期</summary>
    public DateOnly LastCheckInDate { get; set; }

    /// <summary>最近一次执行状态</summary>
    public CheckInStatus LastStatus { get; set; }

    /// <summary>最近一次结果摘要</summary>
    public string LastMessage { get; set; } = string.Empty;

    /// <summary>状态更新时间</summary>
    public DateTime UpdatedAt { get; set; }
}
