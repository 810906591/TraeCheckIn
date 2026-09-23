namespace TraeCheckIn.Domain.Exceptions;

/// <summary>网络通信异常（连接失败、请求超时等），默认视为可重试的瞬时错误</summary>
public sealed class NetworkException : CheckInException
{
    /// <summary>创建网络异常</summary>
    /// <param name="message">错误描述</param>
    /// <param name="innerException">内部异常</param>
    public NetworkException(string message, Exception? innerException = null)
        : base(message, isTransient: true, innerException)
    {
    }
}
