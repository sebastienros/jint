using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%-object
/// The %IteratorHelperPrototype% object is the prototype of iterator helper objects.
/// </summary>
[JsObject]
internal sealed partial class IteratorHelperPrototype : Prototype
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

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.next
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue Next(JsValue thisObject)
    {
        // 1. Return ? GeneratorResume(this value, undefined, "Iterator Helper").
        if (thisObject is IteratorHelper helper)
        {
            return helper.Next();
        }

        if (thisObject is ConcatIterator concatIterator)
        {
            return concatIterator.Next();
        }

        if (thisObject is ZipIterator zipIterator)
        {
            return zipIterator.Next();
        }

        Throw.TypeError(_realm, "Method Iterator Helper.prototype.next called on incompatible receiver");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.return
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue Return(JsValue thisObject)
    {
        // 1. Let O be this value.
        // 2. Perform ? RequireInternalSlot(O, [[UnderlyingIterator]]).
        if (thisObject is IteratorHelper helper)
        {
            return helper.Return();
        }

        if (thisObject is ConcatIterator concatIterator)
        {
            return concatIterator.Return();
        }

        if (thisObject is ZipIterator zipIterator)
        {
            return zipIterator.Return();
        }

        Throw.TypeError(_realm, "Method Iterator Helper.prototype.return called on incompatible receiver");
        return Undefined;
    }
}
