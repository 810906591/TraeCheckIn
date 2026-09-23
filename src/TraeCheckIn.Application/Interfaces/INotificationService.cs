using TraeCheckIn.Domain.Entities;

namespace TraeCheckIn.Application.Interfaces;

/// <summary>签到结果通知门面抽象（聚合各通知渠道并按配置过滤）</summary>
public interface INotificationService
{
    /// <summary>分发签到结果通知</summary>
    /// <param name="record">签到记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyCheckInAsync(CheckInRecord record, CancellationToken cancellationToken);
}
