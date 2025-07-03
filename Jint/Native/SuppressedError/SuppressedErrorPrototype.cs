using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.SuppressedError;

internal sealed class SuppressedErrorPrototype : Prototype
{
    private readonly SuppressedErrorConstructor _constructor;

    internal SuppressedErrorPrototype(
        Engine engine,
        Realm realm,
        SuppressedErrorConstructor constructor,
        ObjectInstance prototype)
        : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = prototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["message"] = new PropertyDescriptor(JsString.Empty, PropertyFlag.Configurable | PropertyFlag.Writable),
            ["name"] = new PropertyDescriptor("SuppressedError", PropertyFlag.Configurable | PropertyFlag.Writable),
        };
        SetProperties(properties);
    }
}
