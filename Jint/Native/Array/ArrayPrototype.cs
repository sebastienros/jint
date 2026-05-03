#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Linq;
using System.Text;
using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-array-prototype-object
/// </summary>
[JsObject]
public sealed partial class ArrayPrototype : ArrayInstance
{
    private const int ConstraintCheckInterval = 10_000;

    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ArrayConstructor _constructor;

    private readonly ObjectTraverseStack _joinStack;
    internal JsValue? _originalIteratorFunction;

    internal ArrayPrototype(
        Engine engine,
        Realm realm,
        ArrayConstructor arrayConstructor,
        ObjectPrototype objectPrototype) : base(engine, InternalTypes.Object)
    {
        _prototype = objectPrototype;
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Writable);
        _realm = realm;
        _constructor = arrayConstructor;
        _joinStack = new(engine);
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        CreateProperties_Generated();

        // Per spec, Array.prototype[@@iterator] is the SAME function object as Array.prototype.values
        // (ECMA-262 23.1.3.34). Materialize the generated `values` descriptor and reuse it both as the
        // Symbol.iterator value and as _originalIteratorFunction for ArrayInstance.HasOriginalIterator
        // fast-path detection (ArrayInstance.cs:73).
        _originalIteratorFunction = GetOwnProperty("values").Value;
        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(_originalIteratorFunction, PropertyFlags),
            [GlobalSymbolRegistry.Unscopables] = new LazyPropertyDescriptor<Engine>(_engine, static engine =>
            {
                var unscopables = new JsObject(engine)
                {
                    _prototype = null
                };

                unscopables.FastSetDataProperty("at", JsBoolean.True);
                unscopables.FastSetDataProperty("copyWithin", JsBoolean.True);
                unscopables.FastSetDataProperty("entries", JsBoolean.True);
                unscopables.FastSetDataProperty("fill", JsBoolean.True);
                unscopables.FastSetDataProperty("find", JsBoolean.True);
                unscopables.FastSetDataProperty("findIndex", JsBoolean.True);
                unscopables.FastSetDataProperty("findLast", JsBoolean.True);
                unscopables.FastSetDataProperty("findLastIndex", JsBoolean.True);
                unscopables.FastSetDataProperty("flat", JsBoolean.True);
                unscopables.FastSetDataProperty("flatMap", JsBoolean.True);
                unscopables.FastSetDataProperty("includes", JsBoolean.True);
                unscopables.FastSetDataProperty("keys", JsBoolean.True);
                unscopables.FastSetDataProperty("toReversed", JsBoolean.True);
                unscopables.FastSetDataProperty("toSorted", JsBoolean.True);
                unscopables.FastSetDataProperty("toSpliced", JsBoolean.True);
                unscopables.FastSetDataProperty("values", JsBoolean.True);

                return unscopables;
            }, PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    [JsFunction]
    private ObjectInstance Keys(JsValue thisObject)
    {
        if (thisObject is ObjectInstance oi && oi.IsArrayLike)
        {
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.Key);
        }

        Throw.TypeError(_realm, "cannot construct iterator");
        return null;
    }

    [JsFunction]
    internal ObjectInstance Values(JsValue thisObject)
    {
        if (thisObject is ObjectInstance oi && oi.IsArrayLike)
        {
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.Value);
        }

        Throw.TypeError(_realm, "cannot construct iterator");
        return null;
    }

    [JsFunction(Length = 2)]
    private ObjectInstance With(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(TypeConverter.ToObject(_realm, thisObject), forWrite: false);
        var len = o.GetLongLength();
        var relativeIndex = TypeConverter.ToIntegerOrInfinity(arguments.At(0));
        var value = arguments.At(1);

        long actualIndex;
        if (relativeIndex >= 0)
        {
            actualIndex = (long) relativeIndex;
        }
        else
        {
            actualIndex = (long) (len + relativeIndex);
        }

        if (actualIndex >= (long) len || actualIndex < 0)
        {
            Throw.RangeError(_realm, "Invalid start index");
        }

        var a = CreateBackingArray(len);
        ulong k = 0;
        while (k < len)
        {
            a[k] = k == (ulong) actualIndex ? value : o.Get(k);
            k++;

            // Check constraints periodically to prevent long-running operations
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }
        return new JsArray(_engine, a);
    }

    [JsFunction]
    private ObjectInstance Entries(JsValue thisObject)
    {
        if (thisObject is ObjectInstance oi && oi.IsArrayLike)
        {
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.KeyAndValue);
        }

        Throw.TypeError(_realm, "cannot construct iterator");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.fill
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Fill(JsValue thisObject, JsCallArguments arguments)
    {
        var value = arguments.At(0);
        var start = arguments.At(1);
        var end = arguments.At(2);

        var o = TypeConverter.ToObject(_realm, thisObject);

        var operations = ArrayOperations.For(o, forWrite: true);
        var length = operations.GetLongLength();

        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        ulong k;
        if (double.IsNegativeInfinity(relativeStart))
        {
            k = 0;
        }
        else if (relativeStart < 0)
        {
            k = (ulong) System.Math.Max(length + relativeStart, 0);
        }
        else
        {
            k = (ulong) System.Math.Min(relativeStart, length);
        }

        var relativeEnd = end.IsUndefined() ? length : TypeConverter.ToIntegerOrInfinity(end);
        ulong final;
        if (double.IsNegativeInfinity(relativeEnd))
        {
            final = 0;
        }
        else if (relativeEnd < 0)
        {
            final = (ulong) System.Math.Max(length + relativeEnd, 0);
        }
        else
        {
            final = (ulong) System.Math.Min(relativeEnd, length);
        }

        // Check constraints periodically to prevent memory exhaustion in large fills
        for (var i = k; i < final; ++i)
        {
            operations.Set(i, value, throwOnError: false);

            if (i > k && (i - k) % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.copywithin
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue CopyWithin(JsValue thisObject, JsCallArguments arguments)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);

        JsValue target = arguments.At(0);
        JsValue start = arguments.At(1);
        JsValue end = arguments.At(2);

        var operations = ArrayOperations.For(o, forWrite: true);
        var len = operations.GetLongLength();

        var relativeTarget = TypeConverter.ToIntegerOrInfinity(target);

        var to = relativeTarget < 0 ?
            System.Math.Max(len + relativeTarget, 0) :
            System.Math.Min(relativeTarget, len);

        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        long from;
        if (double.IsNegativeInfinity(relativeStart))
        {
            from = 0;
        }
        else if (relativeStart < 0)
        {
            from = (long) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            from = (long) System.Math.Min(relativeStart, len);
        }

        var relativeEnd = end.IsUndefined() ? len : TypeConverter.ToIntegerOrInfinity(end);

        long final;
        if (double.IsNegativeInfinity(relativeEnd))
        {
            final = 0;
        }
        else if (relativeEnd < 0)
        {
            final = (long) System.Math.Max(len + relativeEnd, 0);
        }
        else
        {
            final = (long) System.Math.Min(relativeEnd, len);
        }

        var count = (long) System.Math.Min(final - from, len - to);
        var initialCount = count;

        long direction = 1;

        if (from < to && to < from + count)
        {
            direction = -1;
            from += count - 1;
            to += count - 1;
        }

        while (count > 0)
        {
            var fromPresent = operations.HasProperty((ulong) from);
            if (fromPresent)
            {
                var fromValue = operations.Get((ulong) from);
                operations.Set((ulong) to, fromValue, updateLength: true, throwOnError: true);
            }
            else
            {
                operations.DeletePropertyOrThrow((ulong) to);
            }
            from += direction;
            to += direction;
            count--;

            // Check constraints periodically to prevent long-running operations
            if ((initialCount - count) % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.lastindexof
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue LastIndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLongLength();
        if (len == 0)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var n = arguments.Length > 1
            ? TypeConverter.ToInteger(arguments[1])
            : len - 1;
        double k;
        if (n >= 0)
        {
            k = System.Math.Min(n, len - 1); // min
        }
        else
        {
            k = len - System.Math.Abs(n);
        }

        if (k < 0 || k > ArrayOperations.MaxArrayLikeLength)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var searchElement = arguments.At(0);
        var i = (ulong) k;
        for (; ; i--)
        {
            var kPresent = o.HasProperty(i);
            if (kPresent)
            {
                var elementK = o.Get(i);
                if (elementK == searchElement)
                {
                    return i;
                }
            }

            if (i == 0)
            {
                break;
            }
        }

        return JsNumber.IntegerNegativeOne;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.reduce
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Reduce(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var initialValue = arguments.At(1);

        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var len = o.GetLength();

        var callable = GetCallable(callbackfn);

        if (len == 0 && arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Reduce of empty array with no initial value");
        }

        var k = 0;
        JsValue accumulator = Undefined;
        if (arguments.Length > 1)
        {
            accumulator = initialValue;
        }
        else
        {
            var kPresent = false;
            while (kPresent == false && k < len)
            {
                if (kPresent = o.TryGetValue((uint) k, out var temp))
                {
                    accumulator = temp;
                }

                k++;
            }

            if (kPresent == false)
            {
                Throw.TypeError(_realm, "Reduce of empty array with no initial value");
            }
        }

        var args = new JsValue[4];
        args[3] = o.Target;
        while (k < len)
        {
            var i = (uint) k;
            if (o.TryGetValue(i, out var kvalue))
            {
                args[0] = accumulator;
                args[1] = kvalue;
                args[2] = i;
                accumulator = callable.Call(Undefined, args);
            }

            k++;
        }

        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.filter
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Filter(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLength();

        var callable = GetCallable(callbackfn);

        var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObject), 0);
        var operations = ArrayOperations.For(a, forWrite: true);

        uint to = 0;
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o.Target;
        for (uint k = 0; k < len; k++)
        {
            if (o.TryGetValue(k, out var kvalue))
            {
                args[0] = kvalue;
                args[1] = k;
                var selected = callable.Call(thisArg, args);
                if (TypeConverter.ToBoolean(selected))
                {
                    operations.CreateDataPropertyOrThrow(to, kvalue);
                    to++;
                }
            }
        }

        operations.SetLength(to);
        _engine._jsValueArrayPool.ReturnArray(args);

        return a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.map
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Map(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is JsArray { CanUseFastAccess: true } arrayInstance
            && !arrayInstance.HasOwnProperty(CommonProperties.Constructor))
        {
            return arrayInstance.Map(arguments);
        }

        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLongLength();

        if (len > ArrayOperations.MaxArrayLength)
        {
            Throw.RangeError(_realm, "Invalid array length");
        }

        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);
        var callable = GetCallable(callbackfn);

        var a = ArrayOperations.For(_realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObject), (uint) len), forWrite: true);
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o.Target;
        for (uint k = 0; k < len; k++)
        {
            if (o.TryGetValue(k, out var kvalue))
            {
                args[0] = kvalue;
                args[1] = k;
                var mappedValue = callable.Call(thisArg, args);
                a.CreateDataPropertyOrThrow(k, mappedValue);
            }
        }
        _engine._jsValueArrayPool.ReturnArray(args);
        return a.Target;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.flat
    /// </summary>
    [JsFunction]
    private JsValue Flat(JsValue thisObject, JsCallArguments arguments)
    {
        var operations = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var sourceLen = operations.GetLength();
        double depthNum = 1;
        var depth = arguments.At(0);
        if (!depth.IsUndefined())
        {
            depthNum = TypeConverter.ToIntegerOrInfinity(depth);
        }

        if (depthNum < 0)
        {
            depthNum = 0;
        }

        var A = _realm.Intrinsics.Array.ArraySpeciesCreate(operations.Target, 0);
        FlattenIntoArray(A, operations, sourceLen, 0, depthNum);
        return A;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.flatmap
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue FlatMap(JsValue thisObject, JsCallArguments arguments)
    {
        var O = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var mapperFunction = arguments.At(0);
        var thisArg = arguments.At(1);

        var sourceLen = O.GetLength();

        if (!mapperFunction.IsCallable)
        {
            Throw.TypeError(_realm, $"{mapperFunction} is not a function");
        }

        var A = _realm.Intrinsics.Array.ArraySpeciesCreate(O.Target, 0);
        FlattenIntoArray(A, O, sourceLen, 0, 1, (ICallable) mapperFunction, thisArg);
        return A;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-flattenintoarray
    /// </summary>
    private ulong FlattenIntoArray(
        ObjectInstance target,
        ArrayOperations source,
        uint sourceLen,
        ulong start,
        double depth,
        ICallable? mapperFunction = null,
        JsValue? thisArg = null)
    {
        var targetIndex = start;
        ulong sourceIndex = 0;

        var callArguments = System.Array.Empty<JsValue>();
        if (mapperFunction is not null)
        {
            callArguments = _engine._jsValueArrayPool.RentArray(3);
            callArguments[2] = source.Target;
        }

        while (sourceIndex < sourceLen)
        {
            var exists = source.HasProperty(sourceIndex);
            if (exists)
            {
                var element = source.Get(sourceIndex);
                if (mapperFunction is not null)
                {
                    callArguments[0] = element;
                    callArguments[1] = JsNumber.Create(sourceIndex);
                    element = mapperFunction.Call(thisArg ?? Undefined, callArguments);
                }

                var shouldFlatten = false;
                if (depth > 0)
                {
                    shouldFlatten = element.IsArray();
                }

                if (shouldFlatten)
                {
                    var newDepth = double.IsPositiveInfinity(depth)
                        ? depth
                        : depth - 1;

                    var objectInstance = (ObjectInstance) element;
                    var elementLen = objectInstance.GetLength();
                    targetIndex = FlattenIntoArray(target, ArrayOperations.For(objectInstance, forWrite: false), elementLen, targetIndex, newDepth);
                }
                else
                {
                    if (targetIndex >= NumberConstructor.MaxSafeInteger)
                    {
                        Throw.TypeError(_realm, "Invalid array length");
                    }

                    target.CreateDataPropertyOrThrow(targetIndex, element);
                    targetIndex += 1;
                }
            }

            sourceIndex++;
        }

        if (mapperFunction is not null)
        {
            _engine._jsValueArrayPool.ReturnArray(callArguments);
        }

        return targetIndex;
    }

    [JsFunction(Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLength();

        var callable = GetCallable(callbackfn);

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o.Target;
        for (uint k = 0; k < len; k++)
        {
            if (o.TryGetValue(k, out var kvalue))
            {
                args[0] = kvalue;
                args[1] = k;
                callable.Call(thisArg, args);
            }
        }
        _engine._jsValueArrayPool.ReturnArray(args);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.includes
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Includes(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = (long) o.GetLongLength();

        if (len == 0)
        {
            return JsBoolean.False;
        }

        var searchElement = arguments.At(0);
        var fromIndex = arguments.At(1);

        long k = 0;
        var n = TypeConverter.ToIntegerOrInfinity(fromIndex);
        if (double.IsPositiveInfinity(n))
        {
            return JsBoolean.False;
        }
        else if (double.IsNegativeInfinity(n))
        {
            n = 0;
        }
        else if (n >= 0)
        {
            k = (long) n;
        }
        else
        {
            k = len + (long) n;
            if (k < 0)
            {
                k = 0;
            }
        }

        // Fast path: dense JsArray scan avoids the per-element virtual call through ArrayOperations.Get.
        if (thisObject is JsArray { CanUseFastAccess: true } fast && fast._dense is { } dense)
        {
            var actualLen = (int) System.Math.Min((uint) dense.Length, (uint) len);
            for (var i = (int) k; i < actualLen; i++)
            {
                var v = dense[i];
                // Holes count as undefined for Includes.
                v ??= Undefined;
                if (SameValueZeroComparer.Equals(v, searchElement))
                {
                    return JsBoolean.True;
                }
            }
            // Trailing positions beyond _dense are holes => undefined.
            if (actualLen < len && SameValueZeroComparer.Equals(Undefined, searchElement))
            {
                return JsBoolean.True;
            }
            return JsBoolean.False;
        }

        while (k < len)
        {
            var value = o.Get((ulong) k);
            if (SameValueZeroComparer.Equals(value, searchElement))
            {
                return true;
            }
            k++;
        }
        return false;
    }

    [JsFunction(Length = 1)]
    private JsValue Some(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        return target.FindWithCallback(arguments, out _, out _, false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.every
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Every(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        ulong len = o.GetLongLength();

        if (len == 0)
        {
            return JsBoolean.True;
        }

        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);
        var callable = GetCallable(callbackfn);

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o.Target;
        for (uint k = 0; k < len; k++)
        {
            if (o.TryGetValue(k, out var kvalue))
            {
                args[0] = kvalue;
                args[1] = k;
                var testResult = callable.Call(thisArg, args);
                if (!TypeConverter.ToBoolean(testResult))
                {
                    return JsBoolean.False;
                }
            }
        }
        _engine._jsValueArrayPool.ReturnArray(args);

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.indexof
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue IndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLongLength();
        if (len == 0)
        {
            return -1;
        }

        var startIndex = arguments.Length > 1
            ? TypeConverter.ToIntegerOrInfinity(arguments.At(1))
            : 0;

        if (startIndex > ArrayOperations.MaxArrayLikeLength)
        {
            return JsNumber.IntegerNegativeOne;
        }

        ulong k;
        if (startIndex < 0)
        {
            var abs = System.Math.Abs(startIndex);
            ulong temp = len - (uint) abs;
            if (abs > len || temp < 0)
            {
                temp = 0;
            }

            k = temp;
        }
        else
        {
            k = (ulong) startIndex;
        }

        if (k >= len)
        {
            return -1;
        }

        ulong smallestIndex = o.GetSmallestIndex(len);
        if (smallestIndex > k)
        {
            k = smallestIndex;
        }

        var searchElement = arguments.At(0);

        // Fast path: dense JsArray scan avoids the per-element HasProperty + Get virtual call pair.
        if (thisObject is JsArray { CanUseFastAccess: true } fast && fast._dense is { } dense)
        {
            var actualLen = (int) System.Math.Min((uint) dense.Length, (uint) len);
            for (var i = (int) k; i < actualLen; i++)
            {
                var v = dense[i];
                if (v is not null && v.Equals(searchElement))
                {
                    return JsNumber.Create((uint) i);
                }
            }
            return JsNumber.IntegerNegativeOne;
        }

        for (; k < len; k++)
        {
            var kPresent = o.HasProperty(k);
            if (kPresent)
            {
                var elementK = o.Get(k);
                if (elementK == searchElement)
                {
                    return k;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.find
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Find(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        target.FindWithCallback(arguments, out _, out var value, visitUnassigned: true);
        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.findindex
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue FindIndex(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        if (target.FindWithCallback(arguments, out var index, out _, visitUnassigned: true))
        {
            return index;
        }
        return -1;
    }

    [JsFunction(Length = 1)]
    private JsValue FindLast(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        target.FindWithCallback(arguments, out _, out var value, visitUnassigned: true, fromEnd: true);
        return value;
    }

    [JsFunction(Length = 1)]
    private JsValue FindLastIndex(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        if (target.FindWithCallback(arguments, out var index, out _, visitUnassigned: true, fromEnd: true))
        {
            return index;
        }
        return -1;
    }

    /// <summary>
    /// https://tc39.es/proposal-relative-indexing-method/#sec-array-prototype-additions
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue At(JsValue thisObject, JsCallArguments arguments)
    {
        var target = TypeConverter.ToObject(_realm, thisObject);
        var len = target.GetLength();
        var relativeIndex = TypeConverter.ToInteger(arguments.At(0));

        long actualIndex;
        if (relativeIndex < 0)
        {
            actualIndex = (long) (len + relativeIndex);
        }
        else
        {
            actualIndex = (long) relativeIndex;
        }

        if (actualIndex < 0 || (ulong) actualIndex >= len)
        {
            return Undefined;
        }

        return target.Get((ulong) actualIndex);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.splice
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue Splice(JsValue thisObject, JsCallArguments arguments)
    {
        var start = arguments.At(0);
        var deleteCount = arguments.At(1);

        var obj = TypeConverter.ToObject(_realm, thisObject);
        var o = ArrayOperations.For(_realm, obj, forWrite: true);
        var len = o.GetLongLength();
        var relativeStart = TypeConverter.ToInteger(start);

        ulong actualStart;
        if (relativeStart < 0)
        {
            actualStart = (ulong) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            actualStart = (ulong) System.Math.Min(relativeStart, len);
        }

        var items = System.Array.Empty<JsValue>();
        ulong insertCount;
        ulong actualDeleteCount;
        if (arguments.Length == 0)
        {
            insertCount = 0;
            actualDeleteCount = 0;
        }
        else if (arguments.Length == 1)
        {
            insertCount = 0;
            actualDeleteCount = len - actualStart;
        }
        else
        {
            insertCount = (ulong) (arguments.Length - 2);
            var dc = TypeConverter.ToInteger(deleteCount);
            actualDeleteCount = (ulong) System.Math.Min(System.Math.Max(dc, 0), len - actualStart);

            items = [];
            if (arguments.Length > 2)
            {
                items = new JsValue[arguments.Length - 2];
                System.Array.Copy(arguments, 2, items, 0, items.Length);
            }
        }

        if (len + insertCount - actualDeleteCount > ArrayOperations.MaxArrayLikeLength)
        {
            Throw.TypeError(_realm, "Invalid array length");
        }

        var instance = _realm.Intrinsics.Array.ArraySpeciesCreate(obj, actualDeleteCount);
        var a = ArrayOperations.For(instance, forWrite: true);

        // Fast path: dense JsArray with default prototype, fits in uint, and the result array
        // is also a fresh dense JsArray we can fill via Array.Copy. Replaces three O(n) loops
        // (capture deleted, shift tail, write inserts) with up to three Array.Copy calls.
        if (len + (ulong) items.Length <= uint.MaxValue
            && actualStart <= uint.MaxValue && actualDeleteCount <= uint.MaxValue
            && o.Target is JsArray jsArr && jsArr._dense is { } srcDense && jsArr.CanUseFastAccess
            && a.Target is JsArray resultArr && resultArr._dense is not null)
        {
            var startU = (uint) actualStart;
            var deleteU = (uint) actualDeleteCount;
            var insertU = (uint) items.Length;
            var lenU = (uint) len;
            var newLenU = (uint) (len - actualDeleteCount + (ulong) items.Length);

            // 1. Copy deleted slots into the result array.
            if (deleteU > 0)
            {
                resultArr.EnsureCapacity(deleteU);
                var availableInSource = startU < (uint) srcDense.Length
                    ? System.Math.Min(deleteU, (uint) srcDense.Length - startU)
                    : 0u;
                if (availableInSource > 0)
                {
                    System.Array.Copy(srcDense, startU, resultArr._dense!, 0, availableInSource);
                }
            }
            resultArr.SetLength(deleteU);

            // 2. Shift the tail to its new position.
            var tailStart = startU + deleteU;
            var tailCount = lenU - tailStart;
            if (tailCount > 0 && insertU != deleteU)
            {
                jsArr.TryMoveDenseRange(sourceIndex: tailStart, destIndex: startU + insertU, count: tailCount);
            }

            // 3. Clear stale slots when shrinking.
            if (newLenU < lenU)
            {
                jsArr.ClearDenseRange(newLenU, lenU - newLenU);
            }

            // 4. Write the inserted items. Amortized grow for the case where step 2 didn't
            // run (insertU == deleteU or tailCount == 0) yet we still need write capacity.
            if (insertU > 0)
            {
                jsArr.EnsureCapacityAmortized(startU + insertU);
                for (uint k = 0; k < insertU; k++)
                {
                    jsArr._dense![startU + k] = items[(int) k];
                }
            }

            jsArr.SetLength(newLenU);
            return resultArr;
        }

        for (uint k = 0; k < actualDeleteCount; k++)
        {
            var index = actualStart + k;
            if (o.HasProperty(index))
            {
                var fromValue = o.Get(index);
                a.CreateDataPropertyOrThrow(k, fromValue);
            }
        }
        a.SetLength((uint) actualDeleteCount);

        var length = len - actualDeleteCount + (uint) items.Length;
        o.EnsureCapacity(length);
        if ((ulong) items.Length < actualDeleteCount)
        {
            for (ulong k = actualStart; k < len - actualDeleteCount; k++)
            {
                var from = k + actualDeleteCount;
                var to = k + (ulong) items.Length;
                if (o.HasProperty(from))
                {
                    var fromValue = o.Get(from);
                    o.Set(to, fromValue, throwOnError: false);
                }
                else
                {
                    o.DeletePropertyOrThrow(to);
                }
            }

            for (var k = len; k > len - actualDeleteCount + (ulong) items.Length; k--)
            {
                o.DeletePropertyOrThrow(k - 1);
            }
        }
        else if ((ulong) items.Length > actualDeleteCount)
        {
            for (var k = len - actualDeleteCount; k > actualStart; k--)
            {
                var from = k + actualDeleteCount - 1;
                var to = k + (ulong) items.Length - 1;
                if (o.HasProperty(from))
                {
                    var fromValue = o.Get(from);
                    o.Set(to, fromValue, throwOnError: true);
                }
                else
                {
                    o.DeletePropertyOrThrow(to);
                }
            }
        }

        for (uint k = 0; k < items.Length; k++)
        {
            var e = items[k];
            o.Set(k + actualStart, e, throwOnError: true);
        }

        o.SetLength(length);
        return a.Target;
    }

    /// <summary>
    /// /https://tc39.es/ecma262/#sec-array.prototype.unshift
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Unshift(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var len = o.GetLongLength();
        var argCount = (uint) arguments.Length;

        if (len + argCount > ArrayOperations.MaxArrayLikeLength)
        {
            Throw.TypeError(_realm, "Invalid array length");
        }

        // Fast path: dense JsArray with default prototype — bulk Array.Copy right by argCount
        // then write the new front slots, instead of O(n) per-element Set/HasProperty.
        if (argCount > 0 && len <= uint.MaxValue && o.Target is JsArray jsArr
            && jsArr._dense is not null && jsArr.CanUseFastAccess
            && len + argCount <= ArrayInstance.MaxDenseArrayLengthInternal)
        {
            var lenU = (uint) len;
            var newLenU = lenU + argCount;
            // Amortized grow so back-to-back unshift loops don't incur O(N²) Array.Resize work
            // (matches the doubling growth that the slow path's per-element Set() relies on).
            jsArr.EnsureCapacityAmortized(newLenU);
            if (lenU > 0)
            {
                System.Array.Copy(jsArr._dense!, 0, jsArr._dense!, argCount, lenU);
            }
            for (uint j = 0; j < argCount; j++)
            {
                jsArr._dense![j] = arguments[(int) j];
            }
            jsArr.SetLength(newLenU);
            return newLenU;
        }

        // only prepare for larger if we cannot rely on default growth algorithm
        if (len + argCount > 2 * len)
        {
            o.EnsureCapacity(len + argCount);
        }

        var minIndex = o.GetSmallestIndex(len);
        for (var k = len; k > minIndex; k--)
        {
            var from = k - 1;
            var to = k + argCount - 1;
            if (o.TryGetValue(from, out var fromValue))
            {
                o.Set(to, fromValue, updateLength: false);
            }
            else
            {
                o.DeletePropertyOrThrow(to);
            }
        }

        for (uint j = 0; j < argCount; j++)
        {
            o.Set(j, arguments[j], updateLength: false);
        }

        o.SetLength(len + argCount);
        return len + argCount;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.sort
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Sort(JsValue thisObject, JsCallArguments arguments)
    {
        var obj = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var compareFn = GetCompareFunction(arguments.At(0));

        var len = obj.GetLength();
        if (len <= 1)
        {
            return obj.Target;
        }

        // capacity capped to a sane upper bound so a 1e9-length sparse array doesn't preallocate everything
        var items = new List<JsValue>((int) System.Math.Min(1024, len));
        for (ulong k = 0; k < len; ++k)
        {
            if (obj.TryGetValue(k, out var kValue))
            {
                items.Add(kValue);
            }
        }

        var itemCount = items.Count;

        // don't eat inner exceptions
        try
        {
            if (compareFn is null)
            {
                SortByCachedStringKeys(items);
            }
            else
            {
                var comparer = ArrayComparer.WithFunction(_engine, compareFn);
#if NETCOREAPP
                // OrderBy is stable; List<T>.Sort is not. Stability is required by the spec since ES2019.
#if NET8_0_OR_GREATER
                items = items.Order(comparer).ToList();
#else
                items = items.OrderBy(x => x, comparer).ToList();
#endif
#else
                items.Sort(comparer);
#endif
            }

            for (uint j = 0; j < itemCount; j++)
            {
                obj.Set(j, items[(int) j], updateLength: false, throwOnError: true);
            }

            // Holes (TryGetValue returned false) only need clearing when the input had holes.
            for (uint j = (uint) itemCount; j < len; ++j)
            {
                obj.DeletePropertyOrThrow(j);
            }
        }
        catch (InvalidOperationException e)
        {
            throw e.InnerException ?? e;
        }

        return obj.Target;
    }

    /// <summary>
    /// Default ECMA Array.prototype.sort path: SortCompare coerces both operands via ToString
    /// per comparison, which is O(n log n) ToString allocations. Cache the coerced key once per
    /// element (Schwartzian transform), then sort indices stably (tiebreak by original index).
    /// </summary>
    private static void SortByCachedStringKeys(List<JsValue> items)
    {
        var itemCount = items.Count;
        if (itemCount <= 1)
        {
            return;
        }

        var keys = new string?[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            var v = items[i];
            // Spec: undefined elements sort to the end and are not coerced via ToString.
            keys[i] = v.IsUndefined() ? null : TypeConverter.ToString(v);
        }

        var indices = new int[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            indices[i] = i;
        }

        System.Array.Sort(indices, new CachedStringKeyComparer(keys));

        // Apply permutation via a temp buffer; cycle-permute would avoid the allocation but adds risk.
        var sorted = new JsValue[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            sorted[i] = items[indices[i]];
        }
        for (var i = 0; i < itemCount; i++)
        {
            items[i] = sorted[i];
        }
    }

    private sealed class CachedStringKeyComparer : IComparer<int>
    {
        private readonly string?[] _keys;
        public CachedStringKeyComparer(string?[] keys) => _keys = keys;
        public int Compare(int a, int b)
        {
            var ka = _keys[a];
            var kb = _keys[b];
            // undefined sorts to the end (spec: SortCompare returns 1 if x is undefined, -1 if y is)
            if (ka is null)
            {
                if (kb is null)
                {
                    return a - b; // tiebreak by original index for stability
                }
                return 1;
            }
            if (kb is null)
            {
                return -1;
            }
            var c = string.CompareOrdinal(ka, kb);
            return c != 0 ? c : a - b; // tiebreak by original index for stability
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.slice
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue Slice(JsValue thisObject, JsCallArguments arguments)
    {
        var start = arguments.At(0);
        var end = arguments.At(1);

        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLongLength();

        var relativeStart = TypeConverter.ToInteger(start);
        ulong k;
        if (relativeStart < 0)
        {
            k = (ulong) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            k = (ulong) System.Math.Min(TypeConverter.ToInteger(start), len);
        }

        ulong final;
        if (end.IsUndefined())
        {
            final = (ulong) TypeConverter.ToNumber(len);
        }
        else
        {
            double relativeEnd = TypeConverter.ToInteger(end);
            if (relativeEnd < 0)
            {
                final = (ulong) System.Math.Max(len + relativeEnd, 0);
            }
            else
            {
                final = (ulong) System.Math.Min(relativeEnd, len);
            }
        }

        if (k < final && final - k > ArrayOperations.MaxArrayLength)
        {
            Throw.RangeError(_realm, "Invalid array length");
        }

        var length = (uint) System.Math.Max(0, (long) final - (long) k);
        var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObject), length);
        if (thisObject is JsArray ai && a is JsArray a2)
        {
            a2.CopyValues(ai, (uint) k, 0, length);
        }
        else
        {
            // slower path
            var operations = ArrayOperations.For(a, forWrite: true);
            for (uint n = 0; k < final; k++, n++)
            {
                if (o.TryGetValue(k, out var kValue))
                {
                    operations.CreateDataPropertyOrThrow(n, kValue);
                }
            }
        }
        return a;
    }

    [JsFunction]
    private JsValue Shift(JsValue thisObject)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var len = o.GetLength();
        if (len == 0)
        {
            o.SetLength(0);
            return Undefined;
        }

        // Fast path: dense JsArray with default prototype — single Array.Copy left by 1
        // replaces O(n) per-element Set/HasProperty calls.
        if (o.Target is JsArray jsArr && jsArr._dense is { } dense && jsArr.CanUseFastAccess)
        {
            var first = (len <= (uint) dense.Length ? dense[0] : null) ?? Undefined;
            jsArr.TryMoveDenseRange(sourceIndex: 1, destIndex: 0, count: len - 1);
            jsArr.ClearDenseRange(len - 1, 1);
            jsArr.SetLength((ulong) (len - 1));
            return first;
        }

        var firstSlow = o.Get(0);
        for (uint k = 1; k < len; k++)
        {
            var to = k - 1;
            if (o.TryGetValue(k, out var fromVal))
            {
                o.Set(to, fromVal, throwOnError: false);
            }
            else
            {
                o.DeletePropertyOrThrow(to);
            }
        }

        o.DeletePropertyOrThrow(len - 1);
        o.SetLength(len - 1);

        return firstSlow;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.reverse
    /// </summary>
    [JsFunction]
    private JsValue Reverse(JsValue thisObject)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var len = o.GetLongLength();
        var middle = (ulong) System.Math.Floor(len / 2.0);
        uint lower = 0;
        while (lower != middle)
        {
            var upper = len - lower - 1;

            var lowerExists = o.HasProperty(lower);
            var lowerValue = lowerExists ? o.Get(lower) : null;

            var upperExists = o.HasProperty(upper);
            var upperValue = upperExists ? o.Get(upper) : null;

            if (lowerExists && upperExists)
            {
                o.Set(lower, upperValue!, throwOnError: true);
                o.Set(upper, lowerValue!, throwOnError: true);
            }

            if (!lowerExists && upperExists)
            {
                o.Set(lower, upperValue!, throwOnError: true);
                o.DeletePropertyOrThrow(upper);
            }

            if (lowerExists && !upperExists)
            {
                o.DeletePropertyOrThrow(lower);
                o.Set(upper, lowerValue!, throwOnError: true);
            }

            lower++;

            // Check constraints periodically to prevent long-running operations
            if (lower > 0 && lower % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }

        return o.Target;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.join
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Join(JsValue thisObject, JsCallArguments arguments)
    {
        var separator = arguments.At(0);
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = o.GetLength();

        var sep = TypeConverter.ToString(separator.IsUndefined() ? JsString.CommaString : separator);

        // as per the spec, this has to be called after ToString(separator)
        if (len == 0)
        {
            return JsString.Empty;
        }

        if (!_joinStack.TryEnter(thisObject))
        {
            return JsString.Empty;
        }

        static string StringFromJsValue(JsValue value)
        {
            return value.IsNullOrUndefined()
                ? ""
                : TypeConverter.ToString(value);
        }

        var s = StringFromJsValue(o.Get(0));
        if (len == 1)
        {
            _joinStack.Exit();
            return s;
        }

        using var sb = new ValueStringBuilder();
        sb.Append(s);
        for (uint k = 1; k < len; k++)
        {
            if (sep != "")
            {
                sb.Append(sep);
            }
            sb.Append(StringFromJsValue(o.Get(k)));
        }
        _joinStack.Exit();

        return sb.ToString();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.tolocalestring
    /// </summary>
    [JsFunction]
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        const string Separator = ",";

        var array = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var len = array.GetLength();
        if (len == 0)
        {
            return JsString.Empty;
        }

        if (!_joinStack.TryEnter(thisObject))
        {
            return JsString.Empty;
        }

        // Per ECMA-402, always pass locales and options to element's toLocaleString
        var locales = arguments.At(0);
        var options = arguments.At(1);
        var invokeArgs = new[] { locales, options };

        using var r = new ValueStringBuilder();
        for (uint k = 0; k < len; k++)
        {
            if (k > 0)
            {
                r.Append(Separator);
            }
            if (array.TryGetValue(k, out var nextElement) && !nextElement.IsNullOrUndefined())
            {
                var s = TypeConverter.ToString(Invoke(nextElement, "toLocaleString", invokeArgs));
                r.Append(s);
            }
        }
        _joinStack.Exit();

        return r.ToString();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.concat
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Concat(JsValue thisObject, JsCallArguments arguments)
    {
        // Fast path: thisObject and every spreadable arg are CanUseFastAccess JsArrays.
        // Pre-sum total length so the output is allocated once at exact size and each source
        // can be copied via Array.Copy instead of element-by-element CreateDataPropertyOrThrow.
        if (thisObject is JsArray { CanUseFastAccess: true } thisArr
            && !thisArr.HasOwnProperty(CommonProperties.Constructor)
            && TryConcatAllJsArrayFast(thisArr, arguments, out var fastResult))
        {
            return fastResult;
        }

        var o = TypeConverter.ToObject(_realm, thisObject);
        var items = new List<JsValue>(arguments.Length + 1) { o };
        items.AddRange(arguments);

        uint n = 0;
        var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObject), 0);
        var aOperations = ArrayOperations.For(a, forWrite: true);
        for (var i = 0; i < items.Count; i++)
        {
            var e = items[i];
            if (e is ObjectInstance { IsConcatSpreadable: true } oi)
            {
                if (e is JsArray eArray && a is JsArray a2)
                {
                    a2.CopyValues(eArray, 0, n, eArray.GetLength());
                    n += eArray.GetLength();
                }
                else
                {
                    var operations = ArrayOperations.For(oi, forWrite: false);
                    var len = operations.GetLongLength();

                    if (n + len > ArrayOperations.MaxArrayLikeLength)
                    {
                        Throw.TypeError(_realm, "Invalid array length");
                    }

                    for (uint k = 0; k < len; k++)
                    {
                        operations.TryGetValue(k, out var subElement);
                        aOperations.CreateDataPropertyOrThrow(n, subElement);
                        n++;
                    }
                }
            }
            else
            {
                aOperations.CreateDataPropertyOrThrow(n, e);
                n++;
            }
        }

        // this is not in the specs, but is necessary in case the last element of the last
        // array doesn't exist, and thus the length would not be incremented
        a.DefineOwnProperty(CommonProperties.Length, new PropertyDescriptor(n, PropertyFlag.OnlyWritable));

        return a;
    }

    private bool TryConcatAllJsArrayFast(JsArray thisArr, JsCallArguments arguments, out JsArray result)
    {
        result = null!;

        // CanUseFastAccess does not catch a custom @@isConcatSpreadable symbol, so we still
        // have to consult IsConcatSpreadable for each input.
        if (!thisArr.IsConcatSpreadable)
        {
            return false;
        }

        // Pre-classify each arg as spreadable-array / scalar / bail-to-slow-path so we don't
        // call IsConcatSpreadable twice (once during sizing, once during copy).
        var argInfo = arguments.Length == 0 ? null : new bool[arguments.Length];

        ulong totalLen = thisArr.GetLength();
        for (var i = 0; i < arguments.Length; i++)
        {
            var e = arguments[i];
            if (e is JsArray { CanUseFastAccess: true } ja && ja.IsConcatSpreadable)
            {
                argInfo![i] = true;
                totalLen += ja.GetLength();
            }
            else if (e is ObjectInstance)
            {
                // Either a non-fast array, a non-array object, or a custom-symbol object.
                // Defer to slow path which handles all spreadability cases correctly.
                return false;
            }
            else
            {
                totalLen++;
            }

            if (totalLen > ArrayOperations.MaxArrayLikeLength)
            {
                Throw.TypeError(_realm, "Invalid array length");
            }
        }

        if (totalLen > int.MaxValue)
        {
            return false;
        }

        var a = _realm.Intrinsics.Array.ArrayCreate(totalLen);
        var dest = a._dense;
        if (dest is null)
        {
            return false;
        }

        uint pos = 0;

        var thisLen = thisArr.GetLength();
        if (thisLen > 0)
        {
            var src = thisArr._dense!;
            var copyLen = (int) System.Math.Min((uint) src.Length, thisLen);
            System.Array.Copy(src, 0, dest, (int) pos, copyLen);
            pos = thisLen;
        }

        for (var i = 0; i < arguments.Length; i++)
        {
            var e = arguments[i];
            if (argInfo is not null && argInfo[i])
            {
                var ja = (JsArray) e;
                var len = ja.GetLength();
                if (len > 0)
                {
                    var src = ja._dense!;
                    var copyLen = (int) System.Math.Min((uint) src.Length, len);
                    System.Array.Copy(src, 0, dest, (int) pos, copyLen);
                    pos += len;
                }
            }
            else
            {
                dest[pos++] = e;
            }
        }

        result = a;
        return true;
    }

    [JsFunction]
    internal JsValue ToString(JsValue thisObject, JsCallArguments arguments)
    {
        var array = TypeConverter.ToObject(_realm, thisObject);

        JsCallDelegate func;
        if (array.Get("join") is ICallable joinFunc)
        {
            func = joinFunc.Call;
        }
        else
        {
            var prototype = _realm.Intrinsics.Object.PrototypeObject;
            func = (thisArg, _) => prototype.ToObjectString(thisArg);
        }

        return func(array, Arguments.Empty);
    }

    [JsFunction]
    private JsValue ToReversed(JsValue thisObject)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);

        var len = o.GetLongLength();

        if (len == 0)
        {
            return new JsArray(_engine);
        }

        var a = CreateBackingArray(len);
        ulong k = 0;
        while (k < len)
        {
            var from = len - k - 1;
            a[k++] = o.Get(from);

            // Check constraints periodically to prevent long-running operations
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }
        }
        return new JsArray(_engine, a);
    }

    [JsFunction(Length = 1)]
    private JsValue ToSorted(JsValue thisObject, JsCallArguments arguments)
    {
        var o = ArrayOperations.For(_realm, thisObject, forWrite: false);
        var compareFn = GetCompareFunction(arguments.At(0));

        var len = o.GetLongLength();
        ValidateArrayLength(len);

        if (len == 0)
        {
            return new JsArray(_engine);
        }

        var array = o.GetAll(skipHoles: true);

        array = SortArray(array, compareFn);

        return new JsArray(_engine, array);
    }

    [JsFunction(Length = 2)]
    private JsValue ToSpliced(JsValue thisObject, JsCallArguments arguments)
    {
        var start = arguments.At(0);
        var deleteCount = arguments.At(1);

        var o = ArrayOperations.For(_realm, TypeConverter.ToObject(_realm, thisObject), forWrite: false);
        var len = o.GetLongLength();
        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        ulong actualStart;
        if (double.IsNegativeInfinity(relativeStart))
        {
            actualStart = 0;
        }
        else if (relativeStart < 0)
        {
            actualStart = (ulong) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            actualStart = (ulong) System.Math.Min(relativeStart, len);
        }

        var items = System.Array.Empty<JsValue>();
        ulong insertCount;
        ulong actualDeleteCount;
        if (arguments.Length == 0)
        {
            insertCount = 0;
            actualDeleteCount = 0;
        }
        else if (arguments.Length == 1)
        {
            insertCount = 0;
            actualDeleteCount = len - actualStart;
        }
        else
        {
            insertCount = (ulong) (arguments.Length - 2);
            var dc = TypeConverter.ToIntegerOrInfinity(deleteCount);
            actualDeleteCount = (ulong) System.Math.Min(System.Math.Max(dc, 0), len - actualStart);

            items = [];
            if (arguments.Length > 2)
            {
                items = new JsValue[arguments.Length - 2];
                System.Array.Copy(arguments, 2, items, 0, items.Length);
            }
        }

        var newLen = len + insertCount - actualDeleteCount;
        if (newLen > ArrayOperations.MaxArrayLikeLength)
        {
            Throw.TypeError(_realm, "Invalid array length");
        }

        ValidateArrayLength(newLen);

        var r = actualStart + actualDeleteCount;
        var a = new JsArray(_engine, (uint) newLen);
        uint i = 0;

        while (i < actualStart)
        {
            a.SetIndexValue(i, o.Get(i), updateLength: false);
            i++;
        }
        a.SetLength((uint) actualStart);

        foreach (var item in items)
        {
            a.SetIndexValue(i++, item, updateLength: false);
        }

        while (i < newLen)
        {
            var fromValue = o.Get(r);
            a.SetIndexValue(i, fromValue, updateLength: false);

            i++;
            r++;
        }

        a.SetLength(i);
        return a;
    }

    private JsValue[] SortArray(IEnumerable<JsValue> array, ICallable? compareFn)
    {
        var comparer = ArrayComparer.WithFunction(_engine, compareFn);

        try
        {
            return array.OrderBy(x => x, comparer).ToArray();
        }
        catch (InvalidOperationException e)
        {
            throw e.InnerException ?? e;
        }
    }

    private ICallable? GetCompareFunction(JsValue compareArg)
    {
        ICallable? compareFn = null;
        if (!compareArg.IsUndefined())
        {
            if (compareArg is not ICallable callable)
            {
                Throw.TypeError(_realm, "The comparison function must be either a function or undefined");
                return null;
            }
            compareFn = callable;
        }

        return compareFn;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.reduceright
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue ReduceRight(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var initialValue = arguments.At(1);

        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var len = o.GetLongLength();

        var callable = GetCallable(callbackfn);

        if (len == 0 && arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Reduce of empty array with no initial value");
        }

        long k = (long) (len - 1);
        JsValue accumulator = Undefined;
        if (arguments.Length > 1)
        {
            accumulator = initialValue;
        }
        else
        {
            var kPresent = false;
            while (kPresent == false && k >= 0)
            {
                if ((kPresent = o.TryGetValue((ulong) k, out var temp)))
                {
                    accumulator = temp;
                }

                k--;
            }

            if (kPresent == false)
            {
                Throw.TypeError(_realm, "Reduce of empty array with no initial value");
            }
        }

        var jsValues = new JsValue[4];
        jsValues[3] = o.Target;
        for (; k >= 0; k--)
        {
            if (o.TryGetValue((ulong) k, out var kvalue))
            {
                jsValues[0] = accumulator;
                jsValues[1] = kvalue;
                jsValues[2] = k;
                accumulator = callable.Call(Undefined, jsValues);
            }
        }

        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.prototype.push
    /// </summary>
    [JsFunction(Length = 1)]
    public JsValue Push(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is JsArray { CanUseFastAccess: true } arrayInstance)
        {
            return arrayInstance.Push(arguments);
        }

        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        var n = o.GetLongLength();

        if (n + (ulong) arguments.Length > ArrayOperations.MaxArrayLikeLength)
        {
            Throw.TypeError(_realm, "Invalid array length");
        }

        foreach (var a in arguments)
        {
            o.Set(n, a, true);
            n++;
        }

        o.SetLength(n);
        return n;
    }

    [JsFunction]
    public JsValue Pop(JsValue thisObject)
    {
        if (thisObject is JsArray { CanUseFastAccess: true } array)
        {
            return array.Pop();
        }

        var o = ArrayOperations.For(_realm, thisObject, forWrite: true);
        ulong len = o.GetLongLength();
        if (len == 0)
        {
            o.SetLength(0);
            return Undefined;
        }

        len -= 1;
        JsValue element = o.Get(len);
        o.DeletePropertyOrThrow(len);
        o.SetLength(len);
        return element;
    }

    private JsValue[] CreateBackingArray(ulong length)
    {
        ValidateArrayLength(length);
        return new JsValue[length];
    }

    private void ValidateArrayLength(ulong length)
    {
        if (length > ArrayOperations.MaxArrayLength)
        {
            Throw.RangeError(_engine.Realm, "Invalid array length " + length);
        }
    }

    internal sealed class ArrayComparer : IComparer<JsValue>
    {
        /// <summary>
        /// Default instance without any compare function.
        /// </summary>
        public static readonly ArrayComparer Default = new(null, null);

        public static ArrayComparer WithFunction(Engine engine, ICallable? compare)
        {
            return compare is null ? Default : new ArrayComparer(engine, compare);
        }

        private readonly Engine? _engine;
        private readonly ICallable? _compare;
        private readonly JsValue[] _comparableArray = new JsValue[2];

        private ArrayComparer(Engine? engine, ICallable? compare)
        {
            _engine = engine;
            _compare = compare;
        }

        public int Compare(JsValue? x, JsValue? y)
        {
            var xIsNull = x is null;
            var yIsNull = y is null;

            if (xIsNull)
            {
                if (yIsNull)
                {
                    return 0;
                }

                return 1;
            }
            else
            {
                if (yIsNull)
                {
                    return -1;
                }
            }

            var xUndefined = x!.IsUndefined();
            var yUndefined = y!.IsUndefined();
            if (xUndefined && yUndefined)
            {
                return 0;
            }

            if (xUndefined)
            {
                return 1;
            }

            if (yUndefined)
            {
                return -1;
            }

            if (_compare != null)
            {
                _engine!.RunBeforeExecuteStatementChecks(null);

                _comparableArray[0] = x!;
                _comparableArray[1] = y!;

                var s = TypeConverter.ToNumber(_compare.Call(Undefined, _comparableArray));
                if (s < 0)
                {
                    return -1;
                }

                if (s > 0)
                {
                    return 1;
                }

                return 0;
            }

            var xString = TypeConverter.ToString(x!);
            var yString = TypeConverter.ToString(y!);

            return string.CompareOrdinal(xString, yString);
        }
    }
}
