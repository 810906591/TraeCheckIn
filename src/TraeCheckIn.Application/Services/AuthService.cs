using Microsoft.Extensions.Logging;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Enums;
using TraeCheckIn.Domain.Exceptions;

namespace TraeCheckIn.Application.Services;

/// <summary>认证服务：按账号配置的认证方式提供有效 Token，并对密码登录结果做按账号的进程内缓存</summary>
public sealed class AuthService : IAuthService
{
    private readonly ITraeApiClient _apiClient;
    private readonly ILogger<AuthService> _logger;

    /// <summary>密码登录 Token 缓存（键：认证方式+登录账号，实现多账号缓存隔离）</summary>
    private readonly Dictionary<string, string> _cachedTokens = new(StringComparer.Ordinal);

    /// <summary>创建认证服务</summary>
    /// <param name="apiClient">平台接口客户端</param>
    /// <param name="logger">日志记录器</param>
    public AuthService(ITraeApiClient apiClient, ILogger<AuthService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetValidTokenAsync(TraeAccountOptions account, bool forceRefresh, CancellationToken cancellationToken)
    {
        // Token 认证方式：直接使用配置中的固定凭证
        if (account.AuthType == AuthType.Token)
        {
            if (string.IsNullOrWhiteSpace(account.Token))
            {
                throw new LoginException($"账号（{ResolveDisplayName(account)}）认证方式为 Token，但未提供凭证值，请更新对应账号的 Token 配置");
            }

            return account.Token;
        }

        // 密码认证方式：优先使用该账号缓存的 Token，需要时重新登录
        var cacheKey = BuildCacheKey(account);
        if (!forceRefresh && _cachedTokens.TryGetValue(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(account.Password))
        {
            throw new LoginException($"账号（{ResolveDisplayName(account)}）认证方式为账号密码，但未配置 Username 或 Password");
        }

        _logger.LogInformation("开始使用账号密码登录 Trae 平台（账号：{Username}）", account.Username);
        var token = await _apiClient.LoginAsync(account, cancellationToken);
        _cachedTokens[cacheKey] = token;
        return token;
    }

    /// <summary>构造密码登录 Token 的缓存键（同一认证方式下按登录账号隔离）</summary>
    private static string BuildCacheKey(TraeAccountOptions account) =>
        $"{account.AuthType}:{account.Username}";

    /// <summary>解析账号展示名称：优先使用配置的 Name，其次使用登录账号，兜底为"未命名账号"</summary>
    private static string ResolveDisplayName(TraeAccountOptions account) =>
        !string.IsNullOrWhiteSpace(account.Name) ? account.Name
            : !string.IsNullOrWhiteSpace(account.Username) ? account.Username
            : "未命名账号";
}
