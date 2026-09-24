using System.Text;
using System.Text.Json;

namespace TraeCheckIn.Domain.Services;

/// <summary>JWT 凭证解析器：从 Token 中提取过期时间（纯函数，无 IO 与外部依赖）</summary>
public static class JwtTokenInspector
{
    /// <summary>临期预警阈值：剩余有效期低于该值时应当告警</summary>
    public static readonly TimeSpan NearExpiryThreshold = TimeSpan.FromDays(3);

    /// <summary>尝试从 JWT 凭证中解析过期时间（UTC）</summary>
    /// <param name="token">JWT 凭证（形如 header.payload.signature 的三段式字符串）</param>
    /// <param name="expirationUtc">解析出的过期时间（UTC）；解析失败时为 default</param>
    /// <returns>是否成功解析出过期时间；非 JWT 格式（如 Cookie、Opaque Token）返回 false</returns>
    public static bool TryGetExpirationUtc(string? token, out DateTime expirationUtc)
    {
        expirationUtc = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var segments = token.Split('.');
        if (segments.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = DecodeBase64Url(segments[1]);
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("exp", out var expElement) ||
                !expElement.TryGetInt64(out var expSeconds))
            {
                return false;
            }

            expirationUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>解码 Base64Url 字符串：还原 URL 安全字符并补齐填充符</summary>
    private static string DecodeBase64Url(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/').TrimEnd('=');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
