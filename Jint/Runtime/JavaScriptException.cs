using System.Text;
using Jint.Diagnostics;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime;

public class JavaScriptException : JintException
{
    /// <summary>
    /// The CLR message for a thrown value, read without calling anything the script could have installed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs wherever a throw is re-raised to leave a function, generator or <c>eval</c> body, once per
    /// frame it unwinds through, and neither <c>throw</c> nor <c>catch</c> reads <c>message</c>
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation). A <c>[[Get]]</c> here is a
    /// getter or a proxy trap the script can count, and one that throws replaces the value being thrown.
    /// </para>
    /// <para>
    /// So the text is <c>ToString(value.message)</c> only where descriptors decide it — a primitive in a data
    /// property on the value's chain, or <c>undefined</c> when nothing on the chain has one — and empty where
    /// only a call could: an accessor, a proxy on the walk, an object-valued <c>message</c>. Two readers are
    /// allowed past an accessor, because neither reaches script: a <c>DOMException</c>'s slot, which is what
    /// its intrinsic accessor answers, and a member of a wrapped CLR object, whose getter is the host's own.
    /// </para>
    /// <para>
    /// Eager rather than on first read of <see cref="Exception.Message"/>: a host reads that on any thread and
    /// at any time, and walking the chain then would race the engine and report the message as it is by then
    /// rather than as it was thrown.
    /// </para>
    /// </remarks>
    private static string? GetMessage(JsValue? error)
    {
        if (error is ObjectInstance oi)
        {
            return MessageOf(oi);
        }

        if (error is null)
        {
            return null;
        }

        // A primitive converts from its own state. ToString refuses a symbol, which takes its descriptive string.
        return error.IsSymbol() ? error.ToString() : TypeConverter.ToString(error);
    }

    /// <summary>
    /// <see cref="GetMessage"/> for an object: its <c>message</c> as far as descriptors can tell.
    /// </summary>
    private static string MessageOf(ObjectInstance error)
    {
        // An error constructed with a message - by far the most common thing thrown - holds it as an own data
        // property served from a field, and asking for the descriptor would allocate one to say the same.
        if (error is JsError { VirtualMessage: { } ownMessage })
        {
            return MessageText(ownMessage);
        }

        var descriptor = ValueSlotReader.FindOnPrototypeChain(error, CommonProperties.Message);
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            return MessageText(JsValue.Undefined);
        }

        if (descriptor is ReflectionDescriptor clrMember)
        {
            return MessageText(clrMember.Value);
        }

        if (descriptor is not null && !descriptor.IsAccessorDescriptor())
        {
            return MessageText(descriptor.Value ?? JsValue.Undefined);
        }

        return (error as IErrorTextSlots)?.ErrorMessageSlot?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// A primitive's string form, which runs nothing; an object's would call its <c>toString</c>.
    /// </summary>
    private static string MessageText(JsValue message) => message.IsObject() ? string.Empty : message.ToString();

    private readonly JavaScriptErrorWrapperException _jsErrorException;

    /// <summary>
    /// Whether this exception is a Throw completion re-raised so that it can leave a function, generator or
    /// <c>eval</c> body, rather than a throw being raised for the first time.
    /// </summary>
    /// <remarks>
    /// Set only by <see cref="Throw.JavaScriptException(Engine, JsValue, in Completion)"/>, whose every
    /// caller is such a boundary. It exists so that the debugger reports and stops once, where the throw
    /// happened, and not again in every frame the unwind passes through — each of which re-raises the same
    /// throw as a new instance of this class, so nothing else on the way up can tell the difference. Both
    /// <see cref="Debugger.DebugHandler.ExceptionThrown"/> and the pause are filtered by it; a fresh
    /// <c>throw e</c> of the very same value is a new throw and is reported as one.
    /// </remarks>
    internal bool _reRaisedAtBodyBoundary;

    public string? JavaScriptStackTrace => _jsErrorException.StackTrace;

    /// <summary>
    /// Where in the JavaScript source the error was raised, or <c>default</c> when nothing located it.
    /// </summary>
    /// <remarks>
    /// Returned by value, deliberately. A public property of an exception is read reflectively by everything
    /// that renders a failure - test runners, structured loggers, error pages - and .NET Framework's
    /// <see cref="System.Reflection.PropertyInfo.GetValue(object)"/> throws
    /// <see cref="NotSupportedException"/> on a by-ref-returning property rather than dereferencing it, so the
    /// reflection message replaces the failure the reader came for. See
    /// <see href="https://github.com/sebastienros/jint/issues/3549">#3549</see>.
    /// </remarks>
    public SourceLocation Location => _jsErrorException.Location;

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
    /// generic host-error message by default; enable
    /// <see cref="Options.InteropOptions.ExposeDetailedExceptionMessages"/> to use the exception's own message.
    /// <para>
    /// Prefer this over projecting the exception into the script
    /// (<c>new JavaScriptException(JsValue.FromObject(engine, ex))</c>). That value is not an <c>Error</c> — it
    /// has no <c>stack</c> and fails <c>instanceof Error</c> — and it hands the running script the exception's
    /// members, including its .NET stack trace and its inner exceptions.
    /// </para>
    /// </summary>
    public JavaScriptException(ErrorConstructor errorConstructor, string? message, Exception clrException)
        : this(errorConstructor, ResolveClrMessage(errorConstructor, message, clrException))
    {
        if (Error is ErrorInstance errorInstance)
        {
            errorInstance.SetClrException(clrException);
        }
    }

    private static string? ResolveClrMessage(ErrorConstructor errorConstructor, string? message, Exception clrException)
    {
        if (clrException is null)
        {
            Throw.ArgumentNullException(nameof(clrException));
        }

        return message
               ?? (errorConstructor.Engine.Options.Interop.ExposeDetailedExceptionMessages
                   ? clrException.Message
                   : Throw.GenericHostErrorMessage);
    }

    /// <summary>
    /// Creates an exception carrying a thrown JavaScript value, with a <see cref="Exception.Message"/> read from
    /// that value without running any script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The message is the value's <c>message</c> wherever a data property holds a string or another primitive
    /// there, <c>undefined</c> when nothing on its prototype chain has one, and the value itself as a string
    /// when it is a primitive.
    /// </para>
    /// <para>
    /// It is empty when only running code could produce it: a <c>message</c> getter, a proxy anywhere on the
    /// walk, or a <c>message</c> that is itself an object. Read <see cref="JavaScriptException.Error"/> on the
    /// engine's thread when the value itself is needed.
    /// </para>
    /// </remarks>
    /// <param name="error">The value the script threw, or a host wants it to see thrown.</param>
    public JavaScriptException(JsValue error)
        : this(GetMessage(error), error)
    {
    }

    /// <summary>
    /// Builds the outer exception and its wrapper from one message, read once.
    /// </summary>
    private JavaScriptException(string? message, JsValue error)
        : base(message, new JavaScriptErrorWrapperException(error, message, GetChainedClrException(error)))
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

    /// <summary>
    /// The JavaScript error and its JavaScript stack, as a script author would read it.
    /// <para>
    /// Deliberately never the CLR side of the story: a chained CLR exception
    /// (<see cref="Options.InteropOptions.ChainClrExceptionAsInnerException"/>) is rendered by
    /// <see cref="object.ToString"/> on this exception, which is the host-facing string form, and is reachable
    /// through <see cref="Exception.InnerException"/> and <see cref="JintException.TryGetClrException"/>. This
    /// accessor renders the same thing whether that option is on or off.
    /// </para>
    /// </summary>
    public string GetJavaScriptErrorString() => _jsErrorException.Render(includeChainedClrException: false);

    /// <summary>
    /// Returns the JavaScript error and stack while enforcing host output limits.
    /// </summary>
    /// <remarks>
    /// Reading a script-defined <c>stack</c> accessor can execute JavaScript, so this overload runs under the
    /// originating engine's execution constraints when the thrown value is an object.
    /// </remarks>
    public string GetJavaScriptErrorString(ResultLimits limits)
    {
        if (limits is null)
        {
            Throw.ArgumentNullException(nameof(limits));
        }

        if (Error is ObjectInstance objectInstance)
        {
            return objectInstance.Engine.ExecuteWithConstraints(
                objectInstance.Engine.Options.Strict,
                () => _jsErrorException.Render(includeChainedClrException: false, limits));
        }

        return _jsErrorException.Render(includeChainedClrException: false, limits);
    }

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

        /// <summary>
        /// Internal, and by design. This exception is the <see cref="Exception.InnerException"/> that a host's
        /// exception renderer walks into, and a <em>public</em> by-ref-returning property here takes .NET
        /// Framework reflection down exactly as one on the outer exception does.
        /// </summary>
        internal ref readonly SourceLocation Location => ref _location;

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

            // Does the Error object already carry a usable stack? HasProperty walks the prototype chain, and
            // %Error.prototype% exposes "stack" as an accessor (error-stack-accessor proposal) that returns
            // undefined for receivers without [[ErrorData]] — e.g. `Object.create(Error.prototype)` subclasses
            // such as WPT idlharness's IdlHarnessError. Only a string value counts as an existing stack;
            // anything else falls through to building one, instead of failing AsString() with a CLR
            // ArgumentException escaping out of an ordinary JavaScript `throw`.
            var existingStack = !overwriteExisting && errObj.HasProperty(CommonProperties.Stack)
                ? errObj.Get(CommonProperties.Stack)
                : JsValue.Undefined;
            if (existingStack.IsString())
            {
                _callStack = existingStack.AsString();
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

                errObj.DefineOwnPropertyUnchecked(CommonProperties.Stack._value, new PropertyDescriptor(_callStack, false, false, false));
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

        public override string ToString() => Render(includeChainedClrException: true);

        /// <summary>
        /// <paramref name="includeChainedClrException"/> is false for
        /// <see cref="JavaScriptException.GetJavaScriptErrorString()"/>, which promises the JavaScript error and
        /// nothing else; the string form of the exception itself keeps the chain.
        /// </summary>
        internal string Render(bool includeChainedClrException, ResultLimits? limits = null)
        {
            var sb = new ValueStringBuilder();
            try
            {
                AppendBounded(ref sb, "Error", limits, checkIndividualString: false);
                var message = Message;
                if (!string.IsNullOrEmpty(message))
                {
                    AppendBounded(ref sb, ": ", limits, checkIndividualString: false);
                    AppendBounded(ref sb, message, limits, checkIndividualString: true);
                }

                // Exception.ToString() renders an inner exception between the message and the frames. This override
                // replaces that rendering wholesale, so a chained CLR exception has to be spelled out here or it
                // would be reachable through InnerException and yet invisible in every log line.
                if (includeChainedClrException && InnerException is { } innerException)
                {
                    AppendBounded(ref sb, " ---> ", limits, checkIndividualString: false);
                    AppendBounded(ref sb, innerException.ToString(), limits, checkIndividualString: true);
                    AppendBounded(ref sb, Environment.NewLine, limits, checkIndividualString: false);
                    AppendBounded(
                        ref sb,
                        "   --- End of inner exception stack trace ---",
                        limits,
                        checkIndividualString: false);
                }

                var stackTrace = StackTrace;
                if (stackTrace != null)
                {
                    AppendBounded(ref sb, Environment.NewLine, limits, checkIndividualString: false);
                    AppendBounded(ref sb, stackTrace, limits, checkIndividualString: true);
                }

                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }

        private static void AppendBounded(
            ref ValueStringBuilder builder,
            string value,
            ResultLimits? limits,
            bool checkIndividualString)
        {
            if (limits is not null)
            {
                if (checkIndividualString && value.Length > limits.MaxStringLength)
                {
                    throw new ResultLimitExceededException(
                        ResultLimit.StringLength,
                        limits.MaxStringLength,
                        value.Length);
                }

                var observed = (long) builder.Length + value.Length;
                if (observed > limits.MaxOutputCharacters)
                {
                    throw new ResultLimitExceededException(
                        ResultLimit.OutputCharacters,
                        limits.MaxOutputCharacters,
                        observed);
                }
            }

            builder.Append(value);
        }
    }
}
