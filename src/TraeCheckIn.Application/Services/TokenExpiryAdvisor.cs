using Microsoft.Extensions.Logging;
using TraeCheckIn.Domain.Services;

namespace TraeCheckIn.Application.Services;

/// <summary>凭证临期告警器：评估 Trae 认证凭证的剩余有效期，临期时输出告警日志</summary>
public static class TokenExpiryAdvisor
{
    /// <summary>评估凭证有效期，剩余时长低于阈值时记录 Warning 日志，避免凭证静默过期后才被发现</summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="token">Trae 认证凭证（JWT 或其他形式）</param>
    public static void WarnIfNearExpiry(ILogger logger, string? token)
    {
        if (!JwtTokenInspector.TryGetExpirationUtc(token, out var expirationUtc))
        {
            // 非 JWT 格式（如 Cookie、Opaque Token）无法判定有效期，跳过告警
            return;
        }

        var remaining = expirationUtc - DateTime.UtcNow;
        if (remaining >= JwtTokenInspector.NearExpiryThreshold)
        {
            return;
        }

        logger.LogWarning(
            "Trae 认证凭证即将过期：{ExpireTime:yyyy-MM-dd HH:mm}（本地时间），剩余约 {RemainingDays:0.#} 天。请尽快在 Trae IDE 中重新抓取 Token 并更新 TraeAccount:Token 配置",
            expirationUtc.ToLocalTime(),
            remaining.TotalDays);
    }
}
