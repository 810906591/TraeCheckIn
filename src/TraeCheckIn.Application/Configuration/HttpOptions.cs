namespace TraeCheckIn.Application.Configuration;

/// <summary>HTTP 客户端配置（模拟浏览器行为，降低被风控拦截的概率）</summary>
public sealed class HttpOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "Http";

    /// <summary>平台基地址，如 https://www.trae.cn</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>User-Agent 浏览器标识</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    /// <summary>附加的浏览器请求头（如 Referer、Origin、Accept-Language）</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>请求超时时间（秒）</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
