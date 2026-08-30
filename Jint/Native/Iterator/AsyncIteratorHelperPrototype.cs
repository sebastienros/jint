using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/proposal-async-iterator-helpers/#sec-%asynciteratorhelperprototype%-object
/// The %AsyncIteratorHelperPrototype% object is the prototype of async iterator helper objects.
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class AsyncIteratorHelperPrototype : Prototype
{
    /// <summary>
    /// https://tc39.es/proposal-async-iterator-helpers/#sec-%asynciteratorhelperprototype%-@@tostringtag
    /// A data property, so it shadows the accessor pair %AsyncIterator.prototype% carries.
    /// </summary>
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString AsyncIteratorHelperToStringTag = new("Async Iterator Helper");

    internal AsyncIteratorHelperPrototype(
        Engine engine,
        Realm realm,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, realm)
    {
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// %AsyncIteratorHelperPrototype%.next()
    /// </summary>
    [JsFunction]
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
    [JsFunction]
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
