using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime.CallStack;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Runtime;

/// <summary>
/// Wraps known runtime type error information.
/// </summary>
internal sealed record ErrorDispatchInfo(ErrorConstructor ErrorConstructor, string? Message = null);

internal static class Throw
{
    /// <summary>
    /// Renders <paramref name="value"/> for an error message without ever running user JavaScript.
    /// <para>
    /// This is the sanctioned way to put a <see cref="JsValue"/> into a <c>Throw.*</c> message. Writing
    /// <c>$"{value} is not a function"</c> instead is a bug: the message argument is evaluated eagerly, and
    /// <see cref="Native.Object.ObjectInstance.ToString"/> is <see cref="TypeConverter.ToString(JsValue)"/>
    /// — the full JavaScript ToString algorithm, which reads <c>@@toPrimitive</c>, reads <c>toString</c>,
    /// <b>calls it</b>, and reads <c>@@toStringTag</c>. That makes building the error observable (the extra
    /// <c>[[Get]]</c>s show up in a Proxy log) and lets a user <c>toString</c> that throws replace the error
    /// with its own exception.
    /// </para>
    /// <para>
    /// Every non-object <see cref="JsValue"/> renders from its own state and is therefore safe:
    /// <see cref="JsString"/> (and its concatenated/sliced forms) hands back its characters,
    /// <see cref="JsNumber"/>, <see cref="JsBigInt"/>, <see cref="JsBoolean"/>, <see cref="JsNull"/> and
    /// <see cref="JsUndefined"/> are pure conversions, and <see cref="JsSymbol"/> renders
    /// SymbolDescriptiveString over an own field, so a symbol keeps its description. An object has no safe
    /// value rendering at all, so it is reported as its shape rather than its contents.
    /// </para>
    /// <para>
    /// Prefer this wherever the offending <i>value</i> is what the reader needs. Where the reader needs the
    /// <i>kind</i> of thing that was passed rather than which one, <c>value.Type</c> is the better answer and
    /// is equally safe; and where a guard already proves the value is a primitive, interpolating it directly
    /// is fine — say so in a comment, because the next sweep for this bug class will look at that line again.
    /// </para>
    /// </summary>
    public static string SafeToDisplayString(JsValue value)
    {
        return value.IsObject() ? "[object]" : value.ToString();
    }

    internal const string GenericHostErrorMessage = "A host operation failed.";

    internal static bool IsEngineAbortException(Exception exception)
        => EngineAbortRegistry.Exceptions.TryGetValue(exception, out _);

    internal static bool MustPropagateHostException(Exception exception) => exception
        is global::Jint.Runtime.ExecutionCanceledException
        or global::Jint.ParsingLimitException
        or global::Jint.Runtime.MemoryLimitExceededException
        or global::Jint.Runtime.ResultLimitExceededException
        or global::Jint.Runtime.StatementsCountOverflowException
        or global::Jint.Runtime.RecursionDepthOverflowException
        or global::Jint.Runtime.Modules.ModuleGraphLimitException
        or System.Text.RegularExpressions.RegexMatchTimeoutException
        or System.PlatformNotSupportedException
        or System.OutOfMemoryException
        || IsEngineAbortException(exception);

    private static void MarkEngineAbort(Exception exception)
        => EngineAbortRegistry.Exceptions.Add(exception, EngineAbortRegistry.Marker);

    private static class EngineAbortRegistry
    {
        internal static readonly ConditionalWeakTable<Exception, object> Exceptions = new();
        internal static readonly object Marker = new();
    }
    [DoesNotReturn]
    public static void SyntaxError(Realm realm, string? message = null)
    {
        throw CreateSyntaxError(realm, message);
    }

    [DoesNotReturn]
    public static void SyntaxError(Realm realm, string message, in SourceLocation location)
    {
        throw CreateSyntaxError(realm, message).SetJavaScriptLocation(in location);
    }

    public static JavaScriptException CreateSyntaxError(Realm realm, string? message)
    {
        return new JavaScriptException(realm.Intrinsics.SyntaxError, message);
    }

    [DoesNotReturn]
    public static void ArgumentException(string? message = null)
    {
        ArgumentException(message, paramName: null);
    }

    [DoesNotReturn]
    public static void ArgumentException(string? message, string? paramName)
    {
        throw new ArgumentException(message, paramName);
    }

    [DoesNotReturn]
    public static void ReferenceError(Realm realm, Reference reference)
    {
        ReferenceNameError(realm, reference?.ReferencedName?.ToString());
    }

    [DoesNotReturn]
    public static void ReferenceNameError(Realm realm, string? name)
    {
        var message = name != null ? name + " is not defined" : null;
        ReferenceError(realm, message);
    }

    [DoesNotReturn]
    public static void ReferenceError(Realm realm, string? message)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.ReferenceError, message).SetJavaScriptLocation(in location);
    }

    [DoesNotReturn]
    public static void SyntaxErrorNoEngine(string? message = null)
    {
        throw new SyntaxErrorException(message);
    }

    [DoesNotReturn]
    public static void TypeErrorNoEngine(string? message = null, Node? source = null)
    {
        throw new TypeErrorException(message, source);
    }

    [DoesNotReturn]
    public static void TypeError(Realm realm, string? message = null)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.TypeError, message).SetJavaScriptLocation(in location);
    }

    /// <summary>
    /// Throws the TypeError used when an interop method or constructor call cannot be resolved.
    /// The originating CLR <paramref name="clrType"/> (and optional <paramref name="memberName"/>) are
    /// recorded on the error object so the host can read them via <see cref="JintException.TryGetClrType"/>
    /// without parsing the message. They are CLR fields, not JavaScript properties, so the running script
    /// cannot observe them; this is independent of <see cref="Options.InteropOptions.ExposeDetailedResolutionErrors"/>.
    /// A configured <see cref="Options.InteropOptions.ClrResolutionErrorDecorator"/> runs against the error
    /// object before the exception is created, so a rewritten <c>message</c> also becomes <see cref="Exception.Message"/>.
    /// </summary>
    [DoesNotReturn]
    public static void InteropResolutionError(
        Realm realm,
        string message,
        Type clrType,
        string? memberName,
        JsCallArguments arguments,
        MethodDescriptor[]? candidates)
    {
        var engine = realm.GlobalObject.Engine;
        var location = engine.GetLastSyntaxElement()?.Location ?? default;

        var error = realm.Intrinsics.TypeError.Construct(message);

        // The error value survives the interpreter's throw-completion reconstruction (the .NET exception
        // instance does not), so record the CLR origin on the error object rather than on Exception.Data.
        if (error is ErrorInstance errorInstance)
        {
            errorInstance.SetClrResolutionInfo(clrType, memberName);
        }

        var decorator = engine.Options.Interop.ClrResolutionErrorDecorator;
        if (decorator is not null)
        {
            // the info object (and its argument copy) is only built when a decorator is configured - cold path
            decorator(engine, error, new ClrResolutionErrorInfo(clrType, memberName, arguments, candidates));
        }

        throw new JavaScriptException(error).SetJavaScriptLocation(in location);
    }

    [DoesNotReturn]
    public static void RangeError(Realm realm, string? message = null)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.RangeError, message).SetJavaScriptLocation(in location);
    }

    [DoesNotReturn]
    public static void UriError(Realm realm, string? message = null)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.UriError, message).SetJavaScriptLocation(in location);
    }

    public static ErrorDispatchInfo CreateUriError(Realm realm, string message)
    {
        return new ErrorDispatchInfo(realm.Intrinsics.UriError, message);
    }

    public static ErrorDispatchInfo CreateRangeError(Realm realm, string message)
    {
        return new ErrorDispatchInfo(realm.Intrinsics.RangeError, message);
    }

    [DoesNotReturn]
    public static void NotImplementedException(string? message = null)
    {
        throw new NotImplementedException(message);
    }

    [DoesNotReturn]
    public static void ArgumentOutOfRangeException(string paramName, string message)
    {
        throw new ArgumentOutOfRangeException(paramName, message);
    }

    [DoesNotReturn]
    public static void OverflowException(string message)
    {
        throw new OverflowException(message);
    }

    [DoesNotReturn]
    public static void TimeoutException()
    {
        var exception = new TimeoutException();
        MarkEngineAbort(exception);
        throw exception;
    }

    [DoesNotReturn]
    public static void TimeoutException(string message)
    {
        var exception = new TimeoutException(message);
        MarkEngineAbort(exception);
        throw exception;
    }

    [DoesNotReturn]
    public static void OperationCanceledException(CancellationToken cancellationToken)
    {
        var exception = new OperationCanceledException(cancellationToken);
        MarkEngineAbort(exception);
        throw exception;
    }

    [DoesNotReturn]
    public static void StatementsCountOverflowException()
    {
        throw new StatementsCountOverflowException();
    }

    [DoesNotReturn]
    public static void ArgumentOutOfRangeException()
    {
#pragma warning disable MA0015
        throw new ArgumentOutOfRangeException();
#pragma warning restore MA0015
    }

    [DoesNotReturn]
    public static void NotSupportedException(string? message = null)
    {
        throw new NotSupportedException(message);
    }

    [DoesNotReturn]
    public static void InvalidOperationException(string? message = null, Exception? exception = null)
    {
        throw new InvalidOperationException(message, exception);
    }

    /// <summary>
    /// Shared by the two <see cref="Options"/> read-only refusals, so they cannot drift.
    /// </summary>
    private const string OptionsReadOnlyExplanation =
        ": these options are read-only because an engine has been built from them. "
        + "Most settings are read off Engine.Options while the engine runs, so a later change would reach an engine that already exists. "
        + "Configure the options fully before constructing the engine, or build the second engine from its own Options instance.";

    /// <summary>
    /// A write to an <see cref="Options"/> setting or registry after an engine has been built from it.
    /// </summary>
    /// <param name="setting">The full path of the setting, e.g. <c>Options.Interop.AllowWrite</c>.</param>
    [DoesNotReturn]
    public static void OptionsReadOnly(string setting)
    {
        throw new InvalidOperationException(setting + " cannot be changed" + OptionsReadOnlyExplanation);
    }

    /// <summary>
    /// A configuration method called on an <see cref="Options"/> an engine has already been built from.
    /// </summary>
    /// <param name="method">The method the host called, e.g. <c>Options.AddLazyGlobal</c>.</param>
    [DoesNotReturn]
    public static void OptionsReadOnlyCall(string method)
    {
        throw new InvalidOperationException(method + " cannot be called" + OptionsReadOnlyExplanation);
    }

    [DoesNotReturn]
    public static void PromiseRejectedException(JsValue error)
    {
        throw new PromiseRejectedException(error);
    }

    [DoesNotReturn]
    public static void JavaScriptException(Engine engine, JsValue value, in Completion result)
    {
        throw new JavaScriptException(value).SetJavaScriptCallstack(engine, result.Location);
    }

    [DoesNotReturn]
    public static void JavaScriptException(Engine engine, JsValue value, in SourceLocation location)
    {
        throw new JavaScriptException(value).SetJavaScriptCallstack(engine, in location);
    }

    [DoesNotReturn]
    public static void JavaScriptException(ErrorConstructor errorConstructor, string message)
    {
        throw new JavaScriptException(errorConstructor, message);
    }

    /// <summary>
    /// Creates and throws a JavaScript exception from a CLR exception, applying any configured decorator.
    /// When the engine has an active syntax element, the resulting <see cref="Jint.Runtime.JavaScriptException"/>
    /// is annotated with the current JavaScript source location and call-stack string.
    /// </summary>
    [DoesNotReturn]
    public static void FromClrException(Engine engine, Exception clrException)
    {
        if (ModuleLoadCompletion.MustPropagate(clrException))
        {
            ExceptionDispatchInfo.Capture(clrException).Throw();
        }

        var message = engine.Options.Interop.ExposeDetailedExceptionMessages
            ? clrException.Message
            : GenericHostErrorMessage;
        var error = CreateClrError(engine, clrException, message);

        var jsException = new JavaScriptException(error);
        var location = engine._lastSyntaxElement?.Location ?? default;
        if (location != default)
        {
            // overwriteExisting:false preserves a "stack" property a decorator may have written
            // (e.g. copying clrException.StackTrace onto the JS Error).
            jsException.SetJavaScriptCallstack(engine, in location, overwriteExisting: false);
        }

        throw jsException;
    }

    internal static ObjectInstance CreateClrError(
        Engine engine,
        Exception clrException,
        string message,
        ErrorConstructor? errorConstructor = null,
        bool moduleErrorPolicyApplied = false)
    {
        var diagnosticException = JintException.TryGetClrException(clrException, out var originatingException)
            ? originatingException
            : clrException;
        errorConstructor ??= clrException is JavaScriptException { Error: ObjectInstance javaScriptError }
            ? GetErrorConstructor(engine, javaScriptError)
            : engine.Realm.Intrinsics.Error;
        var error = errorConstructor.Construct(message);

        // The error value survives the interpreter's throw-completion reconstruction (the .NET exception
        // instance does not), so the only place the originating exception can be recorded and still reach
        // the host is the error object itself. Attached before the decorator runs, so a decorator can read it.
        if (error is ErrorInstance errorInstance)
        {
            errorInstance.SetClrException(diagnosticException);
            if (moduleErrorPolicyApplied)
            {
                errorInstance.SetModuleErrorPolicyToken(engine.Modules.GetModuleErrorPolicyToken());
            }
        }

        engine.Options.Interop.ClrExceptionErrorDecorator?.Invoke(engine, error, diagnosticException);
        return error;
    }

    private static ErrorConstructor GetErrorConstructor(Engine engine, ObjectInstance error)
    {
        var intrinsics = engine.Realm.Intrinsics;
        var prototype = error.Prototype;
        if (ReferenceEquals(prototype, intrinsics.SyntaxError.PrototypeObject))
        {
            return intrinsics.SyntaxError;
        }
        if (ReferenceEquals(prototype, intrinsics.TypeError.PrototypeObject))
        {
            return intrinsics.TypeError;
        }
        if (ReferenceEquals(prototype, intrinsics.RangeError.PrototypeObject))
        {
            return intrinsics.RangeError;
        }
        if (ReferenceEquals(prototype, intrinsics.ReferenceError.PrototypeObject))
        {
            return intrinsics.ReferenceError;
        }
        if (ReferenceEquals(prototype, intrinsics.EvalError.PrototypeObject))
        {
            return intrinsics.EvalError;
        }
        if (ReferenceEquals(prototype, intrinsics.UriError.PrototypeObject))
        {
            return intrinsics.UriError;
        }

        return intrinsics.Error;
    }

    [DoesNotReturn]
    public static void RecursionDepthOverflowException(JintCallStack currentStack, bool preserveTop = false)
    {
        var current = currentStack.Pop();
        var exception = new RecursionDepthOverflowException(currentStack, current.ToString());
        if (preserveTop)
        {
            currentStack.RestoreTop(in current);
        }
        throw exception;
    }

    [DoesNotReturn]
    public static void ArgumentNullException(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    public static void MeaningfulException(Engine engine, TargetInvocationException exception)
    {
        var meaningfulException = exception.InnerException ?? exception;
        if (MustPropagateHostException(meaningfulException))
        {
            ExceptionDispatchInfo.Capture(meaningfulException).Throw();
        }

        if (engine.Options.Interop.ExceptionHandler(meaningfulException))
        {
            FromClrException(engine, meaningfulException);
        }

        ExceptionDispatchInfo.Capture(meaningfulException).Throw();
#pragma warning disable CS8763
    }
#pragma warning restore CS8763

    [DoesNotReturn]
    internal static void Error(Engine engine, string message)
    {
        throw new JavaScriptException(engine.Realm.Intrinsics.Error, message);
    }

    [DoesNotReturn]
    public static void PlatformNotSupportedException(string message)
    {
        throw new PlatformNotSupportedException(message);
    }

    [DoesNotReturn]
    public static void MemoryLimitExceededException(string message)
    {
        throw new MemoryLimitExceededException(message);
    }

    [DoesNotReturn]
    internal static void MemoryLimitExceededException(string message, Exception? innerException)
    {
        throw new MemoryLimitExceededException(message, innerException);
    }

    [DoesNotReturn]
    public static void ExecutionCanceledException()
    {
        throw new ExecutionCanceledException();
    }

    [DoesNotReturn]
    public static void ModuleResolutionException(string message, string specifier, string? parent, string? filePath = null)
    {
        throw new ModuleResolutionException(message, specifier, parent, filePath);
    }

    [DoesNotReturn]
    public static void InvalidPreparedScriptArgumentException(string paramName)
    {
        throw new ArgumentException($"Instances of {typeof(Prepared<Script>)} returned by {nameof(Engine.PrepareScript)} are allowed only.", paramName);
    }

    [DoesNotReturn]
    public static void InvalidPreparedModuleArgumentException(string paramName)
    {
        // Qualified because this file also imports System.Reflection, whose Module collides with
        // Acornima's. That pair is not Jint's to rename, and it is the one an embedder still meets.
        throw new ArgumentException($"Instances of {typeof(Prepared<Acornima.Ast.Module>)} returned by {nameof(Engine.PrepareModule)} are allowed only.", paramName);
    }
}
