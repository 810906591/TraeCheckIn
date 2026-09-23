using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Models;

namespace TraeCheckIn.Infrastructure.Storage;

/// <summary>基于 JSON 文件的签到状态仓储，用于跨进程重启后的当日去重判断</summary>
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
    public async Task<CheckInState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<CheckInState>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            // 状态文件损坏时按无状态处理，后续签到成功后将重建文件
            _logger.LogWarning(ex, "签到状态文件解析失败，将按无状态处理并重建：{Path}", _filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(CheckInState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        _logger.LogInformation("签到状态已写入：{Path}（日期：{Date}，状态：{Status}）",
            _filePath, state.LastCheckInDate, state.LastStatus);
    }
}
