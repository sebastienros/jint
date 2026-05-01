using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%asynciteratorhelperprototype%-object
/// The %AsyncIteratorHelperPrototype% object is the prototype of async iterator helper objects.
/// </summary>
[JsObject]
internal sealed partial class AsyncIteratorHelperPrototype : Prototype
{
    internal AsyncIteratorHelperPrototype(
        Engine engine,
        Realm realm,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// %AsyncIteratorHelperPrototype%.next()
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue Next(JsValue thisObject)
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
    [JsFunction(Length = 0)]
    private JsValue Return(JsValue thisObject)
    {
        if (thisObject is AsyncIteratorHelper helper)
        {
            return helper.Return();
        }

        Throw.TypeError(_realm, "Method AsyncIterator Helper.prototype.return called on incompatible receiver");
        return Undefined;
    }
}
