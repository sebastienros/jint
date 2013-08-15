using System;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly object _errorObject;

        public JavaScriptException(object errorObject)
        {
            _errorObject = errorObject;
        }

        public object Error { get { return _errorObject; } }
    }
}
