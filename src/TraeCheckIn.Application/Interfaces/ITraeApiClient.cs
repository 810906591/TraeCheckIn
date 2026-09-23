using TraeCheckIn.Application.Models;

namespace TraeCheckIn.Application.Interfaces;

/// <summary>Trae 平台 HTTP 接口客户端抽象</summary>
public interface ITraeApiClient
{
    /// <summary>使用配置的账号密码登录，换取认证 Token</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证 Token</returns>
    /// <exception cref="TraeCheckIn.Domain.Exceptions.LoginException">登录失败时抛出</exception>
    Task<string> LoginAsync(CancellationToken cancellationToken);

    /// <summary>执行每日签到</summary>
    /// <param name="token">认证 Token</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到接口评估结果</returns>
    Task<CheckInApiResult> CheckInAsync(string token, CancellationToken cancellationToken);

    /// <summary>查询当日签到状态（StatusUrl 未配置时返回 null）</summary>
    /// <param name="token">认证 Token</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>状态接口评估结果；未配置查询接口时返回 null</returns>
    Task<CheckInApiResult?> QueryStatusAsync(string token, CancellationToken cancellationToken);
}
