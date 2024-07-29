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

internal static class ExceptionHelper
{
    [DoesNotReturn]
    public static void ThrowSyntaxError(Realm realm, string? message = null)
    {
        throw CreateSyntaxError(realm, message);
    }

    [DoesNotReturn]
    public static void ThrowSyntaxError(Realm realm, string message, in SourceLocation location)
    {
        throw CreateSyntaxError(realm, message).SetJavaScriptLocation(location);
    }

    public static JavaScriptException CreateSyntaxError(Realm realm, string? message)
    {
        return new JavaScriptException(realm.Intrinsics.SyntaxError, message);
    }

    [DoesNotReturn]
    public static void ThrowArgumentException(string? message = null)
    {
        ThrowArgumentException(message, paramName: null);
    }

    [DoesNotReturn]
    public static void ThrowArgumentException(string? message, string? paramName)
    {
        throw new ArgumentException(message, paramName);
    }

    [DoesNotReturn]
    public static void ThrowReferenceError(Realm realm, Reference reference)
    {
        ThrowReferenceNameError(realm, reference?.ReferencedName?.ToString());
    }

    [DoesNotReturn]
    public static void ThrowReferenceNameError(Realm realm, string? name)
    {
        var message = name != null ? name + " is not defined" : null;
        ThrowReferenceError(realm, message);
    }

    [DoesNotReturn]
    public static void ThrowReferenceError(Realm realm, string? message)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.ReferenceError, message).SetJavaScriptLocation(location);
    }

    [DoesNotReturn]
    public static void ThrowTypeErrorNoEngine(string? message = null, Node? source = null)
    {
        throw new TypeErrorException(message, source);
    }

    [DoesNotReturn]
    public static void ThrowTypeError(Realm realm, string? message = null)
    {
        var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
        throw new JavaScriptException(realm.Intrinsics.TypeError, message).SetJavaScriptLocation(location);
    }

    [DoesNotReturn]
    public static void ThrowRangeError(Realm realm, string? message = null)
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
    public static void ThrowNotImplementedException(string? message = null)
    {
        throw new NotImplementedException(message);
    }

    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException(string paramName, string message)
    {
        throw new ArgumentOutOfRangeException(paramName, message);
    }

    [DoesNotReturn]
    public static void ThrowTimeoutException()
    {
        throw new TimeoutException();
    }

    [DoesNotReturn]
    public static void ThrowStatementsCountOverflowException()
    {
        throw new StatementsCountOverflowException();
    }

    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException()
    {
#pragma warning disable MA0015
        throw new ArgumentOutOfRangeException();
#pragma warning restore MA0015
    }

    [DoesNotReturn]
    public static void ThrowNotSupportedException(string? message = null)
    {
        throw new NotSupportedException(message);
    }

    [DoesNotReturn]
    public static void ThrowInvalidOperationException(string? message = null, Exception? exception = null)
    {
        throw new InvalidOperationException(message, exception);
    }

    [DoesNotReturn]
    public static void ThrowPromiseRejectedException(JsValue error)
    {
        throw new PromiseRejectedException(error);
    }

    [DoesNotReturn]
    public static void ThrowJavaScriptException(Engine engine, JsValue value, in Completion result)
    {
        throw new JavaScriptException(value).SetJavaScriptCallstack(engine, result.Location);
    }

    [DoesNotReturn]
    public static void ThrowJavaScriptException(Engine engine, JsValue value, in SourceLocation location)
    {
        throw new JavaScriptException(value).SetJavaScriptCallstack(engine, location);
    }

    [DoesNotReturn]
    public static void ThrowJavaScriptException(ErrorConstructor errorConstructor, string message)
    {
        throw new JavaScriptException(errorConstructor, message);
    }

    [DoesNotReturn]
    public static void ThrowRecursionDepthOverflowException(JintCallStack currentStack)
    {
        var currentExpressionReference = currentStack.Pop().ToString();
        throw new RecursionDepthOverflowException(currentStack, currentExpressionReference);
    }

    [DoesNotReturn]
    public static void ThrowArgumentNullException(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    public static void ThrowMeaningfulException(Engine engine, TargetInvocationException exception)
    {
        var meaningfulException = exception.InnerException ?? exception;

        if (engine.Options.Interop.ExceptionHandler(meaningfulException))
        {
            ThrowError(engine, meaningfulException.Message);
        }

        ExceptionDispatchInfo.Capture(meaningfulException).Throw();
#pragma warning disable CS8763
    }
#pragma warning restore CS8763

    [DoesNotReturn]
    internal static void ThrowError(Engine engine, string message)
    {
        throw new JavaScriptException(engine.Realm.Intrinsics.Error, message);
    }

    [DoesNotReturn]
    public static void ThrowPlatformNotSupportedException(string message)
    {
        throw new PlatformNotSupportedException(message);
    }

    [DoesNotReturn]
    public static void ThrowMemoryLimitExceededException(string message)
    {
        throw new MemoryLimitExceededException(message);
    }

    [DoesNotReturn]
    public static void ThrowExecutionCanceledException()
    {
        throw new ExecutionCanceledException();
    }

    [DoesNotReturn]
    public static void ThrowModuleResolutionException(string message, string specifier, string? parent, string? filePath = null)
    {
        throw new ModuleResolutionException(message, specifier, parent, filePath);
    }

    [DoesNotReturn]
    public static void ThrowInvalidPreparedScriptArgumentException(string paramName)
    {
        throw new ArgumentException($"Instances of {typeof(Prepared<Script>)} returned by {nameof(Engine.PrepareScript)} are allowed only.", paramName);
    }

    [DoesNotReturn]
    public static void ThrowInvalidPreparedModuleArgumentException(string paramName)
    {
        throw new ArgumentException($"Instances of {typeof(Prepared<AstModule>)} returned by {nameof(Engine.PrepareModule)} are allowed only.", paramName);
    }
}