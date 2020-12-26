#nullable enable

using System;
using Esprima;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;

namespace Jint.Runtime
{
    public class JavaScriptException : JintException
    {
        private string? _callStack;

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

        internal JavaScriptException SetCallstack(Engine engine, Location location, bool root = false)
        {
            Location = location;
            CallStack = engine.CallStack.BuildCallStackString(location);

            return this;
        }

        private static string GetErrorMessage(JsValue error)
        {
            if (error is ObjectInstance oi)
            {
                return oi.Get("message", oi).ToString();
            }

            return error.ToString();
        }

        public JsValue Error { get; }

        public override string ToString()
        {
            var str = TypeConverter.ToString(Error);
            var callStack = CallStack;
            if (!string.IsNullOrWhiteSpace(callStack))
            {
                str += Environment.NewLine + callStack;
            }
            return str;
        }

        /// <summary>
        /// Returns the call stack of the exception. Requires that engine was built using
        /// <see cref="Options.CollectStackTrace"/>.
        /// </summary>
        public string? CallStack
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
            set
            {
                _callStack = value;
                if (value != null && Error.IsObject())
                {
                    Error.AsObject()
                        .FastAddProperty(CommonProperties.Stack, new JsString(value), false, false, false);
                }
            }
        }

        public Location Location { get; private set; }

        public int LineNumber => Location.Start.Line;

        public int Column => Location.Start.Column;
    }
}
