#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray;

/// <summary>
/// https://tc39.es/ecma262/#sec-%typedarray%-intrinsic-object
/// </summary>
[JsObject]
internal sealed partial class IntrinsicTypedArrayConstructor : Constructor
{
    internal IntrinsicTypedArrayConstructor(
        Engine engine,
        Realm realm,
        ObjectInstance functionPrototype,
        ObjectInstance objectPrototype,
        string functionName) : base(engine, realm, new JsString(functionName))
    {
        _prototype = functionPrototype;
        PrototypeObject = new IntrinsicTypedArrayPrototype(engine, objectPrototype, this);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public IntrinsicTypedArrayPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.from
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue From(JsValue thisObject, JsValue source, JsValue mapFunction, JsValue thisArg)
    {
        var c = thisObject;
        if (!c.IsConstructor)
        {
            Throw.TypeError(_realm, "Value is not a constructor");
        }

        var mapping = !mapFunction.IsUndefined();
        if (mapping)
        {
            if (!mapFunction.IsCallable)
            {
                Throw.TypeError(_realm, "mapFn is not a function");
            }
        }

        var usingIterator = GetMethod(_realm, source, Symbol.GlobalSymbolRegistry.Iterator);
        if (usingIterator is not null)
        {
            var values = TypedArrayConstructor.IterableToList(_realm, source, usingIterator);
            var iteratorLen = values.Count;
            var iteratorTarget = TypedArrayCreate(_realm, (IConstructor) c, [iteratorLen]);
            iteratorTarget._viewedArrayBuffer.AssertNotImmutable();
            for (var k = 0; k < iteratorLen; ++k)
            {
                var kValue = values[k];
                var mappedValue = mapping
                    ? ((ICallable) mapFunction).Call(thisArg, kValue, k)
                    : kValue;
                iteratorTarget[k] = mappedValue;
            }

            return iteratorTarget;
        }

        if (source.IsNullOrUndefined())
        {
            Throw.TypeError(_realm, "Cannot convert undefined or null to object");
        }

        var arrayLike = TypeConverter.ToObject(_realm, source);
        var len = arrayLike.GetLength();

        var argumentList = new JsValue[] { JsNumber.Create(len) };
        var targetObj = TypedArrayCreate(_realm, (IConstructor) c, argumentList);
        targetObj._viewedArrayBuffer.AssertNotImmutable();

        var mappingArgs = mapping ? new JsValue[2] : null;
        for (uint k = 0; k < len; ++k)
        {
            var Pk = JsNumber.Create(k);
            var kValue = arrayLike.Get(Pk);
            JsValue mappedValue;
            if (mapping)
            {
                mappingArgs![0] = kValue;
                mappingArgs[1] = Pk;
                mappedValue = ((ICallable) mapFunction).Call(thisArg, mappingArgs);
            }
            else
            {
                mappedValue = kValue;
            }

            targetObj.Set(Pk, mappedValue, true);
        }

        return targetObj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.of
    /// </summary>
    [JsFunction]
    private JsValue Of(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> items)
    {
        var len = items.Length;

        if (!thisObject.IsConstructor)
        {
            Throw.TypeError(_realm, "Value is not a constructor");
        }

        var newObj = TypedArrayCreate(_realm, (IConstructor) thisObject, [len]);
        newObj._viewedArrayBuffer.AssertNotImmutable();

        for (var k = 0; k < len; k++)
        {
            newObj[k] = items[k];
        }

        return newObj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#typedarray-species-create
    /// </summary>
    internal JsTypedArray TypedArraySpeciesCreate(JsTypedArray exemplar, JsCallArguments argumentList)
    {
        var defaultConstructor = exemplar._arrayElementType.GetConstructor(_realm.Intrinsics)!;
        var constructor = SpeciesConstructor(exemplar, defaultConstructor);
        var result = TypedArrayCreate(_realm, constructor, argumentList);
        if (result._contentType != exemplar._contentType)
        {
            Throw.TypeError(_realm, "Content type mismatch");
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#typedarray-create
    /// </summary>
    internal static JsTypedArray TypedArrayCreate(Realm realm, IConstructor constructor, JsCallArguments arguments)
    {
        var newTypedArray = Construct(constructor, arguments);
        var taRecord = newTypedArray.ValidateTypedArray(realm);

        if (arguments.Length == 1 && arguments[0] is JsNumber number)
        {
            if (taRecord.IsTypedArrayOutOfBounds)
            {
                Throw.TypeError(realm, "TypedArray is out of bounds");
            }
            if (newTypedArray.GetLength() < number._value)
            {
                Throw.TypeError(realm, "Derived TypedArray constructor created an array which was too small");
            }
        }

        return taRecord.Object;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%-@@species
    /// </summary>
    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject) => thisObject;

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Abstract class TypedArray not directly constructable");
        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Abstract class TypedArray not directly constructable");
        return null;
    }
}
