namespace TraeCheckIn.Domain.Enums;

/// <summary>Trae 平台认证方式</summary>
public enum AuthType
{
    /// <summary>Token 直接认证（从配置中读取已有凭证，如 Bearer Token 或 Cookie）</summary>
    Token = 0,

    /// <summary>账号密码登录换取凭证</summary>
    Password = 1
}
