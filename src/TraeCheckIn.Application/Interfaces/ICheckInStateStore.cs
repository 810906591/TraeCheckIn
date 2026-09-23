using TraeCheckIn.Application.Models;

namespace TraeCheckIn.Application.Interfaces;

/// <summary>签到状态持久化仓储抽象（接口定义于应用层，实现位于基础设施层）</summary>
public interface ICheckInStateStore
{
    /// <summary>加载最近一次签到状态；状态文件不存在或损坏时返回 null</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到状态快照</returns>
    Task<CheckInState?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>保存签到状态</summary>
    /// <param name="state">签到状态快照</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(CheckInState state, CancellationToken cancellationToken);
}
