using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Error;
using Jint.Pooling;

namespace Jint.Runtime
{
    public class JavaScriptException : JintException
    {
        private string _callStack;

        public JavaScriptException(ErrorConstructor errorConstructor) : base("")
        {
            Error = errorConstructor.Construct(Arguments.Empty);
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string message, Exception innerException)
             : base(message, innerException)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string message)
            : base(message)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        public JavaScriptException(JsValue error)
            : base(GetErrorMessage(error))
        {
            Error = error;
        }

        public JavaScriptException SetCallstack(Engine engine, Location? location = null)
        {
            Location = location ?? default;

            using (var sb = StringBuilderPool.Rent())
            {
                foreach (var cse in engine.CallStack)
                {
                    sb.Builder.Append(" at ")
                        .Append(cse)
                        .Append("(");

                    for (var index = 0; index < cse.CallExpression.Arguments.Count; index++)
                    {
                        if (index != 0)
                        {
                            sb.Builder.Append(", ");
                        }
                        var arg = cse.CallExpression.Arguments[index];
                        if (arg is Expression pke)
                        {
                            sb.Builder.Append(GetPropertyKey(pke));
                        }
                        else
                        {
                            sb.Builder.Append(arg);
                        }
                    }

                    sb.Builder.Append(") @ ")
                        .Append(cse.CallExpression.Location.Source)
                        .Append(" ")
                        .Append(cse.CallExpression.Location.Start.Column)
                        .Append(":")
                        .Append(cse.CallExpression.Location.Start.Line)
                        .AppendLine();
                }
                CallStack = sb.ToString();
            }

            return this;
        }

        /// <summary>
        /// A version of <see cref="EsprimaExtensions.GetKey"/> that cannot get into loop as we are already building a stack.
        /// </summary>
        private static string GetPropertyKey(Expression expression)
        {
            if (expression is Literal literal)
            {
                return EsprimaExtensions.LiteralKeyToString(literal);
            }

            if (expression is Identifier identifier)
            {
                return identifier.Name;
            }

            if (expression is StaticMemberExpression staticMemberExpression)
            {
                return GetPropertyKey(staticMemberExpression.Object) + "." + GetPropertyKey(staticMemberExpression.Property);
            }

            return "?";
        }

        private static string GetErrorMessage(JsValue error)
        {
            if (error.IsObject())
            {
                var oi = error.AsObject();
                var message = oi.Get("message", oi).ToString();
                return message;
            }
            if (error.IsString())
                return error.ToString();

            return error.ToString();
        }

        public JsValue Error { get; }

        public override string ToString()
        {
            return Error.ToString();
        }

        public string CallStack
        {
            get
            {
                if (_callStack != null)
                    return _callStack;
                if (ReferenceEquals(Error, null))
                    return null;
                if (Error.IsObject() == false)
                    return null;
                var callstack = Error.AsObject().Get("callstack", Error);
                if (callstack.IsUndefined())
                    return null;
                return callstack.AsString();
            }
            set
            {
                _callStack = value;
                if (value != null && Error.IsObject())
                {
                    Error.AsObject()
                        .FastAddProperty("callstack", new JsString(value), false, false, false);
                }
            }
        }

        public Location Location { get; set; }

        public int LineNumber => Location.Start.Line;

        public int Column => Location.Start.Column;
    }
}
