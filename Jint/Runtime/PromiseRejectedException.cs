using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
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
