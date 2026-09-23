namespace TraeCheckIn.Application.Configuration;

/// <summary>签到结果通知配置</summary>
public sealed class NotificationOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "Notification";

    /// <summary>签到成功时是否发送通知</summary>
    public bool NotifyOnSuccess { get; set; } = true;

    /// <summary>签到失败时是否发送通知</summary>
    public bool NotifyOnFailure { get; set; } = true;

    /// <summary>是否启用邮件通知（关闭时仅保留日志通知）</summary>
    public bool EnableEmail { get; set; }

    /// <summary>SMTP 邮件发送设置</summary>
    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>SMTP 邮件发送设置</summary>
public sealed class SmtpOptions
{
    /// <summary>SMTP 服务器地址（如 smtp.qq.com）</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP 端口（STARTTLS 加密通常为 587；465 为隐式 SSL，SmtpClient 不支持）</summary>
    public int Port { get; set; } = 587;

    /// <summary>是否启用 SSL/TLS 加密</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>发件人邮箱</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>发件人密码或邮箱服务商授权码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>收件人邮箱列表</summary>
    public string[] To { get; set; } = [];
}
