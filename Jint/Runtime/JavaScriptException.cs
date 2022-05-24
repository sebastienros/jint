#nullable enable

using System;
using Esprima;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Pooling;

namespace Jint.Runtime;

public class JavaScriptException : JintException
{
    private readonly JavaScriptErrorWrapperException _jsErrorException;

    public string? JavaScriptStackTrace => _jsErrorException.StackTrace;
    public Location Location => _jsErrorException.Location;
    public int LineNumber => Location.Start.Line;
    public int Column => Location.Start.Column;
    public JsValue? Error => _jsErrorException.Error;
    
    internal JavaScriptException(ErrorConstructor errorConstructor)
        : base("", new JavaScriptErrorWrapperException(errorConstructor.Construct(Arguments.Empty)))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    internal JavaScriptException(ErrorConstructor errorConstructor, string? message)
        : base(message, new JavaScriptErrorWrapperException(errorConstructor.Construct(new JsValue[] { message })))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }
    
    internal JavaScriptException(JsValue error)
        : base(JavaScriptErrorWrapperException.GetMessage(error), new JavaScriptErrorWrapperException(error))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }
    
    public string GetJavaScriptErrorString() => _jsErrorException.ToString();
    
    public JavaScriptException SetCallstack(Engine engine, Location location)
    {
        _jsErrorException.SetCallstack(engine, location);
        return this;
    }
    
    public JavaScriptException SetLocation(Location location)
    {
        _jsErrorException.SetLocation(location);
        return this;
    }

    internal class JavaScriptErrorWrapperException : JintException
    {
        internal static string GetMessage(JsValue error)
        {
            string? ret;
            if (error is ObjectInstance oi)
            {
                ret = oi.Get(CommonProperties.Message).ToString();
            }
            else
            {
                ret = null;
            }

            return ret ?? TypeConverter.ToString(error);
        }

        private string? _callStack;

        private Location _location;

        internal JavaScriptErrorWrapperException(JsValue error)
            : base(GetMessage(error))
        {
            Error = error;
        }

        internal JavaScriptErrorWrapperException SetLocation(Location location)
        {
            _location = location;

            return this;
        }

        internal JavaScriptErrorWrapperException SetCallstack(Engine engine, Location location)
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

        public JsValue Error { get; }

        public override string Message
        {
            get
            {
                return GetMessage(Error);
            }
        }

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

        public Location Location => _location;

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
