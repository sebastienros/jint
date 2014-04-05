using System;
using Jint.Native;
using Jint.Native.Error;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly JsValue _errorObject;
        public Jint.Parser.Location Location;

        public JavaScriptException(ErrorConstructor errorConstructor, Jint.Parser.Location location = null) : base("")
        {
            _errorObject = errorConstructor.Construct(Arguments.Empty);
            InitLocation(location);
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string message, Jint.Parser.Location location = null)
            : base(message)
        {
            _errorObject = errorConstructor.Construct(new JsValue[] { message });
            InitLocation(location);
        }

        public JavaScriptException(JsValue error, Jint.Parser.Location location = null)
            : base(GetErrorMessage(error))
        {
            _errorObject = error;
            InitLocation(location);
        }

        private static string GetErrorMessage(JsValue error) 
        {
            if (error.IsObject())
            {
                var oi = error.AsObject();
                var message = oi.Get("message").AsString();
                return message;
            }
            else
                return string.Empty;            
        }

        public JsValue Error { get { return _errorObject; } }

        public override string ToString()
        {
            return _errorObject.ToString();
        }

        public void InitLocation(Jint.Parser.Location location)
        {
            if(location != null && !this.Location.IsInitialized)
            {
                this.Location = location.Clone();
            }
        }

        public void InitLocation(Jint.Parser.Ast.Statement statement)
        {
            if (statement != null)
            {
                InitLocation(statement.Location);
            }
        }
    }
}
