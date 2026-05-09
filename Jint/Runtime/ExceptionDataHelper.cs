namespace Jint.Runtime;

internal static class ExceptionDataHelper
{
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
                data[JintExceptionDataKeys.Location] = JintExceptionLocation.FromSourceLocation(location);
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
