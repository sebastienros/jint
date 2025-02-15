using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Error;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
/// </summary>
internal sealed class ErrorPrototype : ErrorInstance
{
    private readonly JsString _name;
    private readonly Realm _realm;
    private readonly ErrorConstructor _constructor;

    internal ErrorPrototype(
        Engine engine,
        Realm realm,
        ErrorConstructor constructor,
        ObjectInstance prototype,
        JsString name)
        : base(engine, ObjectClass.Object)
    {
        _realm = realm;
        _name = name;
        _constructor = constructor;
        _prototype = prototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["message"] = new PropertyDescriptor("", PropertyFlag.Configurable | PropertyFlag.Writable),
            ["name"] = new PropertyDescriptor(_name, PropertyFlag.Configurable | PropertyFlag.Writable),
            ["toString"] = new PropertyDescriptor(new ClrFunction(Engine, "toString", ToString, 0, PropertyFlag.Configurable), PropertyFlag.Configurable | PropertyFlag.Writable)
        };
        SetProperties(properties);
    }

    public JsValue ToString(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject.TryCast<ObjectInstance>();
        if (o is null)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var nameProp = o.Get("name", this);
        var name = nameProp.IsUndefined() ? "Error" : TypeConverter.ToString(nameProp);

        var msgProp = o.Get("message", this);
        string msg = msgProp.IsUndefined() ? "" : TypeConverter.ToString(msgProp);

        if (name == "")
        {
            return msg;
        }
        if (msg == "")
        {
            return name;
        }
        return name + ": " + msg;
    }
}
