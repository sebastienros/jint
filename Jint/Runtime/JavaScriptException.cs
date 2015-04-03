using System;
using Jint.Native;
using Jint.Native.Error;

namespace Jint.Runtime
{
    using Jint.Parser.Ast;

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

        public JavaScriptException(JsValue error, Statement lastStatement)
            : base(GetErrorMessage(error, lastStatement))
        {
            _errorObject = error;
        }

        private static string GetErrorMessage(JsValue error, Statement lastStatement) 
        {
            if (error.IsObject())
            {
                var oi = error.AsObject();
                var message = oi.Get("message").AsString();
                if (lastStatement != null)
                {
                    string location = string.Format(
                        "Ln: {0}, Col: {1}",
                        lastStatement.Location.Start.Line,
                        lastStatement.Location.Start.Column);

                    return string.Format("Error '{0}' thrown at {1}", message, location);
                }

                return message;
            }
            
            return string.Empty;            
        }

        public JsValue Error { get { return _errorObject; } }

        public override string ToString()
        {
            return _errorObject.ToString();
        }
    }
}
