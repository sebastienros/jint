using Jint.Native;

namespace Jint.Runtime
{
    public class PromiseRejectedException : JintException
    {
        public JsValue RejectedValue { get; }

        public PromiseRejectedException(JsValue value) : base($"Promise was rejected with value {value}")
        {
            RejectedValue = value;
        }

    }
}