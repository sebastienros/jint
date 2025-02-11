using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public abstract class Constructor : Function.Function, IConstructor
{
    protected Constructor(Engine engine, string name) : this(engine, engine.Realm, new JsString(name))
    {
    }

    internal Constructor(Engine engine, Realm realm, JsString name) : base(engine, realm, name)
    {
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        ExceptionHelper.ThrowTypeError(_realm, $"Constructor {_nameDescriptor?.Value} requires 'new'");
        return null;
    }

    public abstract ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget);
}
