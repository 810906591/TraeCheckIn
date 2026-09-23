using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Enums;
using TraeCheckIn.Domain.Exceptions;

namespace TraeCheckIn.Application.Services;

/// <summary>认证服务：按配置的认证方式提供有效 Token，并对密码登录结果做进程内缓存</summary>
public sealed class AuthService : IAuthService
{
    private readonly ITraeApiClient _apiClient;
    private readonly TraeAccountOptions _accountOptions;
    private readonly ILogger<AuthService> _logger;
    private string? _cachedToken;

    /// <summary>创建认证服务</summary>
    /// <param name="apiClient">平台接口客户端</param>
    /// <param name="accountOptions">账号凭证配置</param>
    /// <param name="logger">日志记录器</param>
    public AuthService(ITraeApiClient apiClient, IOptions<TraeAccountOptions> accountOptions, ILogger<AuthService> logger)
    {
        _apiClient = apiClient;
        _accountOptions = accountOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetValidTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        // Token 认证方式：直接使用配置中的固定凭证
        if (_accountOptions.AuthType == AuthType.Token)
        {
            if (string.IsNullOrWhiteSpace(_accountOptions.Token))
            {
                throw new LoginException("认证方式为 Token，但未在 TraeAccount:Token 中提供凭证值");
            }

            return _accountOptions.Token;
        }

        // 密码认证方式：优先使用缓存的 Token，需要时重新登录
        if (!forceRefresh && !string.IsNullOrEmpty(_cachedToken))
        {
            return _cachedToken;
        }

        if (string.IsNullOrWhiteSpace(_accountOptions.Username) || string.IsNullOrWhiteSpace(_accountOptions.Password))
        {
            throw new LoginException("认证方式为账号密码，但未配置 TraeAccount:Username 或 TraeAccount:Password");
        }

        _logger.LogInformation("开始使用账号密码登录 Trae 平台");
        _cachedToken = await _apiClient.LoginAsync(cancellationToken);
        return _cachedToken;
    }
}
