using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Entities;

namespace TraeCheckIn.Infrastructure.Notifications;

/// <summary>邮件通知器：通过 SMTP 将签到结果发送至指定收件人</summary>
public sealed class EmailNotifier : ICheckInNotifier
{
    private readonly NotificationOptions _options;
    private readonly ILogger<EmailNotifier> _logger;

    /// <summary>创建邮件通知器</summary>
    /// <param name="options">通知配置</param>
    /// <param name="logger">日志记录器</param>
    public EmailNotifier(IOptions<NotificationOptions> options, ILogger<EmailNotifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyCheckInAsync(CheckInRecord record, CancellationToken cancellationToken)
    {
        var smtp = _options.Smtp;
        if (!_options.EnableEmail || smtp.To.Length == 0 || string.IsNullOrWhiteSpace(smtp.Host))
        {
            return;
        }

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            Credentials = new NetworkCredential(smtp.From, smtp.Password)
        };
        using var message = BuildMessage(smtp, record);

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("签到结果邮件已发送至 {Recipients}", string.Join(",", smtp.To));
    }

    /// <summary>构造签到结果邮件</summary>
    private static MailMessage BuildMessage(SmtpOptions smtp, CheckInRecord record)
    {
        var message = new MailMessage
        {
            From = new MailAddress(smtp.From),
            Subject = $"[Trae 自动签到] {record.Status} - {record.Time:yyyy-MM-dd HH:mm:ss}",
            Body = BuildBody(record),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        foreach (var recipient in smtp.To)
        {
            message.To.Add(recipient);
        }

        return message;
    }

    /// <summary>构造邮件正文</summary>
    private static string BuildBody(CheckInRecord record) =>
        $"""
        Trae 每日自动签到结果

        时间：{record.Time:yyyy-MM-dd HH:mm:ss}
        状态：{record.Status}
        摘要：{record.Message}
        详情：{record.Detail ?? "-"}
        """;
}
