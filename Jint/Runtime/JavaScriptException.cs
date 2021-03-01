#nullable enable

using System;
using Esprima;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Pooling;

namespace Jint.Runtime
{
    public class JavaScriptException : JintException
    {
        private string? _callStack;

        public JavaScriptException(ErrorConstructor errorConstructor) : base("")
        {
            Error = errorConstructor.Construct(Arguments.Empty);
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string? message, Exception? innerException)
             : base(message, innerException)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        public JavaScriptException(ErrorConstructor errorConstructor, string? message)
            : base(message)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        public JavaScriptException(JsValue error)
        {
            Error = error;
        }

        internal JavaScriptException SetCallstack(Engine engine, Location location)
        {
            Location = location;
            var value = engine.CallStack.BuildCallStackString(location);
            _callStack = value;
            if (Error.IsObject())
            {
                Error.AsObject()
                    .FastAddProperty(CommonProperties.Stack, new JsString(value), false, false, false);
            }

            return this;
        }

        private string? GetErrorMessage()
        {
            if (Error is ObjectInstance oi)
            {
                return oi.Get(CommonProperties.Message).ToString();
            }

            return null;
        }

        public JsValue Error { get; }

        public override string Message => GetErrorMessage() ?? TypeConverter.ToString(Error);

        /// <summary>
        /// Returns the call stack of the exception. Requires that engine was built using
        /// <see cref="Options.CollectStackTrace"/>.
        /// </summary>
        public override string? StackTrace
        {
            get
            {
                if (_callStack is not null)
                {
                    return _callStack;
                }

                if (Error is not ObjectInstance oi)
                {
                    return null;
                }

                var callstack = oi.Get(CommonProperties.Stack, Error);

                return callstack.IsUndefined()
                    ? null 
                    : callstack.AsString();
            }
        }

        public Location Location { get; private set; }

        public int LineNumber => Location.Start.Line;

        public int Column => Location.Start.Column;

        public override string ToString()
        {
            // adapted custom version as logic differs between full framework and .NET Core
            var className = GetType().ToString();
            var message = Message;
            var innerExceptionString = InnerException?.ToString() ?? "";
            const string endOfInnerExceptionResource = "--- End of inner exception stack trace ---";
            var stackTrace = StackTrace;
 
            using var rent = StringBuilderPool.Rent();
            var sb = rent.Builder;
            sb.Append(className);
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(": ");
                sb.Append(message);
            }
            if (InnerException != null)
            {
                sb.Append(Environment.NewLine);
                sb.Append(" ---> ");
                sb.Append(innerExceptionString);
                sb.Append(Environment.NewLine);
                sb.Append("   ");
                sb.Append(endOfInnerExceptionResource);
            }
            if (stackTrace != null)
            {
                sb.Append(Environment.NewLine);
                sb.Append(stackTrace);
            }
 
            return rent.ToString();
        }
    }
}
