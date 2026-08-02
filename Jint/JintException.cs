using System.Diagnostics.CodeAnalysis;
using Jint.Native.Error;
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

    /// <summary>
    /// If <paramref name="exception"/> was thrown because a CLR method or constructor call could not be
    /// resolved (e.g. wrong number or types of arguments), returns the originating CLR <see cref="Type"/>.
    /// This works regardless of <see cref="Jint.Options.InteropOptions.ExposeDetailedResolutionErrors"/>;
    /// the type is host-only and never exposed to the running script.
    /// </summary>
    public static bool TryGetClrType(Exception? exception, [NotNullWhen(true)] out Type? clrType)
    {
        if (exception is JavaScriptException { Error: ErrorInstance { ClrResolutionType: { } type } })
        {
            clrType = type;
            return true;
        }

        clrType = null;
        return false;
    }

    /// <summary>
    /// If <paramref name="exception"/> was thrown because a CLR method call could not be resolved,
    /// returns the name of the member that was invoked. Constructors have no member name and return false.
    /// </summary>
    public static bool TryGetClrMemberName(Exception? exception, [NotNullWhen(true)] out string? memberName)
    {
        if (exception is JavaScriptException { Error: ErrorInstance { ClrResolutionMemberName: { } name } })
        {
            memberName = name;
            return true;
        }

        memberName = null;
        return false;
    }

    /// <summary>
    /// How far <see cref="TryGetClrException"/> follows <c>cause</c>. The chain is script-controlled, so it can
    /// be arbitrarily deep or cyclic; the bound is what makes both cases terminate.
    /// </summary>
    private const int MaxCauseDepth = 8;

    /// <summary>
    /// If <paramref name="exception"/> stands in for a CLR exception that was thrown out of host code — a
    /// delegate, a reflected member, a proxy trap — and turned into a JavaScript error because
    /// <see cref="Jint.Options.InteropOptions.ExceptionHandler"/> asked for it to be catchable by the script,
    /// returns that exception, with its message, .NET stack trace and inner chain intact.
    /// <para>
    /// The exception is host-only: it is CLR state on the error object rather than a JavaScript property, so a
    /// running script can neither read it nor strip it. It survives a script catching the error and rethrowing
    /// the same value, and a script rewrapping with <c>throw new Error(msg, { cause: err })</c>, which this
    /// method follows. A script that throws an unrelated error instead keeps nothing, which is correct — it
    /// discarded the original.
    /// </para>
    /// <para>
    /// Note that the error object holds the exception, and so everything the exception's object graph reaches,
    /// for as long as the script keeps the error reachable.
    /// </para>
    /// </summary>
    public static bool TryGetClrException(Exception? exception, [NotNullWhen(true)] out Exception? clrException)
    {
        var value = exception switch
        {
            JavaScriptException javaScriptException => javaScriptException.Error,
            PromiseRejectedException promiseRejectedException => promiseRejectedException.RejectedValue,
            _ => null
        };

        for (var depth = 0; depth < MaxCauseDepth && value is ErrorInstance error; depth++)
        {
            if (error.ClrException is { } found)
            {
                clrException = found;
                return true;
            }

            // Own data property only, never Get: following the chain must not run a script getter while the
            // host is unwinding. An accessor-valued cause simply ends the walk.
            var cause = error.GetOwnProperty(CommonProperties.Cause);
            value = cause.IsDataDescriptor() ? cause.Value : null;
        }

        clrException = null;
        return false;
    }
}
