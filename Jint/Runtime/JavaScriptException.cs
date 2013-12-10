using System;
using Jint.Native;
using Jint.Native.Error;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly JsValue _errorObject;

        public JavaScriptException(ErrorConstructor errorConstructor) : base("")
        {
            _errorObject = errorConstructor.Construct(Arguments.Empty);
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string message)
            : base(message)
        {
            _errorObject = errorConstructor.Construct(new JsValue[] { message });
        }

        public JavaScriptException(JsValue error)
            : base("")
        {
            _errorObject = error;
        }

        public JsValue Error { get { return _errorObject; } }
    }
}
