#nullable enable
using Esprima;
using Jint.Native;

namespace Jint.Runtime;

public class JintScriptExecutionException : JintException
{
    private readonly JavaScriptException _javaScriptException;

    public string? JavaScriptStackTrace => _javaScriptException.StackTrace;
    public Location Location => _javaScriptException.Location;
    public JsValue Error => _javaScriptException.Error;

    public JintScriptExecutionException(JavaScriptException innerException)
        : base(innerException.Message, innerException)
    {
        _javaScriptException = innerException;
    }

    public string GetJavaScriptErrorString() => _javaScriptException.ToString();
}
