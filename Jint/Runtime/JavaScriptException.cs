using System;
using Jint.Native.Error;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly object _errorObject;

        public JavaScriptException(ErrorConstructor errorConstructor) : base("")
        {
            _errorObject = errorConstructor.Construct(Arguments.Empty);
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string message)
            : base(message)
        {
            _errorObject = errorConstructor.Construct(new object[] { message });
        }

        public JavaScriptException(object error)
            : base("")
        {
            _errorObject = error;
        }

        public object Error { get { return _errorObject; } }
    }
}
