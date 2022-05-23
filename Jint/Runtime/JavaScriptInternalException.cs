#nullable enable

using System;
using Esprima;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Pooling;

namespace Jint.Runtime
{
    public class JavaScriptInternalException : JavaScriptException
    {
        private string? _callStack;
        private Location _location;

        internal JavaScriptInternalException(ErrorConstructor errorConstructor) : base("")
        {
            Error = errorConstructor.Construct(Arguments.Empty);
        }

        internal JavaScriptInternalException(ErrorConstructor errorConstructor, string? message, Exception? innerException)
            : base(message, innerException)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        internal JavaScriptInternalException(ErrorConstructor errorConstructor, string? message)
            : base(message)
        {
            Error = errorConstructor.Construct(new JsValue[] { message });
        }

        internal JavaScriptInternalException(JsValue error)
        {
            Error = error;
        }

        internal JavaScriptInternalException SetLocation(Location location)
        {
            _location = location;

            return this;
        }

        internal JavaScriptInternalException SetCallstack(Engine engine, Location location)
        {
            _location = location;
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

        public override JsValue Error { get; }

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

        public override Location Location => _location;

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