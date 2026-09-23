using TraeCheckIn.Domain.Entities;

namespace TraeCheckIn.Application.Interfaces;

/// <summary>单一签到结果通知渠道抽象（日志文件、邮件等具体实现）</summary>
public interface ICheckInNotifier
{
    /// <summary>发送签到结果通知</summary>
    /// <param name="record">签到记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyCheckInAsync(CheckInRecord record, CancellationToken cancellationToken);
}
