using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime.CallStack;
using Jint.Runtime.Modules;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    internal static class ExceptionHelper
    {
        [DoesNotReturn]
        public static void ThrowSyntaxError(Realm realm, string? message = null)
        {
            throw new JavaScriptException(realm.Intrinsics.SyntaxError, message);
        }

        [DoesNotReturn]
        public static void ThrowSyntaxError(Realm realm, string message, Location location)
        {
            throw new JavaScriptException(realm.Intrinsics.SyntaxError, message).SetJavaScriptLocation(location);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(string? message = null)
        {
            ThrowArgumentException(message, null);
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

        [DoesNotReturn]
        public static void ThrowUriError(Realm realm)
        {
            var location = realm.GlobalObject.Engine.GetLastSyntaxElement()?.Location ?? default;
            throw new JavaScriptException(realm.Intrinsics.UriError).SetJavaScriptLocation(location);
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
            throw new ArgumentOutOfRangeException();
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
        public static void ThrowJavaScriptException(JsValue value)
        {
            throw new JavaScriptException(value);
        }

        [DoesNotReturn]
        public static void ThrowJavaScriptException(Engine engine, JsValue value, in Completion result)
        {
            throw new JavaScriptException(value).SetJavaScriptCallstack(engine, result.Location);
        }

        [DoesNotReturn]
        public static void ThrowJavaScriptException(Engine engine, JsValue value, in Location location)
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
        public static void ThrowModuleResolutionException(string resolverAlgorithmError, string specifier, string? parent)
        {
            throw new ModuleResolutionException(resolverAlgorithmError, specifier, parent);
        }
    }
}
