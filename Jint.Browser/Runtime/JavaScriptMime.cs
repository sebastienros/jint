namespace Jint.Browser.Runtime;

/// <summary>https://mimesniff.spec.whatwg.org/#javascript-mime-type.</summary>
internal static class JavaScriptMime
{
    internal static bool IsJavaScript(string essence)
        => essence.ToLowerInvariant() is "application/ecmascript" or "application/javascript"
            or "application/x-ecmascript" or "application/x-javascript"
            or "text/ecmascript" or "text/javascript" or "text/javascript1.0" or "text/javascript1.1"
            or "text/javascript1.2" or "text/javascript1.3" or "text/javascript1.4" or "text/javascript1.5"
            or "text/jscript" or "text/livescript" or "text/x-ecmascript" or "text/x-javascript";
}
