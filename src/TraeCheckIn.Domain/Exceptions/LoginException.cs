namespace TraeCheckIn.Domain.Exceptions;

/// <summary>登录认证失败异常（凭证无效、接口返回 401/403 等），属于不可重试错误，需重新认证</summary>
public sealed class LoginException : CheckInException
{
    /// <summary>创建登录异常</summary>
    /// <param name="message">错误描述</param>
    /// <param name="innerException">内部异常</param>
    public LoginException(string message, Exception? innerException = null)
        : base(message, isTransient: false, innerException)
    {
    }
}
