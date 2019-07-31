using System;
using Jint.Native;

namespace Jint.Runtime
{
    [Serializable]
    public class PromiseRejectedException : Exception
    {
        public JsValue RejectedValue { get; }

        public PromiseRejectedException(JsValue value) : base($"Promise was rejected with value {value}")
        {
            RejectedValue = value;
        }

    }
}
