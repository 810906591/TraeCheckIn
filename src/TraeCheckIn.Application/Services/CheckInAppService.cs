using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Models;
using TraeCheckIn.Domain.Entities;
using TraeCheckIn.Domain.Enums;

namespace TraeCheckIn.Application.Services;

/// <summary>签到用例编排服务：状态检测（防重复）→ 执行签到 → 状态落盘 → 结果通知</summary>
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

    /// <summary>执行一次完整的签到流程（含当日防重复签到检测）</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到结果记录</returns>
    public async Task<CheckInRecord> ExecuteAsync(CancellationToken cancellationToken)
    {
        // 每次执行签到前评估凭证有效期：长期驻留的服务跨天运行时也能暴露临期状态
        TokenExpiryAdvisor.WarnIfNearExpiry(_logger, _accountOptions.Token);

        if (await IsAlreadyCheckedInTodayAsync(cancellationToken))
        {
            _logger.LogInformation("今日已完成签到，跳过本次执行");
            return new CheckInRecord
            {
                Time = DateTime.Now,
                Status = CheckInStatus.Skipped,
                Message = "今日已完成签到，跳过执行"
            };
        }

        var record = await _executor.ExecuteAsync(cancellationToken);

        if (record.Status is CheckInStatus.Success or CheckInStatus.AlreadyCheckedIn)
        {
            await SaveStateAsync(record, cancellationToken);
        }

        await _notificationService.NotifyCheckInAsync(record, cancellationToken);
        return record;
    }

    /// <summary>判断今日是否已完成签到：优先检查本地状态文件，其次查询远程状态接口</summary>
    private async Task<bool> IsAlreadyCheckedInTodayAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var state = await _stateStore.LoadAsync(cancellationToken);
        if (state?.LastCheckInDate == today && state.LastStatus is CheckInStatus.Success or CheckInStatus.AlreadyCheckedIn)
        {
            _logger.LogInformation("本地状态显示今日（{Today}）已完成签到", today);
            return true;
        }

        var remoteChecked = await _executor.TryQueryCheckedTodayAsync(cancellationToken);
        if (remoteChecked == true)
        {
            _logger.LogInformation("远程状态显示今日已完成签到");
            return true;
        }

        return false;
    }

    /// <summary>持久化签到状态；写入失败仅记录日志，下次启动可能重复签到但不影响本次结果</summary>
    private async Task SaveStateAsync(CheckInRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await _stateStore.SaveAsync(new CheckInState
            {
                LastCheckInDate = DateOnly.FromDateTime(record.Time),
                LastStatus = record.Status,
                LastMessage = record.Message,
                UpdatedAt = DateTime.Now
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "签到状态写入失败，下次启动可能出现重复签到");
        }
    }
}
