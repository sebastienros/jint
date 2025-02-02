using System.Diagnostics;

namespace Jint.Native.Function;

[DebuggerDisplay("{ToString(),nq}")]
#pragma warning disable MA0049
public abstract partial class Function
{
    protected internal virtual ValueTask<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => new ValueTask<JsValue>(Call(thisObject, arguments));

    ValueTask<JsValue> ICallable.CallAsync(JsValue thisObject, JsValue[] arguments) => CallAsync(thisObject, arguments);

}
