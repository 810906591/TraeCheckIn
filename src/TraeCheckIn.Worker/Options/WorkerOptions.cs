namespace TraeCheckIn.Worker.Options;

/// <summary>宿主运行选项（时间点字符串在 Worker 中解析，便于记录无效配置的日志）</summary>
/// <param name="Times">配置的触发时间点字符串（HH:mm 格式）</param>
/// <param name="RunOnce">是否单次执行模式（--run-once）</param>
public sealed record WorkerOptions(IReadOnlyList<string> Times, bool RunOnce);
