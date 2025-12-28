using Jint.Native;

namespace Jint.Runtime;

/// <summary>
/// Internal exception used to implement yield suspension.
/// When a yield expression is evaluated, this exception is thrown to immediately
/// interrupt expression evaluation and propagate control back up the call stack.
/// This is caught at the statement level to handle generator suspension properly.
/// </summary>
internal sealed class YieldSuspendException : Exception
{
    public JsValue YieldedValue { get; }

    public YieldSuspendException(JsValue yieldedValue) : base()
    {
        YieldedValue = yieldedValue;
    }
}
