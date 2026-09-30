using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Models;
using TraeCheckIn.Domain.Entities;
using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Services;

/// <summary>签到用例编排服务：遍历全部账号，按"状态检测（防重复）→ 执行签到 → 状态落盘 → 结果通知"逐账号执行；单账号异常不影响其他账号</summary>
public sealed class CheckInAppService
{
    private readonly CheckInExecutor _executor;
    private readonly ICheckInStateStore _stateStore;
    private readonly INotificationService _notificationService;
    private readonly TraeAccountOptions _accountOptions;
    private readonly ILogger<CheckInAppService> _logger;

    /// <summary>创建签到用例服务</summary>
    /// <param name="executor">签到执行器</param>
    /// <param name="stateStore">签到状态仓储</param>
    /// <param name="notificationService">通知门面服务</param>
    /// <param name="accountOptions">账号凭证配置</param>
    /// <param name="logger">日志记录器</param>
    public CheckInAppService(CheckInExecutor executor, ICheckInStateStore stateStore, INotificationService notificationService, IOptions<TraeAccountOptions> accountOptions, ILogger<CheckInAppService> logger)
    {
        _executor = executor;
        _stateStore = stateStore;
        _notificationService = notificationService;
        _accountOptions = accountOptions.Value;
        _logger = logger;
    }

    /// <summary>执行一次完整的签到流程：逐账号执行签到（含当日防重复签到检测），返回全部账号的结果记录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>各账号的签到结果记录（顺序与配置中的账号顺序一致）</returns>
    public async Task<IReadOnlyList<CheckInRecord>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var accounts = _accountOptions.ResolveAccounts();
        var results = new List<CheckInRecord>(accounts.Count);

        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            var accountKey = ResolveAccountKey(account, i);
            try
            {
                results.Add(await ExecuteForAccountAsync(account, accountKey, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 隔离单账号异常：记录失败结果后继续处理其余账号
                _logger.LogError(ex, "账号（{Account}）签到流程发生未处理异常", accountKey);
                results.Add(new CheckInRecord
                {
                    AccountName = accountKey,
                    Time = DateTime.Now,
                    Status = CheckInStatus.Failed,
                    Message = $"签到流程异常：{ex.Message}",
                    Detail = ex.ToString()
                });
            }
        }

        return results;
    }

    /// <summary>执行单个账号的签到流程（含当日防重复签到检测）</summary>
    /// <param name="account">账号凭证配置</param>
    /// <param name="accountKey">账号键（用于日志展示与本地状态隔离）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到结果记录</returns>
    private async Task<CheckInRecord> ExecuteForAccountAsync(TraeAccountOptions account, string accountKey, CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始执行账号（{Account}）的签到流程", accountKey);

        // 每次执行签到前评估凭证有效期：长期驻留的服务跨天运行时也能暴露临期状态
        TokenExpiryAdvisor.WarnIfNearExpiry(_logger, account.Token);

        if (await IsAlreadyCheckedInTodayAsync(accountKey, account, cancellationToken))
        {
            _logger.LogInformation("账号（{Account}）今日已完成签到，跳过本次执行", accountKey);
            return new CheckInRecord
            {
                AccountName = accountKey,
                Time = DateTime.Now,
                Status = CheckInStatus.Skipped,
                Message = "今日已完成签到，跳过执行"
            };
        }

        var record = await _executor.ExecuteAsync(account, cancellationToken);
        record = WithAccountName(record, accountKey);

        if (record.Status is CheckInStatus.Success or CheckInStatus.AlreadyCheckedIn)
        {
            await SaveStateAsync(accountKey, record, cancellationToken);
        }

        await _notificationService.NotifyCheckInAsync(record, cancellationToken);
        return record;
    }

    /// <summary>判断指定账号今日是否已完成签到：优先检查本地状态文件，其次查询远程状态接口</summary>
    private async Task<bool> IsAlreadyCheckedInTodayAsync(string accountKey, TraeAccountOptions account, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var state = await _stateStore.LoadAsync(accountKey, cancellationToken);
        if (state?.LastCheckInDate == today && state.LastStatus is CheckInStatus.Success or CheckInStatus.AlreadyCheckedIn)
        {
            _logger.LogInformation("本地状态显示账号（{Account}）今日（{Today}）已完成签到", accountKey, today);
            return true;
        }

        var remoteChecked = await _executor.TryQueryCheckedTodayAsync(account, cancellationToken);
        if (remoteChecked == true)
        {
            _logger.LogInformation("远程状态显示账号（{Account}）今日已完成签到", accountKey);
            return true;
        }

        return false;
    }

    /// <summary>持久化指定账号的签到状态；写入失败仅记录日志，下次启动可能重复签到但不影响本次结果</summary>
    private async Task SaveStateAsync(string accountKey, CheckInRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await _stateStore.SaveAsync(accountKey, new CheckInState
            {
                LastCheckInDate = DateOnly.FromDateTime(record.Time),
                LastStatus = record.Status,
                LastMessage = record.Message,
                UpdatedAt = DateTime.Now
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "账号（{Account}）签到状态写入失败，下次启动可能出现重复签到", accountKey);
        }
    }

    /// <summary>解析账号键：优先使用配置的 Name，未配置时按配置顺序生成（account-1、account-2 …）</summary>
    private static string ResolveAccountKey(TraeAccountOptions account, int index) =>
        string.IsNullOrWhiteSpace(account.Name) ? $"account-{index + 1}" : account.Name.Trim();

    /// <summary>为签到记录补充账号名称</summary>
    private static CheckInRecord WithAccountName(CheckInRecord record, string accountName) =>
        new() { AccountName = accountName, Time = record.Time, Status = record.Status, Message = record.Message, Detail = record.Detail };
}
