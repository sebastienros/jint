namespace Jint.Runtime;

internal static class ExceptionDataHelper
{
    /// <summary>
    /// Whether <paramref name="exception"/> already carries the JavaScript location, or can never carry
    /// it at all. Both answers mean the same thing to a caller: there is nothing left for it to attach.
    /// </summary>
    /// <remarks>
    /// This is read from an exception <em>filter</em>, so it runs in the first pass with the whole
    /// throwing stack still live: it must not recurse, must not allocate beyond the lazy dictionary
    /// <see cref="System.Exception.Data"/> creates on first read, and must never throw. Only the
    /// innermost interpreter frame is answered <c>false</c>; every frame above it then declines to catch
    /// the exception, which is what lets a CLR exception from host code unwind a deep interpreter stack
    /// instead of consuming a nested exception dispatch per level.
    /// </remarks>
    internal static bool HasJavaScriptLocation(Exception exception)
    {
        try
        {
            var data = exception.Data;
            return data is null || data.IsReadOnly || data.Contains(JintExceptionDataKeys.Location);
        }
        catch
        {
            // Same defensive contract as TryAttachJavaScriptLocation: a Data implementation that throws
            // is one the location cannot be attached to, which is also "nothing left to do here".
            return true;
        }
    }

    /// <summary>
    /// Best-effort attach JavaScript location and call-stack info to a CLR exception's
    /// <see cref="System.Exception.Data"/> dictionary using the well-known keys defined in
    /// <see cref="JintExceptionDataKeys"/>. Idempotent: the innermost JavaScript site wins
    /// when an exception bubbles through multiple catch sites. Never throws.
    /// </summary>
    /// <remarks>
    /// On .NET Framework the underlying <c>ListDictionaryInternal</c> rejects values that are
    /// not <see cref="System.SerializableAttribute"/>-marked, so we store the location as a
    /// <see cref="JintExceptionLocation"/> wrapper rather than the raw <see cref="SourceLocation"/>.
    /// The call stack string is always serializable.
    /// </remarks>
    internal static void TryAttachJavaScriptLocation(
        Exception exception,
        Engine engine,
        in SourceLocation location)
    {
        try
        {
            var data = exception.Data;
            if (data is null || data.IsReadOnly)
            {
                return;
            }

            if (!data.Contains(JintExceptionDataKeys.Location))
            {
                data[JintExceptionDataKeys.Location] = JintExceptionLocation.FromSourceLocation(in location);
            }

            if (!data.Contains(JintExceptionDataKeys.CallStack))
            {
                data[JintExceptionDataKeys.CallStack] = engine.CallStack.BuildCallStackString(engine, location);
            }
        }
        catch
        {
            // Defensive: never let exception decoration replace the original exception.
        }
    }
}
