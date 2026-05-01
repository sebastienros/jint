#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set;

/// <summary>
/// https://www.ecma-international.org/ecma-262/6.0/#sec-set-objects
/// </summary>
[JsObject]
internal sealed partial class SetPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SetConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SetToStringTag = new("Set");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Spec requires Set.prototype.keys, Set.prototype.values, and Set.prototype[@@iterator] to all be
        // the same function object (function identity, observable via ===). Alias the descriptors here
        // so they share the same materialized function as `values` rather than emitting separate dispatchers.
        var valuesDesc = GetOwnProperty("values");
        SetProperty("keys", valuesDesc);
        SetProperty(GlobalSymbolRegistry.Iterator, valuesDesc);
    }

    [JsAccessor("size")]
    private JsNumber Size(JsValue thisObject)
    {
        AssertSetInstance(thisObject);
        return JsNumber.Create(0);
    }

    [JsFunction(Length = 1)]
    private JsValue Add(JsValue thisObject, JsValue value)
    {
        var set = AssertSetInstance(thisObject);
        if (value is JsNumber number && number.IsNegativeZero())
        {
            value = JsNumber.PositiveZero;
        }
        set.Add(value);
        return thisObject;
    }

    [JsFunction(Length = 0)]
    private JsValue Clear(JsValue thisObject)
    {
        var set = AssertSetInstance(thisObject);
        set.Clear();
        return Undefined;
    }

    [JsFunction(Length = 1)]
    private JsBoolean Delete(JsValue thisObject, JsValue value)
    {
        var set = AssertSetInstance(thisObject);
        return set.Delete(value)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    [JsFunction(Length = 1)]
    private JsSet Difference(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
        var otherRec = GetSetRecord(other);
        var resultSetData = new JsSet(_engine, new OrderedSet<JsValue>(set._set._set));

        if (set.Size <= otherRec.Size)
        {

            if (other is JsSet otherSet)
            {
                // fast path
                var result = new HashSet<JsValue>(set._set._set, SameValueZeroComparer.Instance);
                result.ExceptWith(otherSet._set._set);
                return new JsSet(_engine, new OrderedSet<JsValue>(result));
            }

            var index = 0;
            var args = new JsValue[1];
            while (index < set.Size)
            {
                var e = resultSetData[index];
                if (e is not null)
                {
                    args[0] = e;
                    var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                    if (inOther)
                    {
                        resultSetData.Delete(e);
                        index--;
                    }
                }

                index++;
            }

            return resultSetData;
        }

        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        while (true)
        {
            if (!keysIter.TryIteratorStep(out var next))
            {
                break;
            }

            var nextValue = next.Get(CommonProperties.Value);
            if (nextValue == JsNumber.NegativeZero)
            {
                nextValue = JsNumber.PositiveZero;
            }

            resultSetData.Delete(nextValue);
        }

        return resultSetData;
    }

    [JsFunction(Length = 1)]
    private JsBoolean IsDisjointFrom(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
        var otherRec = GetSetRecord(other);

        if (set.Size <= otherRec.Size)
        {
            if (other is JsSet otherSet)
            {
                // fast path
                return set._set._set.Overlaps(otherSet._set._set) ? JsBoolean.False : JsBoolean.True;
            }

            var index = 0;
            var args = new JsValue[1];
            while (index < set.Size)
            {
                var e = set[index];
                index++;
                if (e is not null)
                {
                    args[0] = e;
                    var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                    if (inOther)
                    {
                        return JsBoolean.False;
                    }
                }
            }

            return JsBoolean.True;
        }

        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        while (true)
        {
            if (!keysIter.TryIteratorStep(out var next))
            {
                break;
            }

            var nextValue = next.Get(CommonProperties.Value);
            if (set.Has(nextValue))
            {
                keysIter.Close(CompletionType.Normal);
                return JsBoolean.False;
            }
        }

        return JsBoolean.True;
    }


    [JsFunction(Length = 1)]
    private JsSet Intersection(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        var otherRec = GetSetRecord(other);
        var resultSetData = new JsSet(_engine);
        var thisSize = set.Size;

        if (thisSize <= otherRec.Size)
        {
            if (other is JsSet otherSet)
            {
                // fast path
                var result = new HashSet<JsValue>(set._set._set, SameValueZeroComparer.Instance);
                result.IntersectWith(otherSet._set._set);
                return new JsSet(_engine, new OrderedSet<JsValue>(result));
            }

            var index = 0;
            var args = new JsValue[1];
            while (index < thisSize)
            {
                var e = set[index];
                index++;
                if (e is not null)
                {
                    args[0] = e;
                    var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                    if (inOther)
                    {
                        var alreadyInResult = resultSetData.Has(e);
                        if (!alreadyInResult)
                        {
                            resultSetData.Add(e);
                        }
                    }
                    thisSize = set.Size;
                }
            }

            return resultSetData;
        }

        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        while (true)
        {
            if (!keysIter.TryIteratorStep(out var next))
            {
                break;
            }

            var nextValue = next.Get(CommonProperties.Value);
            if (nextValue == JsNumber.NegativeZero)
            {
                nextValue = JsNumber.PositiveZero;
            }

            var alreadyInResult = resultSetData.Has(nextValue);
            var inThis = set.Has(nextValue);
            if (!alreadyInResult && inThis)
            {
                resultSetData.Add(nextValue);
            }
        }

        return resultSetData;
    }

    [JsFunction(Length = 1)]
    private JsSet SymmetricDifference(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            var result = new HashSet<JsValue>(set._set._set, SameValueZeroComparer.Instance);
            result.SymmetricExceptWith(otherSet._set._set);
            return new JsSet(_engine, new OrderedSet<JsValue>(result));
        }

        var otherRec = GetSetRecord(other);
        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        var resultSetData = new JsSet(_engine, new OrderedSet<JsValue>(set._set._set));
        while (true)
        {
            if (!keysIter.TryIteratorStep(out var next))
            {
                break;
            }

            var nextValue = next.Get(CommonProperties.Value);
            if (nextValue == JsNumber.NegativeZero)
            {
                nextValue = JsNumber.PositiveZero;
            }

            var inResult = resultSetData.Has(nextValue);
            if (set.Has(nextValue))
            {
                if (inResult)
                {
                    resultSetData.Delete(nextValue);
                }
            }
            else
            {
                if (!inResult)
                {
                    resultSetData.Add(nextValue);
                }
            }
        }

        return resultSetData;
    }

    [JsFunction(Length = 1)]
    private JsBoolean IsSubsetOf(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            return set._set._set.IsSubsetOf(otherSet._set._set) ? JsBoolean.True : JsBoolean.False;
        }

        var otherRec = GetSetRecord(other);
        var thisSize = set.Size;

        if (thisSize > otherRec.Size)
        {
            return JsBoolean.False;
        }

        var index = 0;
        var args = new JsValue[1];
        while (index < thisSize)
        {
            var e = set[index];
            if (e is not null)
            {
                args[0] = e;
                var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                if (!inOther)
                {
                    return JsBoolean.False;
                }
            }

            thisSize = set.Size;
            index++;
        }

        return JsBoolean.True;
    }

    [JsFunction(Length = 1)]
    private JsBoolean IsSupersetOf(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            return set._set._set.IsSupersetOf(otherSet._set._set) ? JsBoolean.True : JsBoolean.False;
        }

        var thisSize = set.Size;
        var otherRec = GetSetRecord(other);

        if (thisSize < otherRec.Size)
        {
            return JsBoolean.False;
        }

        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        while (true)
        {
            if (!keysIter.TryIteratorStep(out var next))
            {
                break;
            }

            var nextValue = next.Get(CommonProperties.Value);
            if (!set.Has(nextValue))
            {
                keysIter.Close(CompletionType.Normal);
                return JsBoolean.False;
            }
        }

        return JsBoolean.True;
    }


    [JsFunction(Length = 1)]
    private JsBoolean Has(JsValue thisObject, JsValue value)
    {
        var set = AssertSetInstance(thisObject);
        return set.Has(value)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    [JsFunction(Length = 0)]
    private ObjectInstance Entries(JsValue thisObject)
    {
        var set = AssertSetInstance(thisObject);
        return set.Entries();
    }

    [JsFunction(Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callbackfn, JsValue thisArg)
    {
        var set = AssertSetInstance(thisObject);
        var callable = GetCallable(callbackfn);

        set.ForEach(callable, thisArg);

        return Undefined;
    }

    [JsFunction(Length = 1)]
    private JsSet Union(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
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

        var result = new JsSet(_engine, resultSetData);
        return result;
    }

    private readonly record struct SetRecord(JsValue Set, double Size, ICallable Has, ICallable Keys);

    private SetRecord GetSetRecord(JsValue obj)
    {
        if (obj is not ObjectInstance)
        {
            Throw.TypeError(_realm, "The .size property is accessed on an object that is not a valid Set or Set-like");
        }

        var rawSize = obj.Get(CommonProperties.Size);
        var numSize = TypeConverter.ToNumber(rawSize);
        if (double.IsNaN(numSize))
        {
            Throw.TypeError(_realm, "Invalid size");
        }

        var intSize = TypeConverter.ToIntegerOrInfinity(numSize);
        if (intSize < 0)
        {
            Throw.RangeError(_realm, "Invalid size");
        }

        var has = obj.Get(CommonProperties.Has);
        if (!has.IsCallable)
        {
            Throw.TypeError(_realm, $"{obj}.has is not a function");
        }

        var keys = obj.Get(CommonProperties.Keys);
        if (!keys.IsCallable)
        {
            Throw.TypeError(_realm, $"{obj}.keys is not a function");
        }

        return new SetRecord(Set: obj, Size: intSize, Has: (ICallable) has, Keys: (ICallable) keys);
    }

    [JsFunction(Length = 0)]
    private ObjectInstance Values(JsValue thisObject)
    {
        var set = AssertSetInstance(thisObject);
        return set.Values();
    }

    private JsSet AssertSetInstance(JsValue thisObject, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
    {
        if (thisObject is JsSet set)
        {
            return set;
        }

        Throw.TypeError(_realm, $"Method Set.prototype.{SetMethodName(methodName)} called on incompatible receiver {thisObject}");
        return default;
    }

    private static string SetMethodName(string callerName) => callerName switch
    {
        "Size" => "get size",
        _ => char.ToLowerInvariant(callerName[0]) + callerName.Substring(1)
    };
}
