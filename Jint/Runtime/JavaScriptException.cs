using System;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly object _errorObject;

        public JavaScriptException(object errorObject) : base(errorObject.ToString())
        {
            _errorObject = errorObject;
        }

        public JavaScriptException(object errorObject, string message):base(message)
        {
            _errorObject = errorObject;
        }

        public object Error { get { return _errorObject; } }
    }
}
