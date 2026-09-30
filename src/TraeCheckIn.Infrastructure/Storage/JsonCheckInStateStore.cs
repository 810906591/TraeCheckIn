using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Models;

namespace TraeCheckIn.Infrastructure.Storage;

/// <summary>基于 JSON 文件的签到状态仓储（文件内容为"账号键 → 状态"映射），用于跨进程重启后的当日去重判断</summary>
public sealed class JsonCheckInStateStore : ICheckInStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath;
    private readonly ILogger<JsonCheckInStateStore> _logger;

    /// <summary>创建状态仓储</summary>
    /// <param name="checkInOptions">签到配置（含状态文件路径）</param>
    /// <param name="logger">日志记录器</param>
    public JsonCheckInStateStore(IOptions<CheckInOptions> checkInOptions, ILogger<JsonCheckInStateStore> logger)
    {
        var configuredPath = checkInOptions.Value.StateFilePath;
        _filePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CheckInState?> LoadAsync(string accountKey, CancellationToken cancellationToken)
    {
        var states = await LoadAllAsync(cancellationToken);
        return states is not null && states.TryGetValue(accountKey, out var state) ? state : null;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string accountKey, CheckInState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 先读取现有状态再合并写入，避免保存一个账号时覆盖其他账号的记录
        var states = await LoadAllAsync(cancellationToken) ?? [];
        states[accountKey] = state;

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, states, JsonOptions, cancellationToken);
        _logger.LogInformation("签到状态已写入：{Path}（账号：{Account}，日期：{Date}，状态：{Status}）",
            _filePath, accountKey, state.LastCheckInDate, state.LastStatus);
    }

    /// <summary>读取状态文件中的全部账号状态；文件不存在、损坏或为旧版单账号格式时返回 null</summary>
    private async Task<Dictionary<string, CheckInState>?> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, CheckInState>>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            // 旧版单账号格式或损坏文件均按无状态处理：本次借助远程状态接口兜底去重，签到成功后自动重建为新格式
            _logger.LogWarning(ex, "签到状态文件解析失败（可能为旧版单账号格式），将按无状态处理并在签到后重建：{Path}", _filePath);
            return null;
        }
    }
}
