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

        internal JavaScriptException SetLocation(Location location)
        {
            Location = location;

            return this;
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
        /// Returns the call stack of the JavaScript exception.
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

        public Location Location { get; protected set; }

        public int LineNumber => Location.Start.Line;

        public int Column => Location.Start.Column;

        public override string ToString()
        {
            using var rent = StringBuilderPool.Rent();
            var sb = rent.Builder;
            
            sb.Append("Error");
            var message = Message;
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(": ");
                sb.Append(message);
            }

            var stackTrace = StackTrace;
            if (stackTrace != null)
            {
                sb.Append(Environment.NewLine);
                sb.Append(stackTrace);
            }

            return rent.ToString();
        }
    }
}