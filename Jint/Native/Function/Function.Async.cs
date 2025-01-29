using System.Diagnostics;

namespace Jint.Native.Function;

[DebuggerDisplay("{ToString(),nq}")]
#pragma warning disable MA0049
public abstract partial class Function
{
    protected internal virtual Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));

    Task<JsValue> ICallable.CallAsync(JsValue thisObject, JsValue[] arguments) => CallAsync(thisObject, arguments);

}
