namespace TraeCheckIn.Domain.Exceptions;

/// <summary>签到业务异常基类，通过 <see cref="IsTransient"/> 区分可重试与不可重试错误</summary>
public class CheckInException : Exception
{
    /// <summary>是否为瞬时错误（网络抖动、超时等，建议重试）</summary>
    public bool IsTransient { get; }

    /// <summary>创建签到业务异常</summary>
    /// <param name="message">错误描述</param>
    /// <param name="isTransient">是否为可重试的瞬时错误</param>
    /// <param name="innerException">内部异常</param>
    public CheckInException(string message, bool isTransient = false, Exception? innerException = null)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }
}
