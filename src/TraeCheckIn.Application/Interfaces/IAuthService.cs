namespace TraeCheckIn.Application.Interfaces;

/// <summary>认证服务抽象：统一 Token 与账号密码两种认证方式，负责凭证缓存与按需刷新</summary>
public interface IAuthService
{
    /// <summary>获取有效的认证 Token</summary>
    /// <param name="forceRefresh">是否强制刷新（忽略缓存，重新登录）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证 Token</returns>
    Task<string> GetValidTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
}
