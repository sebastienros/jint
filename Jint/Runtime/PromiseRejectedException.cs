using Jint.Native;

namespace Jint.Runtime;

public sealed class PromiseRejectedException : JintException
{
    public PromiseRejectedException(JsValue value) : base($"Promise was rejected with value {value}")
    {
        RejectedValue = value;
    }

    public JsValue RejectedValue { get; }
}
