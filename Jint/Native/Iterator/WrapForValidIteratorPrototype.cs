using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%wrapforvaliditeratorprototype%-object
/// The %WrapForValidIteratorPrototype% object is the prototype of wrapped iterator objects from Iterator.from.
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class WrapForValidIteratorPrototype : Prototype
{
    internal WrapForValidIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm)
    {
        _prototype = iteratorPrototype;
    }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%wrapforvaliditeratorprototype%.next
    /// </summary>
    [JsFunction]
    private JsValue Next(JsValue thisObject)
    {
        // 1. Let O be this value.
        // 2. Perform ? RequireInternalSlot(O, [[Iterated]]).
        if (thisObject is not WrapForValidIterator wrapper)
        {
            Throw.TypeError(_realm, "Method WrapForValidIterator.prototype.next called on incompatible receiver");
            return Undefined;
        }

        // 3. Let iteratorRecord be O.[[Iterated]].
        var iterated = wrapper.Iterated;

        // 4. Return ? Call(iteratorRecord.[[NextMethod]], iteratorRecord.[[Iterator]]).
        // The step hands the wrapped iterator's answer straight back: it neither requires the
        // result to be an Object nor reads `done` off it. Both of those belong to IteratorNext
        // (https://tc39.es/ecma262/#sec-iteratornext step 3), performed by whoever consumes this
        // wrapper as an iterator -- calling .next() on it directly is not such a consumption.
        var nextMethod = iterated.NextMethod;
        if (nextMethod is null)
        {
            Throw.TypeError(_realm, "Iterator does not have a next method");
        }

        return nextMethod.Call(iterated.Instance);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%wrapforvaliditeratorprototype%.return
    /// </summary>
    [JsFunction]
    private JsValue Return(JsValue thisObject)
    {
        // 1. Let O be this value.
        // 2. Perform ? RequireInternalSlot(O, [[Iterated]]).
        if (thisObject is not WrapForValidIterator wrapper)
        {
            Throw.TypeError(_realm, "Method WrapForValidIterator.prototype.return called on incompatible receiver");
            return Undefined;
        }

        // 3. Let iterator be O.[[Iterated]].[[Iterator]].
        var iterator = wrapper.Iterated.Instance;

        // 4. Let returnMethod be ? GetMethod(iterator, "return").
        var returnMethod = iterator.GetMethod(CommonProperties.Return);

        // 5. If returnMethod is undefined, return CreateIteratorResultObject(undefined, true).
        if (returnMethod is null)
        {
            return IteratorResult.CreateValueIteratorPosition(_engine, Undefined, JsBoolean.True);
        }

        // 6. Return ? Call(returnMethod, iterator).
        return returnMethod.Call(iterator);
    }
}

/// <summary>
/// Wrapper instance for Iterator.from when the input is not already an Iterator instance.
/// </summary>
internal sealed class WrapForValidIterator : ObjectInstance
{
    internal readonly IteratorInstance.ObjectIterator Iterated;

    public WrapForValidIterator(Engine engine, Realm realm, IteratorInstance.ObjectIterator iterated) : base(engine)
    {
        Iterated = iterated;
        // The realm is the one Iterator.from itself belongs to, not whichever realm is running:
        // step 5 creates the wrapper from the callee's %WrapForValidIteratorPrototype%.
        _prototype = realm.Intrinsics.WrapForValidIteratorPrototype;
    }
}
