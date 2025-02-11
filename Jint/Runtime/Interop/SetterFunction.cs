using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop;

/// <summary>
/// Represents a FunctionInstance wrapping a CLR setter.
/// </summary>
internal sealed class SetterFunction : Function
{
    private static readonly JsString _name = new JsString("set");
    private readonly Action<JsValue, JsValue> _setter;

    public SetterFunction(Engine engine, Action<JsValue, JsValue> setter)
        : base(engine, engine.Realm, _name)
    {
        _setter = setter;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        _setter(thisObject, arguments[0]);

        return Null;
    }
}
