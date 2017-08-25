using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Parser;
using Jint.Parser.Ast;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime
{
    public class JavaScriptException : Exception
    {
        private readonly JsValue _errorObject;
        private string _callStack;

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
            : base(GetErrorMessage(error))
        {
            _errorObject = error;
        }

        public JavaScriptException SetCallstack(Engine engine, Location location = null)
        {
            Location = location;
            var sb = new StringBuilder();
            foreach (var cse in engine.CallStack)
            {
                sb.Append(" at ")
                    .Append(cse)
                    .Append("(");

                for (var index = 0; index < cse.CallExpression.Arguments.Count; index++)
                {
                    if (index != 0)
                        sb.Append(", ");
                    var arg = cse.CallExpression.Arguments[index];
                    if (arg is IPropertyKeyExpression pke)
                        sb.Append(pke.GetKey());
                    else
                        sb.Append(arg);
                }


                sb.Append(") @ ")
                    .Append(cse.CallExpression.Location.Source)
                    .Append(" ")
                    .Append(cse.CallExpression.Location.Start.Column)
                    .Append(":")
                    .Append(cse.CallExpression.Location.Start.Line)
                    .AppendLine();
            }
            CallStack = sb.ToString();
            return this;
        }

        private static string GetErrorMessage(JsValue error) 
        {
            if (error.IsObject())
            {
                var oi = error.AsObject();
                var message = oi.Get("message").AsString();
                return message;
            }
            if (error.IsString())
                return error.AsString();
            
            return error.ToString();
        }

        public JsValue Error { get { return _errorObject; } }

        public override string ToString()
        {
            return _errorObject.ToString();
        }

        public string CallStack
        {
            get
            {
                if (_callStack != null)
                    return _callStack;
                if (_errorObject == null)
                    return null;
                if (_errorObject.IsObject() == false)
                    return null;
                var callstack = _errorObject.AsObject().Get("callstack");
                if (callstack == JsValue.Undefined)
                    return null;
                return callstack.AsString();
            }
            set
            {
                _callStack = value;
                if (value != null && _errorObject.IsObject())
                {
                    _errorObject.AsObject()
                        .FastAddProperty("callstack", new JsValue(value), false, false, false);
                }
            }
        }

        public Jint.Parser.Location Location { get; set; }

        public int LineNumber { get { return null == Location ? 0 : Location.Start.Line; } }

        public int Column { get { return null == Location ? 0 : Location.Start.Column; } }
    }
}
