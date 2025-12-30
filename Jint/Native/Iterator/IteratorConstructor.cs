using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

internal sealed class IteratorConstructor : Constructor
{
    private static readonly JsString _functionName = new("Iterator");

    internal IteratorConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new IteratorPrototype(engine, realm, objectPrototype);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal IteratorPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["concat"] = new(new PropertyDescriptor(new ClrFunction(Engine, "concat", Concat, 0, LengthFlags), PropertyFlags)),
            ["from"] = new(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags)),
        };
        SetProperties(properties);
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined() || ReferenceEquals(this, newTarget))
        {
            Throw.TypeError(_realm);
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Iterator.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsObject(engine));
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-sequencing/#sec-iterator.concat
    /// </summary>
    private JsValue Concat(JsValue thisObject, JsValue[] arguments)
    {
        // 1. Let iterables be a new empty List.
        var iterables = new List<ConcatIterator.IterableRecord>();

        // 2. For each element item of items, do
        foreach (var item in arguments)
        {
            // a. If item is not an Object, throw a TypeError exception.
            if (item is not ObjectInstance obj)
            {
                Throw.TypeError(_realm, "Iterator.concat requires object arguments");
                return Undefined;
            }

            // b. Let method be ? GetMethod(item, %Symbol.iterator%).
            var method = obj.GetMethod(GlobalSymbolRegistry.Iterator);

            // c. If method is undefined, throw a TypeError exception.
            if (method is null)
            {
                Throw.TypeError(_realm, "Argument is not iterable");
                return Undefined;
            }

            // d. Append the Record { [[OpenMethod]]: method, [[Iterable]]: item } to iterables.
            iterables.Add(new ConcatIterator.IterableRecord(method, obj));
        }

        // 3-6. Create and return the concat iterator
        return new ConcatIterator(_engine, iterables);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsValue[] arguments)
    {
        // 1. If O is a String, set O to ! ToObject(O).
        var o = arguments.At(0);

        // 2. Let iteratorRecord be ? GetIteratorFlattenable(O, iterate-strings).
        var iteratorRecord = GetIteratorFlattenable(o, StringHandlingType.IterateStrings, out var underlyingIterator);

        // 3. Let hasInstance be ? OrdinaryHasInstance(%Iterator%, iteratorRecord.[[Iterator]]).
        var hasInstance = _engine.Intrinsics.Iterator.OrdinaryHasInstance(underlyingIterator);

        // 4. If hasInstance is true, return iteratorRecord.[[Iterator]].
        if (TypeConverter.ToBoolean(hasInstance))
        {
            return underlyingIterator;
        }

        // 5. Let wrapper be OrdinaryObjectCreate(%WrapForValidIteratorPrototype%, « [[Iterated]] »).
        // 6. Set wrapper.[[Iterated]] to iteratorRecord.
        // 7. Return wrapper.
        var wrapper = new WrapForValidIterator(_engine, iteratorRecord);
        return wrapper;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getiteratorflattenable
    /// </summary>
    private IteratorInstance.ObjectIterator GetIteratorFlattenable(JsValue obj, StringHandlingType stringHandling, out ObjectInstance iterator)
    {
        // 1. If obj is not an Object, then
        if (obj is not ObjectInstance objInstance)
        {
            // a. If stringHandling is reject-strings or obj is not a String, throw a TypeError exception.
            if (stringHandling == StringHandlingType.RejectStrings || !obj.IsString())
            {
                Throw.TypeError(_realm, "Iterator.from requires an object or string");
            }

            // b. Let method be ? GetMethod(obj, @@iterator).
            // Note: Use GetMethod on primitive to preserve receiver for strict mode getters
            var stringMethod = GetMethod(_realm, obj, GlobalSymbolRegistry.Iterator);
            if (stringMethod is null)
            {
                Throw.TypeError(_realm, "Object is not iterable");
                iterator = null!;
                return null!;
            }

            // c. Call method with obj (primitive) as receiver
            var stringIteratorResult = stringMethod.Call(obj);
            if (stringIteratorResult is not ObjectInstance stringIterator)
            {
                Throw.TypeError(_realm, "Iterator result is not an object");
                iterator = null!;
                return null!;
            }

            iterator = stringIterator;
            return new IteratorInstance.ObjectIterator(stringIterator);
        }

        // 2. Let method be ? GetMethod(obj, %Symbol.iterator%).
        var method = objInstance.GetMethod(GlobalSymbolRegistry.Iterator);

        // 3. If method is undefined, then
        if (method is null)
        {
            // a. Let iterator be obj.
            iterator = objInstance;
        }
        else
        {
            // b. Else,
            // i. Let iterator be ? Call(method, obj).
            var result = method.Call(objInstance);
            if (result is not ObjectInstance iteratorObj)
            {
                Throw.TypeError(_realm, "Iterator result is not an object");
                iterator = null!;
                return null!;
            }
            iterator = iteratorObj;
        }

        // 4. If iterator is not an Object, throw a TypeError exception.
        // (Already checked above)

        // 5. Let nextMethod be ? Get(iterator, "next").
        // 6. Let iteratorRecord be the Iterator Record { [[Iterator]]: iterator, [[NextMethod]]: nextMethod, [[Done]]: false }.
        var iteratorRecord = new IteratorInstance.ObjectIterator(iterator);

        // 7. Return iteratorRecord.
        return iteratorRecord;
    }

    private enum StringHandlingType
    {
        IterateStrings,
        RejectStrings
    }
}
