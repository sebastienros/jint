using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime.CallStack;
using Jint.Runtime.Modules;

namespace Jint.Runtime;

/// <summary>
/// Wraps known runtime type error information.
/// </summary>
internal sealed record ErrorDispatchInfo(ErrorConstructor ErrorConstructor, string? Message = null);

internal static class Throw
{
    [DoesNotReturn]
    public static void SyntaxError(Realm realm, string? message = null)
    {
        throw CreateSyntaxError(realm, message);
    }

    [DoesNotReturn]
    public static void SyntaxError(Realm realm, string message, in SourceLocation location)
    {
        throw CreateSyntaxError(realm, message).SetJavaScriptLocation(location);
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
        throw new JavaScriptException(realm.Intrinsics.ReferenceError, message).SetJavaScriptLocation(location);
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
        throw new JavaScriptException(realm.Intrinsics.TypeError, message).SetJavaScriptLocation(location);
    }

    [DoesNotReturn]
    public static void RangeError(Realm realm, string? message = null)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.RangeError, message).SetJavaScriptLocation(location);
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
    public static void TimeoutException()
    {
        throw new TimeoutException();
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
        throw new JavaScriptException(value).SetJavaScriptCallstack(engine, location);
    }

    [DoesNotReturn]
    public static void JavaScriptException(ErrorConstructor errorConstructor, string message)
    {
        throw new JavaScriptException(errorConstructor, message);
    }

    [DoesNotReturn]
    public static void RecursionDepthOverflowException(JintCallStack currentStack)
    {
        var currentExpressionReference = currentStack.Pop().ToString();
        throw new RecursionDepthOverflowException(currentStack, currentExpressionReference);
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

        if (engine.Options.Interop.ExceptionHandler(meaningfulException))
        {
            Error(engine, meaningfulException.Message);
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
        throw new ArgumentException($"Instances of {typeof(Prepared<AstModule>)} returned by {nameof(Engine.PrepareModule)} are allowed only.", paramName);
    }
}
