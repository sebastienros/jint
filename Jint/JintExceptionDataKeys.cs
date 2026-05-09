namespace Jint;

/// <summary>
/// Well-known string keys used in <see cref="System.Exception.Data"/> when Jint annotates
/// CLR exceptions that bubble out of script execution with JavaScript source information.
/// Use the helpers on <see cref="JintException"/> (e.g. <see cref="JintException.TryGetJavaScriptLocation"/>)
/// to read the values rather than indexing <see cref="System.Exception.Data"/> directly.
/// </summary>
public static class JintExceptionDataKeys
{
    /// <summary>
    /// Key under <see cref="System.Exception.Data"/> whose value is a
    /// <see cref="JintExceptionLocation"/> describing the JavaScript syntax element that triggered
    /// the CLR exception. Prefer the helper
    /// <see cref="JintException.TryGetJavaScriptLocation(System.Exception?, out Acornima.SourceLocation)"/>
    /// over reading this value directly.
    /// </summary>
    public const string Location = "JintLocation";

    /// <summary>
    /// Key under <see cref="System.Exception.Data"/> whose value is a <see cref="string"/>
    /// containing the JavaScript call-stack at the time the CLR exception was thrown,
    /// formatted the same way as <see cref="Jint.Runtime.JavaScriptException.JavaScriptStackTrace"/>.
    /// </summary>
    public const string CallStack = "JintCallStack";
}
