#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Set;

/// <summary>
/// https://www.ecma-international.org/ecma-262/6.0/#sec-set-objects
/// </summary>
internal sealed class SetPrototype : Prototype
{
    private readonly SetConstructor _constructor;

    internal SetPrototype(
        Engine engine,
        Realm realm,
        SetConstructor setConstructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = setConstructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(12, checkExistingKeys: false)
        {
            ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["add"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "add", Add, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["clear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "clear", Clear, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Values, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["size"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get size", Size, 0, PropertyFlag.Configurable), set: null, PropertyFlag.Configurable),
            ["union"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "union", Union, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", Values, 1, PropertyFlag.Configurable), true, false, true),
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("Set", false, false, true)
        };
        SetSymbols(symbols);
    }

    private JsValue Size(JsValue thisObject, JsValue[] arguments)
    {
        AssertSetInstance(thisObject);
        return JsNumber.Create(0);
    }

    private JsValue Add(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        var value = arguments.At(0);
        if (value is JsNumber number && number.IsNegativeZero())
        {
            value = JsNumber.PositiveZero;
        }
        set.Add(value);
        return thisObject;
    }

    private JsValue Clear(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        set.Clear();
        return Undefined;
    }

    private JsValue Delete(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        return set.SetDelete(arguments.At(0))
            ? JsBoolean.True
            : JsBoolean.False;
    }

    private JsValue Has(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        return set.Has(arguments.At(0))
            ? JsBoolean.True
            : JsBoolean.False;
    }

    private JsValue Entries(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        return set.Entries();
    }

    private JsValue ForEach(JsValue thisObject, JsValue[] arguments)
    {
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var set = AssertSetInstance(thisObject);
        var callable = GetCallable(callbackfn);

        set.ForEach(callable, thisArg);

        return Undefined;
    }

    private JsValue Union(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        var other = arguments.At(0);
        var otherRec = GetSetRecord(other);
        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        var resultSetData = set._set.Clone();
        while (keysIter.TryIteratorStep(out var next))
        {
            var nextValue = next.Get(CommonProperties.Value);
            if (nextValue == JsNumber.NegativeZero)
            {
                nextValue = JsNumber.PositiveZero;
            }
            resultSetData.Add(nextValue);
        }

        var result = new JsSet(_engine, resultSetData)
        {
            _prototype = _engine.Realm.Intrinsics.Set.PrototypeObject
        };
        return result;

    }

    private readonly record struct SetRecord(JsValue Set, double Size, ICallable Has, ICallable Keys);

    private SetRecord GetSetRecord(JsValue obj)
    {
        if (obj is not ObjectInstance)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var rawSize = obj.Get("size");
        var numSize = TypeConverter.ToNumber(rawSize);
        if (double.IsNaN(numSize))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var intSize = TypeConverter.ToIntegerOrInfinity(numSize);
        if (intSize < 0)
        {
            ExceptionHelper.ThrowRangeError(_realm);
        }

        var has = obj.Get(CommonProperties.Has);
        if (!has.IsCallable)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var keys = obj.Get(CommonProperties.Keys);
        if (!keys.IsCallable)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        return new SetRecord(Set: obj, Size: intSize, Has: (ICallable) has, Keys: (ICallable) keys);
    }

    private ObjectInstance Values(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertSetInstance(thisObject);
        return set.Values();
    }

    private JsSet AssertSetInstance(JsValue thisObject)
    {
        if (thisObject is JsSet set)
        {
            return set;
        }

        ExceptionHelper.ThrowTypeError(_realm, "object must be a Set");
        return default;
    }
}
