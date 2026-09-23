using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Entities;
using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Services;

/// <summary>聚合通知服务：按配置过滤成功/失败通知，并分发至各通知渠道，单渠道失败不影响整体</summary>
public sealed class CompositeNotificationService : INotificationService
{
    private readonly IEnumerable<ICheckInNotifier> _notifiers;
    private readonly NotificationOptions _options;
    private readonly ILogger<CompositeNotificationService> _logger;

    /// <summary>创建聚合通知服务</summary>
    /// <param name="notifiers">全部通知渠道</param>
    /// <param name="options">通知配置</param>
    /// <param name="logger">日志记录器</param>
    public CompositeNotificationService(IEnumerable<ICheckInNotifier> notifiers, IOptions<NotificationOptions> options, ILogger<CompositeNotificationService> logger)
    {
        _notifiers = notifiers;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyCheckInAsync(CheckInRecord record, CancellationToken cancellationToken)
    {
        var shouldNotify = record.Status switch
        {
            CheckInStatus.Success or CheckInStatus.AlreadyCheckedIn => _options.NotifyOnSuccess,
            CheckInStatus.Failed => _options.NotifyOnFailure,
            _ => false
        };

        if (!shouldNotify)
        {
            return;
        }

        foreach (var notifier in _notifiers)
        {
            try
            {
                await notifier.NotifyCheckInAsync(record, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 单一通知渠道失败不应影响其他渠道与签到主流程
                _logger.LogError(ex, "通知渠道 {Notifier} 发送失败", notifier.GetType().Name);
            }
        }
    }
}
