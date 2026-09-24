namespace TraeCheckIn.Application.Models;

/// <summary>签到接口调用结果（对 HTTP 响应的统一评估快照）</summary>
/// <param name="IsSuccess">签到是否成功</param>
/// <param name="IsAlreadyCheckedIn">当日是否已签到</param>
/// <param name="IsAuthFailure">是否为认证凭证失效（业务码层面的登录失效，如 code:1001）</param>
/// <param name="StatusCode">HTTP 状态码</param>
/// <param name="RawBody">响应原始内容，用于日志与问题排查</param>
public sealed record CheckInApiResult(bool IsSuccess, bool IsAlreadyCheckedIn, bool IsAuthFailure, int StatusCode, string RawBody);
