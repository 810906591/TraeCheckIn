using TraeCheckIn.Application.Models;

namespace TraeCheckIn.Application.Interfaces;

/// <summary>签到状态持久化仓储抽象（接口定义于应用层，实现位于基础设施层）；多账号场景按账号键隔离</summary>
public interface ICheckInStateStore
{
    /// <summary>加载指定账号最近一次签到状态；状态文件不存在、损坏或无该账号记录时返回 null</summary>
    /// <param name="accountKey">账号键（配置的 Name，未配置时按序号生成）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到状态快照</returns>
    Task<CheckInState?> LoadAsync(string accountKey, CancellationToken cancellationToken);

    /// <summary>保存指定账号的签到状态（不影响其他账号的已有状态）</summary>
    /// <param name="accountKey">账号键（配置的 Name，未配置时按序号生成）</param>
    /// <param name="state">签到状态快照</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(string accountKey, CheckInState state, CancellationToken cancellationToken);
}
