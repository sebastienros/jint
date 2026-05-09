using System.Diagnostics.CodeAnalysis;
using Jint.Runtime;

namespace Jint;

/// <summary>
/// Base class for exceptions thrown by Jint.
/// </summary>
public abstract class JintException : Exception
{
    internal JintException(string? message) : base(message)
    {
    }

    internal JintException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// If <paramref name="exception"/> bubbled out of Jint script execution and Jint annotated
    /// it with the JavaScript source location, returns that location. Works for both
    /// <see cref="JavaScriptException"/> (typed location) and bubbled CLR exceptions
    /// (location attached via <see cref="System.Exception.Data"/>).
    /// </summary>
    public static bool TryGetJavaScriptLocation(Exception? exception, out SourceLocation location)
    {
        if (exception is JavaScriptException jse && jse.Location != default)
        {
            location = jse.Location;
            return true;
        }

        if (exception?.Data is { } data && data.Contains(JintExceptionDataKeys.Location))
        {
            switch (data[JintExceptionDataKeys.Location])
            {
                case JintExceptionLocation wrapper:
                    location = wrapper.ToSourceLocation();
                    return true;
                case SourceLocation sl:
                    location = sl;
                    return true;
            }
        }

        location = default;
        return false;
    }

    /// <summary>
    /// If <paramref name="exception"/> bubbled out of Jint script execution and Jint annotated
    /// it with the JavaScript call-stack string, returns that string. Works for both
    /// <see cref="JavaScriptException"/> (via <see cref="JavaScriptException.JavaScriptStackTrace"/>)
    /// and bubbled CLR exceptions (string attached via <see cref="System.Exception.Data"/>).
    /// </summary>
    public static bool TryGetJavaScriptCallStack(Exception? exception, [NotNullWhen(true)] out string? callStack)
    {
        if (exception is JavaScriptException jse && jse.JavaScriptStackTrace is { } trace)
        {
            callStack = trace;
            return true;
        }

        if (exception?.Data is { } data
            && data.Contains(JintExceptionDataKeys.CallStack)
            && data[JintExceptionDataKeys.CallStack] is string s)
        {
            callStack = s;
            return true;
        }

        callStack = null;
        return false;
    }
}
