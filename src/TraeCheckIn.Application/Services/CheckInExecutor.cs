using Microsoft.Extensions.Logging;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Domain.Entities;
using TraeCheckIn.Domain.Enums;
using TraeCheckIn.Domain.Exceptions;

namespace TraeCheckIn.Application.Services;

/// <summary>签到执行器：负责认证、调用签到接口，并按重试策略处理瞬时错误与凭证失效</summary>
public sealed class CheckInExecutor
{
    private readonly ITraeApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly CheckInOptions _options;
    private readonly ILogger<CheckInExecutor> _logger;

    /// <summary>创建签到执行器</summary>
    /// <param name="apiClient">平台接口客户端</param>
    /// <param name="authService">认证服务</param>
    /// <param name="options">签到接口配置</param>
    /// <param name="logger">日志记录器</param>
    public CheckInExecutor(ITraeApiClient apiClient, IAuthService authService, CheckInOptions options, ILogger<CheckInExecutor> logger)
    {
        _apiClient = apiClient;
        _authService = authService;
        _options = options;
        _logger = logger;
    }

    /// <summary>查询远程当日签到状态（未配置 StatusUrl 或查询失败时返回 null，不阻断签到主流程）</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示已签到；false 表示未签到；null 表示无法确定</returns>
    public async Task<bool?> TryQueryCheckedTodayAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.StatusUrl))
        {
            return null;
        }

        try
        {
            var token = await _authService.GetValidTokenAsync(forceRefresh: false, cancellationToken);
            var result = await _apiClient.QueryStatusAsync(token, cancellationToken);
            if (result is { IsAuthFailure: true })
            {
                // 凭证已失效时状态不可信，交由后续签到流程给出明确的认证失效结论
                _logger.LogWarning("远程状态查询返回认证失效，无法确定当日签到状态");
                return null;
            }

            return result?.IsAlreadyCheckedIn;
        }
        catch (CheckInException ex)
        {
            // 状态查询属于辅助手段，失败时降级为直接尝试签到
            _logger.LogWarning(ex, "远程签到状态查询失败，将直接尝试执行签到");
            return null;
        }
    }

    /// <summary>执行签到（含重试机制），完全失败时返回 <see cref="CheckInStatus.Failed"/> 记录而非抛出异常</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>签到结果记录</returns>
    public async Task<CheckInRecord> ExecuteAsync(CancellationToken cancellationToken)
    {
        var maxRetry = Math.Max(1, _options.MaxRetry);
        string? lastDetail = null;

        for (var attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                var record = await ExecuteSingleAttemptAsync(forceRefreshToken: attempt > 1, cancellationToken);
                if (record is not null)
                {
                    return record;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CheckInException ex) when (ex.IsTransient && attempt < maxRetry)
            {
                // 网络抖动、超时等瞬时错误：按次数线性递增间隔后重试
                _logger.LogWarning(ex, "第 {Attempt}/{MaxRetry} 次签到因瞬时错误失败，{Delay} 秒后重试",
                    attempt, maxRetry, _options.RetryDelaySeconds * attempt);
                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds * attempt), cancellationToken);
            }
            catch (LoginException ex) when (attempt < maxRetry)
            {
                // 凭证失效：下一轮强制重新登录换取新 Token
                _logger.LogWarning(ex, "第 {Attempt}/{MaxRetry} 次签到遇到登录失效，将刷新凭证后重试", attempt, maxRetry);
                await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
            }
            catch (CheckInException ex)
            {
                lastDetail = ex.ToString();
                break;
            }
        }

        return new CheckInRecord
        {
            Time = DateTime.Now,
            Status = CheckInStatus.Failed,
            Message = $"签到失败：已达最大尝试次数（{maxRetry}）或发生不可恢复错误",
            Detail = lastDetail
        };
    }

    /// <summary>执行单次签到尝试；业务性失败（接口明确拒绝）直接返回 Failed 记录，不再重试</summary>
    private async Task<CheckInRecord?> ExecuteSingleAttemptAsync(bool forceRefreshToken, CancellationToken cancellationToken)
    {
        var token = await _authService.GetValidTokenAsync(forceRefreshToken, cancellationToken);
        var result = await _apiClient.CheckInAsync(token, cancellationToken);

        if (result.IsAuthFailure)
        {
            // 认证失效属于确定性失败，重试也无法恢复，直接给出可操作的结论
            return BuildRecord(CheckInStatus.Failed, "认证凭证已失效，请在 Trae IDE 中重新抓取 Token 并更新 TraeAccount:Token 配置（凭证有效期约 14 天）", result.RawBody);
        }

        if (result.IsAlreadyCheckedIn)
        {
            return BuildRecord(CheckInStatus.AlreadyCheckedIn, "今日已签到（平台返回幂等结果）", result.RawBody);
        }

        if (result.IsSuccess)
        {
            return BuildRecord(CheckInStatus.Success, "签到成功", result.RawBody);
        }

        return BuildRecord(CheckInStatus.Failed, $"签到失败（HTTP {result.StatusCode}）", result.RawBody);
    }

    /// <summary>构造签到记录</summary>
    private static CheckInRecord BuildRecord(CheckInStatus status, string message, string? detail) =>
        new() { Time = DateTime.Now, Status = status, Message = message, Detail = detail };
}
