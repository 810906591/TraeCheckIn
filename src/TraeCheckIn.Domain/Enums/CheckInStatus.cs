namespace TraeCheckIn.Domain.Enums;

/// <summary>签到结果状态</summary>
public enum CheckInStatus
{
    /// <summary>签到成功</summary>
    Success = 0,

    /// <summary>当日已签到（平台返回幂等结果）</summary>
    AlreadyCheckedIn = 1,

    /// <summary>签到失败</summary>
    Failed = 2,

    /// <summary>本次跳过执行（如检测到当日已完成签到）</summary>
    Skipped = 3
}
