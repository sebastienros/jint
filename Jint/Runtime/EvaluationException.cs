#nullable enable
using Esprima;
using Jint.Native;

namespace Jint.Runtime;

public class EvaluationException : JavaScriptException
{
    private readonly JavaScriptException _javaScriptException;

    public string? JavaScriptStackTrace => _javaScriptException.StackTrace;
    public override Location Location => _javaScriptException.Location;
    public override JsValue Error => _javaScriptException.Error;

    internal EvaluationException(JavaScriptException innerException)
        : base(innerException.Message, innerException)
    {
        _javaScriptException = innerException;
    }

    public string GetJavaScriptErrorString() => _javaScriptException.ToString();
}
