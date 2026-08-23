#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set;

/// <summary>
/// https://www.ecma-international.org/ecma-262/6.0/#sec-set-objects
/// </summary>
// Set.prototype.keys and Set.prototype[@@iterator] are the same function as `values` (spec identity);
// [JsAlias] expresses the string alias through the shape, [JsSymbolAlias] the symbol-keyed alias.
[JsAlias("keys", "values")]
[JsSymbolAlias("Iterator", "values")]
[JsObject(UseShape = true)]
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
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-set.prototype.size
    /// </summary>
    [JsAccessor("size")]
    private JsNumber Size(JsValue thisObject)
    {
        var set = AssertSetInstance(thisObject);
        return JsNumber.Create(set.Size);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.add
    /// </summary>
    /// <remarks>
    /// Leaf for the same reason <c>Map.prototype.get</c> is (see <c>MapPrototype.MapGet</c>): a Set
    /// element is only hashed and compared with SameValueZero, which converts nothing, so no value can
    /// reach user code inside the frameless window — <c>LeafArg0 = AnyValue</c>. The receiver guard is
    /// what keeps <see cref="AssertSetInstance"/>'s TypeError framed, and a Map receiver fails it, so
    /// <c>Set.prototype.add.call(aMap, v)</c> still throws with its frame. Mutating is no obstacle:
    /// the elided frame carries the recursion charge and the JS-error stack and nothing an iteration
    /// or <c>size</c> read depends on.
    /// </remarks>
    [JsFunction(FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Set, LeafArg0 = FastCallGuard.AnyValue)]
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

    [JsFunction]
    private JsValue Clear(JsValue thisObject)
    {
        var set = AssertSetInstance(thisObject);
        set.Clear();
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.delete
    /// </summary>
    /// <remarks>Leaf on the same reasoning as <see cref="Add"/>.</remarks>
    [JsFunction(FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Set, LeafArg0 = FastCallGuard.AnyValue)]
    private JsBoolean Delete(JsValue thisObject, JsValue value)
    {
        var set = AssertSetInstance(thisObject);
        return set.Delete(value)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.difference
    /// </summary>
    [JsFunction]
    private JsSet Difference(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
        var otherRec = GetSetRecord(other);

        // "Let resultSetData be a copy of set.[[SetData]]", taken before anything else can run.
        var resultData = set._data.Clone();

        if (set.Size <= otherRec.Size)
        {
            // The copy is private to this call, so nothing can append to it; walking it with a cursor
            // is the spec's `index < thisSize` walk, with the entries this loop empties left behind it.
            var cursor = default(KeyedCollectionCursor);
            int slot;

            if (other is JsSet otherSet)
            {
                // fast path
                while ((slot = resultData.Next(ref cursor)) >= 0)
                {
                    if (otherSet._data.ContainsKey(resultData.KeyAt(slot)!))
                    {
                        resultData.RemoveAt(slot);
                    }
                }
            }
            else
            {
                var args = new JsValue[1];
                while ((slot = resultData.Next(ref cursor)) >= 0)
                {
                    args[0] = resultData.KeyAt(slot)!;
                    var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                    if (inOther)
                    {
                        resultData.RemoveAt(slot);
                    }
                }
            }

            resultData.TrimTombstones();
            return new JsSet(_engine, resultData);
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

            resultData.Remove(nextValue);
        }

        resultData.TrimTombstones();
        return new JsSet(_engine, resultData);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.isdisjointfrom
    /// </summary>
    [JsFunction]
    private JsBoolean IsDisjointFrom(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
        var otherRec = GetSetRecord(other);

        if (set.Size <= otherRec.Size)
        {
            if (other is JsSet otherSet)
            {
                // fast path: walk the smaller side, probing the larger
                var smaller = set._data;
                var larger = otherSet._data;
                if (smaller.Count > larger.Count)
                {
                    var swap = smaller;
                    smaller = larger;
                    larger = swap;
                }

                var enumerator = smaller.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (larger.ContainsKey(enumerator.Key))
                    {
                        return JsBoolean.False;
                    }
                }

                return JsBoolean.True;
            }

            var args = new JsValue[1];
            var cursor = default(KeyedCollectionCursor);
            int slot;
            while ((slot = set._data.Next(ref cursor)) >= 0)
            {
                args[0] = set._data.KeyAt(slot)!;
                var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                if (inOther)
                {
                    return JsBoolean.False;
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.intersection
    /// </summary>
    [JsFunction]
    private JsSet Intersection(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        var otherRec = GetSetRecord(other);
        var resultData = new KeyedCollectionData();

        if (set.Size <= otherRec.Size)
        {
            // The receiver's own [[SetData]] is walked by index, and otherRec.[[Has]] is free to add to
            // it, delete from it, or both — the cursor over the tombstoned List is what keeps the walk
            // on the entries the spec says it visits, in the order it says.
            var cursor = default(KeyedCollectionCursor);
            int slot;

            if (other is JsSet otherSet)
            {
                // fast path
                while ((slot = set._data.Next(ref cursor)) >= 0)
                {
                    var entry = set._data.KeyAt(slot)!;
                    if (otherSet._data.ContainsKey(entry))
                    {
                        resultData.Append(entry, null);
                    }
                }
            }
            else
            {
                var args = new JsValue[1];
                while ((slot = set._data.Next(ref cursor)) >= 0)
                {
                    var entry = set._data.KeyAt(slot)!;
                    args[0] = entry;
                    var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
                    if (inOther && !resultData.ContainsKey(entry))
                    {
                        resultData.Append(entry, null);
                    }
                }
            }

            return new JsSet(_engine, resultData);
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

            if (set.Has(nextValue) && !resultData.ContainsKey(nextValue))
            {
                resultData.Append(SameValueZeroComparer.ToStableKey(nextValue), null);
            }
        }

        return new JsSet(_engine, resultData);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.symmetricdifference
    /// </summary>
    [JsFunction]
    private JsSet SymmetricDifference(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            var fastResult = set._data.Clone();
            var enumerator = otherSet._data.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var key = enumerator.Key;
                if (set._data.ContainsKey(key))
                {
                    fastResult.Remove(key);
                }
                else
                {
                    fastResult.Add(key);
                }
            }

            fastResult.TrimTombstones();
            return new JsSet(_engine, fastResult);
        }

        var otherRec = GetSetRecord(other);
        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        var resultData = set._data.Clone();
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

            var alreadyInResult = resultData.ContainsKey(nextValue);
            if (set.Has(nextValue))
            {
                if (alreadyInResult)
                {
                    resultData.Remove(nextValue);
                }
            }
            else
            {
                if (!alreadyInResult)
                {
                    resultData.Append(SameValueZeroComparer.ToStableKey(nextValue), null);
                }
            }
        }

        resultData.TrimTombstones();
        return new JsSet(_engine, resultData);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.issubsetof
    /// </summary>
    [JsFunction]
    private JsBoolean IsSubsetOf(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            if (set.Size > otherSet.Size)
            {
                return JsBoolean.False;
            }

            var enumerator = set._data.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!otherSet._data.ContainsKey(enumerator.Key))
                {
                    return JsBoolean.False;
                }
            }

            return JsBoolean.True;
        }

        var otherRec = GetSetRecord(other);

        if (set.Size > otherRec.Size)
        {
            return JsBoolean.False;
        }

        var args = new JsValue[1];
        var cursor = default(KeyedCollectionCursor);
        int slot;
        while ((slot = set._data.Next(ref cursor)) >= 0)
        {
            args[0] = set._data.KeyAt(slot)!;
            var inOther = TypeConverter.ToBoolean(otherRec.Has.Call(otherRec.Set, args));
            if (!inOther)
            {
                return JsBoolean.False;
            }
        }

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.issupersetof
    /// </summary>
    [JsFunction]
    private JsBoolean IsSupersetOf(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);

        if (other is JsSet otherSet)
        {
            // fast path
            if (set.Size < otherSet.Size)
            {
                return JsBoolean.False;
            }

            var enumerator = otherSet._data.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!set._data.ContainsKey(enumerator.Key))
                {
                    return JsBoolean.False;
                }
            }

            return JsBoolean.True;
        }

        var otherRec = GetSetRecord(other);

        // Step 4 reads the receiver's size *after* GetSetRecord, and the difference is observable:
        // reading it first meant a set-like whose `size`, `has` or `keys` getter adds to the receiver
        // was compared against a size taken before its own getters had run.
        if (set.Size < otherRec.Size)
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


    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.has
    /// </summary>
    /// <remarks>Leaf on the same reasoning as <see cref="Add"/>.</remarks>
    [JsFunction(FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Set, LeafArg0 = FastCallGuard.AnyValue)]
    private JsBoolean Has(JsValue thisObject, JsValue value)
    {
        var set = AssertSetInstance(thisObject);
        return set.Has(value)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    [JsFunction]
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set.prototype.union
    /// </summary>
    [JsFunction]
    private JsSet Union(JsValue thisObject, JsValue other)
    {
        var set = AssertSetInstance(thisObject);
        var otherRec = GetSetRecord(other);
        var keysIter = otherRec.Set.GetIteratorFromMethod(_realm, otherRec.Keys);
        var resultData = set._data.Clone();
        while (keysIter.TryIteratorStep(out var next))
        {
            var nextValue = next.Get(CommonProperties.Value);
            if (nextValue == JsNumber.NegativeZero)
            {
                nextValue = JsNumber.PositiveZero;
            }

            if (!resultData.ContainsKey(nextValue))
            {
                // this is the one adder that reaches the ordering set directly rather than through
                // JsSet.Add, so it has to flatten a concatenated key itself
                resultData.Append(SameValueZeroComparer.ToStableKey(nextValue), null);
            }
        }

        var result = new JsSet(_engine, resultData);
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
            // Deliberately not interpolating obj: ObjectInstance.ToString() is the full JavaScript
            // ToString algorithm, so rendering the receiver here would run user code while the error
            // is being built (observable as extra [[Get]]s, and a throwing toString would replace
            // this TypeError with its own exception).
            Throw.TypeError(_realm, "The .has property of the object is not a function");
        }

        var keys = obj.Get(CommonProperties.Keys);
        if (!keys.IsCallable)
        {
            Throw.TypeError(_realm, "The .keys property of the object is not a function");
        }

        return new SetRecord(Set: obj, Size: intSize, Has: (ICallable) has, Keys: (ICallable) keys);
    }

    [JsFunction]
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

        Throw.TypeError(_realm, $"Method Set.prototype.{SetMethodName(methodName)} called on incompatible receiver");
        return default;
    }

    private static string SetMethodName(string callerName) => callerName switch
    {
        "Size" => "get size",
        _ => char.ToLowerInvariant(callerName[0]) + callerName.Substring(1)
    };
}
