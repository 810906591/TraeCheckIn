using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Configuration;

/// <summary>Trae 账号凭证配置</summary>
public sealed class TraeAccountOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "TraeAccount";

    /// <summary>认证方式：Token（直接使用已有凭证）或 Password（账号密码登录换取凭证）</summary>
    public AuthType AuthType { get; set; } = AuthType.Token;

    /// <summary>Token 认证方式使用的凭证（从浏览器开发者工具中获取）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>登录账号（手机号或邮箱），仅 Password 认证方式使用</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>登录密码，仅 Password 认证方式使用；建议通过环境变量或用户机密注入，避免明文提交仓库</summary>
    public string Password { get; set; } = string.Empty;
}
