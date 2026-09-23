namespace TraeCheckIn.Application.Configuration;

/// <summary>签到接口与重试策略配置（接口地址需按 Trae 平台实际抓包结果填写）</summary>
public sealed class CheckInOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "CheckIn";

    /// <summary>登录接口相对路径，仅 Password 认证方式需要（如 /api/user/login）</summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>登录请求体模板，支持 {{username}} / {{password}} 占位符；注意密码含特殊字符时需自行转义</summary>
    public string LoginBodyTemplate { get; set; } = "{\"username\":\"{{username}}\",\"password\":\"{{password}}\"}";

    /// <summary>登录请求 Content-Type</summary>
    public string LoginContentType { get; set; } = "application/json";

    /// <summary>登录响应中 Token 的 JSON 路径（冒号分隔的多级路径，如 data:token）</summary>
    public string TokenJsonPath { get; set; } = "data:token";

    /// <summary>签到接口相对路径（必填，如 /api/user/checkin）</summary>
    public string CheckInUrl { get; set; } = string.Empty;

    /// <summary>签到请求方法（GET 或 POST）</summary>
    public string CheckInMethod { get; set; } = "POST";

    /// <summary>签到请求体（可选，留空表示不携带请求体）</summary>
    public string? CheckInBody { get; set; }

    /// <summary>签到请求 Content-Type</summary>
    public string CheckInContentType { get; set; } = "application/json";

    /// <summary>查询当日签到状态的接口相对路径（可选，留空则仅依赖本地状态判断是否重复签到）</summary>
    public string StatusUrl { get; set; } = string.Empty;

    /// <summary>认证请求头名称（如 Authorization 或 token）</summary>
    public string AuthHeaderName { get; set; } = "Authorization";

    /// <summary>认证请求头值模板，{token} 为占位符（如 Bearer {token}）</summary>
    public string AuthHeaderTemplate { get; set; } = "Bearer {token}";

    /// <summary>响应包含以下关键字时判定为签到成功；为空则 HTTP 2xx 即判定成功</summary>
    public string[] SuccessKeywords { get; set; } = ["签到成功"];

    /// <summary>响应包含以下关键字时判定为当日已签到</summary>
    public string[] AlreadyCheckedKeywords { get; set; } = ["已签到", "重复签到"];

    /// <summary>瞬时错误最大尝试次数（含首次）</summary>
    public int MaxRetry { get; set; } = 3;

    /// <summary>重试基础间隔（秒），按尝试次数线性递增</summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>本地状态文件路径（相对路径时基于程序运行目录）</summary>
    public string StateFilePath { get; set; } = Path.Combine("state", "checkin-state.json");
}
