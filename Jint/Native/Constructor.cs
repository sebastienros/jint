using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public abstract class Constructor : FunctionInstance, IConstructor
{
    protected Constructor(Engine engine, Realm realm, JsString name) : base(engine, realm, name)
    {
    }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        ExceptionHelper.ThrowTypeError(_realm, $"Constructor {_nameDescriptor?.Value} requires 'new'");
        return null;
    }

    public virtual ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        ExceptionHelper.ThrowTypeError(_realm, $"Abstract class {_nameDescriptor?.Value} not directly constructable");
        return null;
    }
}
