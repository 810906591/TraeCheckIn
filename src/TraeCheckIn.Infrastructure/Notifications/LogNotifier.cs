using Microsoft.Extensions.Logging;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Entities;

namespace TraeCheckIn.Infrastructure.Notifications;

/// <summary>日志文件通知器：将签到结果写入按天滚动的日志文件（由 Serilog 落盘）</summary>
public sealed class LogNotifier : ICheckInNotifier
{
    private readonly ILogger<LogNotifier> _logger;

    /// <summary>创建日志通知器</summary>
    /// <param name="logger">日志记录器</param>
    public LogNotifier(ILogger<LogNotifier> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task NotifyCheckInAsync(CheckInRecord record, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[签到通知] 账号：{Account}，时间：{Time:yyyy-MM-dd HH:mm:ss}，状态：{Status}，摘要：{Message}，详情：{Detail}",
            record.AccountName, record.Time, record.Status, record.Message, record.Detail ?? "-");
        return Task.CompletedTask;
    }
}
