using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Configuration;

/// <summary>Trae 账号凭证配置（支持单账号与多账号两种形式）</summary>
public sealed class TraeAccountOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "TraeAccount";

    /// <summary>账号显示名称，用于日志、通知与本地签到状态隔离；多账号时建议必填且唯一</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>认证方式：Token（直接使用已有凭证）或 Password（账号密码登录换取凭证）</summary>
    public AuthType AuthType { get; set; } = AuthType.Token;

    /// <summary>Token 认证方式使用的凭证（从浏览器开发者工具中获取）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>登录账号（手机号或邮箱），仅 Password 认证方式使用</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>登录密码，仅 Password 认证方式使用；建议通过环境变量或用户机密注入，避免明文提交仓库</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>该账号专属的完整请求头集合（模拟浏览器行为）</summary>
    /// <remarks>
    /// 平台签到奖励按设备判定，多账号需各自独立的设备身份；建议每个账号配置完整请求头，
    /// 并保证 x-device-id、vscode-sessionid、x-market-user-id、x-device-brand 等身份头在各账号间互不相同。
    /// 未配置的键将回退到全局 Http:Headers（若也未配置则不发送该头）。
    /// </remarks>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>多账号列表；为空时回退使用顶层单账号字段（兼容旧版配置）</summary>
    public List<TraeAccountOptions> Accounts { get; set; } = [];

    /// <summary>解析生效账号列表：Accounts 非空时返回多账号，否则回退为顶层单账号配置</summary>
    /// <returns>生效账号列表（至少包含一个条目）</returns>
    public IReadOnlyList<TraeAccountOptions> ResolveAccounts() =>
        Accounts.Count > 0 ? Accounts : [this];
}
