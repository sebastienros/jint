using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%-object
/// The %IteratorHelperPrototype% object is the prototype of iterator helper objects.
/// </summary>
internal sealed class IteratorHelperPrototype : Prototype
{
    private readonly IteratorPrototype _iteratorPrototype;

    internal IteratorHelperPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm)
    {
        _iteratorPrototype = iteratorPrototype;
        _prototype = iteratorPrototype;
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
    /// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.next
    /// </summary>
    private JsValue Next(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Return ? GeneratorResume(this value, undefined, "Iterator Helper").
        if (thisObject is not IteratorHelper helper)
        {
            Throw.TypeError(_realm, "Method Iterator Helper.prototype.next called on incompatible receiver");
            return Undefined;
        }

        return helper.Next();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.return
    /// </summary>
    private JsValue Return(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Let O be this value.
        // 2. Perform ? RequireInternalSlot(O, [[UnderlyingIterator]]).
        if (thisObject is not IteratorHelper helper)
        {
            Throw.TypeError(_realm, "Method Iterator Helper.prototype.return called on incompatible receiver");
            return Undefined;
        }

        return helper.Return();
    }
}
