using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%asynciteratorhelperprototype%-object
/// The %AsyncIteratorHelperPrototype% object is the prototype of async iterator helper objects.
/// </summary>
internal sealed class AsyncIteratorHelperPrototype : Prototype
{
    internal AsyncIteratorHelperPrototype(
        Engine engine,
        Realm realm,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new PropertyDescriptor(new ClrFunction(_engine, "next", Next, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
            [KnownKeys.Return] = new PropertyDescriptor(new ClrFunction(_engine, "return", Return, 0, PropertyFlag.Configurable), PropertyFlag.Writable | PropertyFlag.Configurable),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// %AsyncIteratorHelperPrototype%.next()
    /// </summary>
    private JsValue Next(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is AsyncIteratorHelper helper)
        {
            return helper.Next();
        }

        Throw.TypeError(_realm, "Method AsyncIterator Helper.prototype.next called on incompatible receiver");
        return Undefined;
    }

    /// <summary>
    /// %AsyncIteratorHelperPrototype%.return()
    /// </summary>
    private JsValue Return(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is AsyncIteratorHelper helper)
        {
            return helper.Return();
        }

        Throw.TypeError(_realm, "Method AsyncIterator Helper.prototype.return called on incompatible receiver");
        return Undefined;
    }
}
