using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraeCheckIn.Application.Configuration;
using TraeCheckIn.Application.Interfaces;
using TraeCheckIn.Application.Models;
using TraeCheckIn.Domain.Exceptions;

namespace TraeCheckIn.Infrastructure.Http;

/// <summary>基于 HttpClient 的 Trae 平台接口客户端：模拟浏览器请求头与 Cookie 会话完成登录与签到</summary>
public sealed class TraeApiClient : ITraeApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CheckInOptions _options;
    private readonly string? _userAgent;
    private readonly IReadOnlyDictionary<string, string> _globalHeaders;
    private readonly ILogger<TraeApiClient> _logger;

    /// <summary>创建平台接口客户端</summary>
    /// <param name="httpOptions">HTTP 行为配置</param>
    /// <param name="checkInOptions">签到接口配置</param>
    /// <param name="logger">日志记录器</param>
    /// <exception cref="InvalidOperationException">关键接口地址未配置时抛出</exception>
    public TraeApiClient(IOptions<HttpOptions> httpOptions, IOptions<CheckInOptions> checkInOptions, ILogger<TraeApiClient> logger)
    {
        _options = checkInOptions.Value;
        _logger = logger;

        var http = httpOptions.Value;
        if (string.IsNullOrWhiteSpace(http.BaseUrl))
        {
            throw new InvalidOperationException("请在 appsettings.json 的 Http:BaseUrl 中配置 Trae 平台基地址");
        }

        if (string.IsNullOrWhiteSpace(_options.CheckInUrl))
        {
            throw new InvalidOperationException("请在 appsettings.json 的 CheckIn:CheckInUrl 中配置签到接口地址");
        }

        // 请求头改为每次请求动态应用（支持账号级覆盖），此处仅保留全局配置
        _userAgent = http.UserAgent;
        _globalHeaders = http.Headers;

        var handler = new SocketsHttpHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(http.BaseUrl),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, http.TimeoutSeconds))
        };
    }

    /// <inheritdoc />
    public async Task<string> LoginAsync(TraeAccountOptions account, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.LoginUrl))
        {
            throw new LoginException("认证方式为账号密码，但未配置 CheckIn:LoginUrl");
        }

        var content = new StringContent(BuildLoginBody(account), Encoding.UTF8, _options.LoginContentType);
        var (statusCode, responseBody) = await SendAsync(HttpMethod.Post, _options.LoginUrl, content, token: null, account.Headers, cancellationToken);

        if (statusCode is < 200 or >= 300)
        {
            throw new LoginException($"登录失败：HTTP {statusCode}，响应：{Truncate(responseBody)}");
        }

        var token = ExtractJsonString(responseBody, _options.TokenJsonPath);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new LoginException($"登录响应中未能提取 Token（JSON 路径：{_options.TokenJsonPath}），响应：{Truncate(responseBody)}");
        }

        _logger.LogInformation("登录成功，已获取认证 Token");
        return token;
    }

    /// <inheritdoc />
    public Task<CheckInApiResult> CheckInAsync(TraeAccountOptions account, string token, CancellationToken cancellationToken)
    {
        return SendAndEvaluateAsync(_options.CheckInMethod, _options.CheckInUrl, _options.CheckInBody, _options.CheckInContentType, token, account.Headers, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CheckInApiResult?> QueryStatusAsync(TraeAccountOptions account, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.StatusUrl))
        {
            return null;
        }

        var method = string.IsNullOrWhiteSpace(_options.StatusMethod) ? "GET" : _options.StatusMethod;
        return await SendAndEvaluateAsync(method, _options.StatusUrl, _options.StatusBody, _options.StatusBody is null ? null : _options.CheckInContentType, token, account.Headers, cancellationToken);
    }

    /// <summary>释放 HTTP 客户端资源</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>按配置模板构造指定账号的登录请求体（{{username}} / {{password}} 占位符替换）</summary>
    private string BuildLoginBody(TraeAccountOptions account) =>
        _options.LoginBodyTemplate
            .Replace("{{username}}", account.Username, StringComparison.Ordinal)
            .Replace("{{password}}", account.Password, StringComparison.Ordinal);

    /// <summary>应用请求头：先写入全局浏览器头，再应用账号级覆盖（同名头先移除再添加，空值跳过以回退全局）</summary>
    private void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headerOverrides)
    {
        if (!string.IsNullOrWhiteSpace(_userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        }

        foreach (var (name, value) in _globalHeaders)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        if (headerOverrides is null)
        {
            return;
        }

        foreach (var (name, value) in headerOverrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            request.Headers.Remove(name);
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }

    /// <summary>发送请求并根据关键字评估响应结果</summary>
    private async Task<CheckInApiResult> SendAndEvaluateAsync(string method, string url, string? body, string? contentType, string token, IReadOnlyDictionary<string, string>? headerOverrides, CancellationToken cancellationToken)
    {
        var content = body is null ? null : new StringContent(body, Encoding.UTF8, contentType ?? "application/json");
        var (statusCode, responseBody) = await SendAsync(new HttpMethod(method.ToUpperInvariant()), url, content, token, headerOverrides, cancellationToken);

        var result = EvaluateResponse(statusCode, responseBody);
        _logger.LogInformation("接口 {Url} 返回 HTTP {StatusCode}，评估结果：成功={Success}，已签到={Already}，认证失效={AuthFailure}",
            url, statusCode, result.IsSuccess, result.IsAlreadyCheckedIn, result.IsAuthFailure);
        return result;
    }

    /// <summary>发送 HTTP 请求并返回状态码与响应体；网络错误统一包装为 <see cref="NetworkException"/></summary>
    private async Task<(int StatusCode, string Body)> SendAsync(HttpMethod method, string url, HttpContent? content, string? token, IReadOnlyDictionary<string, string>? headerOverrides, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        ApplyHeaders(request, headerOverrides);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation(_options.AuthHeaderName, _options.AuthHeaderTemplate.Replace("{token}", token, StringComparison.Ordinal));
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ((int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException($"网络请求失败：{url}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkException($"网络请求超时：{url}", ex);
        }
    }

    /// <summary>根据 HTTP 状态码与响应关键字判定签到结果</summary>
    private CheckInApiResult EvaluateResponse(int statusCode, string responseBody)
    {
        // 凭证失效统一转为登录异常，由用例层触发重新认证
        if (statusCode is 401 or 403)
        {
            throw new LoginException($"认证凭证已失效（HTTP {statusCode}），需要重新登录");
        }

        var isSuccessCode = statusCode is >= 200 and < 300;

        // 业务码层面的认证失效（如 Trae 返回 HTTP 200 + code:1001）优先判定，避免被误归类为普通签到失败
        if (ContainsAny(responseBody, _options.AuthFailureKeywords))
        {
            return new CheckInApiResult(IsSuccess: false, IsAlreadyCheckedIn: false, IsAuthFailure: true, statusCode, responseBody);
        }

        // 已签到关键字优先判定，保证幂等语义
        if (ContainsAny(responseBody, _options.AlreadyCheckedKeywords))
        {
            return new CheckInApiResult(IsSuccess: isSuccessCode, IsAlreadyCheckedIn: true, IsAuthFailure: false, statusCode, responseBody);
        }

        var keywordSuccess = ContainsAny(responseBody, _options.SuccessKeywords);
        if (isSuccessCode && (_options.SuccessKeywords.Length == 0 || keywordSuccess))
        {
            return new CheckInApiResult(IsSuccess: true, IsAlreadyCheckedIn: false, IsAuthFailure: false, statusCode, responseBody);
        }

        return new CheckInApiResult(IsSuccess: false, IsAlreadyCheckedIn: false, IsAuthFailure: false, statusCode, responseBody);
    }

    /// <summary>判断响应文本是否包含任一关键字</summary>
    private static bool ContainsAny(string text, IEnumerable<string> keywords) =>
        keywords.Any(keyword => !string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>从 JSON 响应中按冒号分隔路径提取字符串值</summary>
    private static string? ExtractJsonString(string json, string path)
    {
        try
        {
            var node = JsonNode.Parse(json);
            foreach (var segment in path.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                node = node?[segment];
            }

            return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>截断超长文本用于日志输出</summary>
    private static string Truncate(string text, int maxLength = 500) =>
        text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
}
