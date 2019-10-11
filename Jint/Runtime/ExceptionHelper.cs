using System;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.CallStack;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    internal static class ExceptionHelper
    {
        public static T ThrowSyntaxError<T>(Engine engine, string message = null)
        {
            ThrowSyntaxError(engine, message);
            return default;
        }

        public static void ThrowSyntaxError(Engine engine, string message = null)
        {
            throw new JavaScriptException(engine.SyntaxError, message);
        }

        public static T ThrowArgumentException<T>(string message = null)
        {
            ThrowArgumentException(message);
            return default;
        }

        public static void ThrowArgumentException(string message = null)
        {
            ThrowArgumentException(message, null);
        }

        public static void ThrowArgumentException(string message, string paramName)
        {
            throw new ArgumentException(message, paramName);
        }

        public static void ThrowReferenceError(Engine engine, Reference reference)
        {
            ThrowReferenceError(engine, reference?.GetReferencedName().Name);
        }

        public static void ThrowReferenceError(Engine engine, string name)
        {
            var message = name != null ? name + " is not defined" : null;
            throw new JavaScriptException(engine.ReferenceError, message);
        }

        public static T ThrowTypeErrorNoEngine<T>(string message = null, Exception exception = null)
        {
            throw new TypeErrorException(message);
        }

        public static T ThrowReferenceError<T>(Engine engine, string message = null)
        {
            throw new JavaScriptException(engine.ReferenceError, message);
        }

        public static T ThrowTypeError<T>(Engine engine, string message = null, Exception exception = null)
        {
            ThrowTypeError(engine, message, exception);
            return default;
        }

        public static void ThrowTypeError(Engine engine, string message = null, Exception exception = null)
        {
            throw new JavaScriptException(engine.TypeError, message, exception);
        }

        public static T ThrowRangeError<T>(Engine engine, string message = null)
        {
            throw new JavaScriptException(engine.RangeError, message);
        }

        public static T ThrowRangeErrorNoEngine<T>(string message)
        {
            throw new RangeErrorException(message);
        }

        public static void ThrowRangeError(Engine engine, string message = null)
        {
            throw new JavaScriptException(engine.RangeError, message);
        }

        public static void ThrowUriError(Engine engine)
        {
            throw new JavaScriptException(engine.UriError);
        }

        public static void ThrowNotImplementedException()
        {
            throw new NotImplementedException();
        }

        public static T ThrowNotImplementedException<T>()
        {
            throw new NotImplementedException();
        }

        public static T ThrowArgumentOutOfRangeException<T>()
        {
            throw new ArgumentOutOfRangeException();
        }

        public static T ThrowArgumentOutOfRangeException<T>(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        public static void ThrowArgumentOutOfRangeException(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        public static void ThrowTimeoutException()
        {
            throw new TimeoutException();
        }

        public static void ThrowStatementsCountOverflowException()
        {
            throw new StatementsCountOverflowException();
        }

        public static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }

        public static T ThrowNotSupportedException<T>(string message = null)
        {
            throw new NotSupportedException(message);
        }

        public static void ThrowNotSupportedException(string message = null)
        {
            throw new NotSupportedException(message);
        }

        public static void ThrowInvalidOperationException(string message = null)
        {
            throw new InvalidOperationException(message);
        }

        public static void ThrowJavaScriptException(Engine engine, JsValue value, Completion result)
        {
            throw new JavaScriptException(value).SetCallstack(engine, result.Location);
        }

        public static void ThrowRecursionDepthOverflowException(JintCallStack currentStack, string currentExpressionReference)
        {
            throw new RecursionDepthOverflowException(currentStack, currentExpressionReference);
        }

        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        public static void ThrowMeaningfulException(Engine engine, TargetInvocationException exception)
        {
            var meaningfulException = exception.InnerException ?? exception;

            var handler = engine.Options._ClrExceptionsHandler;
            if (handler != null && handler(meaningfulException))
                ThrowError(engine, meaningfulException.Message);

            throw meaningfulException;
        }

        public static void ThrowError(Engine engine, string message)
        {
            throw new JavaScriptException(engine.Error, message);
        }

        public static void ThrowPlatformNotSupportedException(string message)
        {
            throw new PlatformNotSupportedException(message);
        }

        public static void ThrowMemoryLimitExceededException(string message)
        {
            throw new MemoryLimitExceededException(message);
        }
    }
}