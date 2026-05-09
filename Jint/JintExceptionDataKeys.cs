namespace Jint;

/// <summary>
/// Well-known string keys used in <see cref="System.Exception.Data"/> when Jint annotates
/// CLR exceptions that bubble out of script execution with JavaScript source information.
/// Use the helpers on <see cref="JintException"/> (e.g. <see cref="JintException.TryGetJavaScriptLocation"/>)
/// to read the values rather than indexing <see cref="System.Exception.Data"/> directly.
/// </summary>
internal static class JintExceptionDataKeys
{
    internal const string Location = "JintLocation";

    internal const string CallStack = "JintCallStack";
}
