using Jint.Native;

namespace Jint.Runtime;

/// <summary>
/// Internal exception used to implement generator return() behavior.
/// When return() is called on a suspended generator, this exception is thrown
/// to trigger finally blocks and iterator close operations before completing.
/// </summary>
internal sealed class GeneratorReturnException : Exception
{
    public JsValue ReturnValue { get; }

    public GeneratorReturnException(JsValue returnValue) : base()
    {
        ReturnValue = returnValue;
    }
}
