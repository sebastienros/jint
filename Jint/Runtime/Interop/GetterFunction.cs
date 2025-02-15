using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop;

/// <summary>
/// Represents a FunctionInstance wrapping a CLR getter.
/// </summary>
internal sealed class GetterFunction: Function
{
    private static readonly JsString _name = new JsString("get");
    private readonly Func<JsValue, JsValue> _getter;

    public GetterFunction(Engine engine, Func<JsValue, JsValue> getter)
        : base(engine, engine.Realm, _name, FunctionThisMode.Global)
    {
        _getter = getter;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return _getter(thisObject);
    }
}
