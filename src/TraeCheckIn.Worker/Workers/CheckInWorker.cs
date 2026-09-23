using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TraeCheckIn.Application.Services;
using TraeCheckIn.Infrastructure.Scheduling;
using TraeCheckIn.Worker.Options;

namespace TraeCheckIn.Worker.Workers;

/// <summary>签到后台服务：按配置的每日时间点触发签到，或以 --run-once 模式立即执行一次</summary>
public sealed class CheckInWorker : BackgroundService
{
    private readonly CheckInAppService _checkInAppService;
    private readonly NextRunCalculator _nextRunCalculator;
    private readonly WorkerOptions _workerOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CheckInWorker> _logger;
    private readonly IReadOnlyList<TimeOnly> _dailyTimes;

    /// <summary>创建签到后台服务，并解析校验触发时间点配置</summary>
    /// <param name="checkInAppService">签到用例服务</param>
    /// <param name="nextRunCalculator">触发时间计算器</param>
    /// <param name="workerOptions">运行选项</param>
    /// <param name="lifetime">宿主生命周期</param>
    /// <param name="logger">日志记录器</param>
    public CheckInWorker(
        CheckInAppService checkInAppService,
        NextRunCalculator nextRunCalculator,
        WorkerOptions workerOptions,
        IHostApplicationLifetime lifetime,
        ILogger<CheckInWorker> logger)
    {
        _checkInAppService = checkInAppService;
        _nextRunCalculator = nextRunCalculator;
        _workerOptions = workerOptions;
        _lifetime = lifetime;
        _logger = logger;
        _dailyTimes = ParseDailyTimes(workerOptions.Times);
    }

    /// <summary>后台主循环：单次执行模式立即签到后退出；定时模式循环等待下一个时间点</summary>
    /// <param name="stoppingToken">停止令牌</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_workerOptions.RunOnce)
        {
            await RunOnceAsync(stoppingToken);
            return;
        }

        if (_dailyTimes.Count == 0)
        {
            _logger.LogError("未配置有效的签到时间点（Schedule:Times），服务退出");
            _lifetime.StopApplication();
            return;
        }

        await ScheduleLoopAsync(stoppingToken);
    }

    /// <summary>解析并校验配置的触发时间点，无效条目记录错误后忽略</summary>
    private IReadOnlyList<TimeOnly> ParseDailyTimes(IReadOnlyList<string> times)
    {
        var result = new List<TimeOnly>();
        foreach (var item in times)
        {
            if (TimeOnly.TryParseExact(item, "HH:mm", out var parsed))
            {
                result.Add(parsed);
            }
            else
            {
                _logger.LogError("无效的签到时间配置：\"{Time}\"，已忽略（应为 HH:mm 24 小时制格式）", item);
            }
        }

        return result;
    }

    /// <summary>单次执行模式：立即签到一次后通知宿主退出</summary>
    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("以 --run-once 模式启动，立即执行一次签到");
        await ExecuteCheckInAsync(stoppingToken);
        _lifetime.StopApplication();
    }

    /// <summary>定时调度循环：等待下一个时间点触发签到，触发后推进一分钟避免重复执行</summary>
    private async Task ScheduleLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = _nextRunCalculator.GetNextOccurrence(DateTime.Now, _dailyTimes);
            if (next is null)
            {
                _logger.LogError("无法计算下一次签到时间，服务退出");
                break;
            }

            _logger.LogInformation("下一次签到时间：{NextTime:yyyy-MM-dd HH:mm:ss}", next.Value);
            var delay = next.Value - DateTime.Now;
            if (delay > TimeSpan.Zero && !await WaitSafelyAsync(delay, stoppingToken))
            {
                return;
            }

            await ExecuteCheckInAsync(stoppingToken);

            // 触发后推进一分钟，避免停留在当前时间点内被重复触发
            if (!await WaitSafelyAsync(TimeSpan.FromMinutes(1), stoppingToken))
            {
                return;
            }
        }
    }

    /// <summary>执行签到流程并兜底捕获所有未处理异常，保证后台服务不因意外异常终止</summary>
    private async Task ExecuteCheckInAsync(CancellationToken stoppingToken)
    {
        try
        {
            var record = await _checkInAppService.ExecuteAsync(stoppingToken);
            _logger.LogInformation("本次签到流程结束：{Status} - {Message}", record.Status, record.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "签到流程发生未处理异常");
        }
    }

    /// <summary>安全等待指定时长；宿主停止时返回 false 表示应退出循环</summary>
    private async Task<bool> WaitSafelyAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
