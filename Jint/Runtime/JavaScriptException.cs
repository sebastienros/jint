#nullable enable

using Esprima;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Pooling;

namespace Jint.Runtime;

public class JavaScriptException : JintException
{
    private static string GetMessage(JsValue error)
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

    private readonly JavaScriptErrorWrapperException _jsErrorException;

    public string? JavaScriptStackTrace => _jsErrorException.StackTrace;
    public Location Location => _jsErrorException.Location;
    public JsValue Error => _jsErrorException.Error;

    internal JavaScriptException(ErrorConstructor errorConstructor)
        : base("", new JavaScriptErrorWrapperException(errorConstructor.Construct(Arguments.Empty), ""))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    internal JavaScriptException(ErrorConstructor errorConstructor, string? message)
        : base(message, new JavaScriptErrorWrapperException(errorConstructor.Construct(new JsValue[] { message }), message))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    internal JavaScriptException(JsValue error)
        : base(GetMessage(error), new JavaScriptErrorWrapperException(error, GetMessage(error)))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    public string GetJavaScriptErrorString() => _jsErrorException.ToString();

    public JavaScriptException SetJavaScriptCallstack(Engine engine, Location location, bool overwriteExisting = false)
    {
        _jsErrorException.SetCallstack(engine, location, overwriteExisting);
        return this;
    }

    public JavaScriptException SetJavaScriptLocation(Location location)
    {
        _jsErrorException.SetLocation(location);
        return this;
    }

    private class JavaScriptErrorWrapperException : JintException
    {
        private string? _callStack;

        public JsValue Error { get; }
        public Location Location { get; private set; }

        internal JavaScriptErrorWrapperException(JsValue error, string? message = null)
            : base(message ?? GetMessage(error))
        {
            Error = error;
        }

        internal void SetLocation(Location location)
        {
            Location = location;
        }

        internal void SetCallstack(Engine engine, Location location, bool overwriteExisting)
        {
            Location = location;

            var errObj = Error.IsObject() ? Error.AsObject() : null;
            if (errObj == null)
            {
                _callStack = engine.CallStack.BuildCallStackString(location);
                return;
            }

            // Does the Error object already have a stack property?
            if (errObj.HasProperty(CommonProperties.Stack) && !overwriteExisting)
            {
                _callStack = errObj.Get(CommonProperties.Stack).AsString();
            }
            else
            {
                _callStack = engine.CallStack.BuildCallStackString(location);
                errObj.FastAddProperty(CommonProperties.Stack, _callStack, false, false, false);
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
