using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime;

public class JavaScriptException : JintException
{
    private static string? GetMessage(JsValue? error)
    {
        string? ret = null;
        if (error is ObjectInstance oi)
        {
            ret = oi.Get(CommonProperties.Message).ToString();
        }
        else if (error is not null)
        {
            ret = error.IsSymbol() ? error.ToString() : TypeConverter.ToString(error);
        }

        return ret;
    }

    private readonly JavaScriptErrorWrapperException _jsErrorException;

    public string? JavaScriptStackTrace => _jsErrorException.StackTrace;
    public ref readonly SourceLocation Location => ref _jsErrorException.Location;
    public JsValue Error => _jsErrorException.Error;

    internal JavaScriptException(ErrorConstructor errorConstructor)
        : base("", new JavaScriptErrorWrapperException(errorConstructor.Construct(), ""))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    public JavaScriptException(ErrorConstructor errorConstructor, string? message = null)
        : base(message, new JavaScriptErrorWrapperException(errorConstructor.Construct(message), message))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;

        // A frameless built-in must not be able to raise a JavaScript error: the error's stack would
        // be missing the built-in's own frame, which user code can read.
        Native.Function.LeafCallGuard.AssertNotInLeafCall("JavaScript error construction");
    }

    /// <summary>
    /// Creates an exception carrying a JavaScript error that stands in for <paramref name="clrException"/>.
    /// Throw this from host code — a <see cref="Jint.Runtime.Interop.ClrFunction"/> delegate, a module export —
    /// to raise an error the script can catch while the originating CLR exception still reaches the host, read
    /// there with <see cref="JintException.TryGetClrException"/>. A null <paramref name="message"/> takes the
    /// exception's own message.
    /// <para>
    /// Prefer this over projecting the exception into the script
    /// (<c>new JavaScriptException(JsValue.FromObject(engine, ex))</c>). That value is not an <c>Error</c> — it
    /// has no <c>stack</c> and fails <c>instanceof Error</c> — and it hands the running script the exception's
    /// members, including its .NET stack trace and its inner exceptions.
    /// </para>
    /// </summary>
    public JavaScriptException(ErrorConstructor errorConstructor, string? message, Exception clrException)
        : this(errorConstructor, ResolveClrMessage(message, clrException))
    {
        if (Error is ErrorInstance errorInstance)
        {
            errorInstance.SetClrException(clrException);
        }
    }

    private static string? ResolveClrMessage(string? message, Exception clrException)
    {
        if (clrException is null)
        {
            Throw.ArgumentNullException(nameof(clrException));
        }

        return message ?? clrException.Message;
    }

    public JavaScriptException(JsValue error)
        : base(GetMessage(error), new JavaScriptErrorWrapperException(error, GetMessage(error), GetChainedClrException(error)))
    {
        _jsErrorException = (JavaScriptErrorWrapperException) InnerException!;
    }

    /// <summary>
    /// The CLR exception to hang under the wrapper, or null when the error carries none or the engine was not
    /// asked to chain it. Read off the error <em>value</em> here rather than carried on the exception instance,
    /// because this constructor is what every reconstruction of a thrown error passes through — the interpreter
    /// keeps only the error value across a throw completion, and the exception the host finally catches is built
    /// here. Reading it here is therefore what makes the chain appear at all of those sites rather than only
    /// where the error was originally built.
    /// </summary>
    private static Exception? GetChainedClrException(JsValue? error)
    {
        return error is ErrorInstance { ClrException: { } clrException } errorInstance
               && errorInstance.Engine.Options.Interop.ChainClrExceptionAsInnerException
            ? clrException
            : null;
    }

    public string GetJavaScriptErrorString() => _jsErrorException.ToString();

    /// <summary>
    /// Returns this exception as the base exception.
    /// The inner exception is a private implementation detail and should not be exposed.
    /// </summary>
    public override Exception GetBaseException() => this;

    public JavaScriptException SetJavaScriptCallstack(Engine engine, in SourceLocation location, bool overwriteExisting = false)
    {
        _jsErrorException.SetCallstack(engine, in location, overwriteExisting);
        return this;
    }

    public JavaScriptException SetJavaScriptLocation(in SourceLocation location)
    {
        _jsErrorException.SetLocation(in location);
        return this;
    }

    private sealed class JavaScriptErrorWrapperException : JintException
    {
        private string? _callStack;
        private SourceLocation _location;

        internal JavaScriptErrorWrapperException(JsValue error, string? message = null, Exception? clrException = null)
            : base(message ?? GetMessage(error), clrException)
        {
            Error = error;
        }

        public JsValue Error { get; }

        public ref readonly SourceLocation Location => ref _location;

        internal void SetLocation(in SourceLocation location)
        {
            _location = location;
        }

        internal void SetCallstack(Engine engine, in SourceLocation location, bool overwriteExisting)
        {
            _location = location;

            var errObj = Error.IsObject() ? Error.AsObject() : null;
            if (errObj is null)
            {
                _callStack = engine.CallStack.BuildCallStackString(engine, location);
                return;
            }

            // Does the Error object already have a stack property?
            if (errObj.HasProperty(CommonProperties.Stack) && !overwriteExisting)
            {
                _callStack = errObj.Get(CommonProperties.Stack).AsString();
            }
            else
            {
                _callStack = engine.CallStack.BuildCallStackString(engine, location);

                // An error's message is served from a virtual field until first materialized. Installing the
                // stack via a raw store would otherwise land before the message in own-key order; materialize
                // the message first so it keeps its construction-time position (message then stack).
                if (errObj is JsError jsError)
                {
                    jsError.EnsureMessageMaterialized();
                }

                errObj.FastSetProperty(CommonProperties.Stack._value, new PropertyDescriptor(_callStack, false, false, false));
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
            var sb = new ValueStringBuilder();

            sb.Append("Error");
            var message = Message;
            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(": ");
                sb.Append(message);
            }

            // Exception.ToString() renders an inner exception between the message and the frames. This override
            // replaces that rendering wholesale, so a chained CLR exception has to be spelled out here or it
            // would be reachable through InnerException and yet invisible in every log line.
            if (InnerException is { } innerException)
            {
                sb.Append(" ---> ");
                sb.Append(innerException.ToString());
                sb.Append(Environment.NewLine);
                sb.Append("   --- End of inner exception stack trace ---");
            }

            var stackTrace = StackTrace;
            if (stackTrace != null)
            {
                sb.Append(Environment.NewLine);
                sb.Append(stackTrace);
            }

            return sb.ToString();
        }
    }
}
