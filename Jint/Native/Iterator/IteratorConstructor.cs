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
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["concat"] = new(new PropertyDescriptor(new ClrFunction(Engine, "concat", Concat, 0, LengthFlags), PropertyFlags)),
            ["from"] = new(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags)),
            ["zip"] = new(new PropertyDescriptor(new ClrFunction(Engine, "zip", Zip, 1, LengthFlags), PropertyFlags)),
            ["zipKeyed"] = new(new PropertyDescriptor(new ClrFunction(Engine, "zipKeyed", ZipKeyed, 1, LengthFlags), PropertyFlags)),
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

    /// <summary>
    /// https://tc39.es/proposal-joint-iteration/#sec-iterator.zip
    /// </summary>
    private JsValue Zip(JsValue thisObject, JsValue[] arguments)
    {
        var iterables = arguments.At(0);
        var options = arguments.At(1);

        // 1. If iterables is not an Object, throw a TypeError exception.
        if (iterables is not ObjectInstance iterablesObj)
        {
            Throw.TypeError(_realm, "Iterator.zip requires an object");
            return Undefined;
        }

        // 2. Set options to ? GetOptionsObject(options).
        var optionsObj = GetOptionsObject(options);

        // 3. Let mode be ? Get(options, "mode").
        var modeValue = optionsObj?.Get(CommonProperties.Mode) ?? Undefined;

        // 4. If mode is undefined, set mode to "shortest".
        ZipMode mode;
        if (modeValue.IsUndefined())
        {
            mode = ZipMode.Shortest;
        }
        else if (modeValue.IsString())
        {
            var modeStr = modeValue.ToString();
            mode = modeStr switch
            {
                "shortest" => ZipMode.Shortest,
                "longest" => ZipMode.Longest,
                "strict" => ZipMode.Strict,
                _ => throw new JavaScriptException(
                    _engine.Realm.Intrinsics.TypeError,
                    $"Invalid mode: {modeStr}")
            };
        }
        else
        {
            Throw.TypeError(_realm, "mode must be a string");
            return Undefined;
        }

        // 6-7. If mode is "longest", get padding option (done later, after collecting iters)
        JsValue[]? paddingValues = null;
        IteratorInstance.ObjectIterator? paddingIterator = null;
        ObjectInstance? paddingObj = null;
        if (mode == ZipMode.Longest)
        {
            var paddingOption = optionsObj?.Get(CommonProperties.Padding) ?? Undefined;
            if (!paddingOption.IsUndefined())
            {
                if (paddingOption is not ObjectInstance pObj)
                {
                    Throw.TypeError(_realm, "padding must be an object");
                    return Undefined;
                }
                paddingObj = pObj;
            }
        }

        // 8-14. Create iterators from the iterables array
        // Per spec: iterate and call GetIteratorFlattenable for each element immediately
        var iters = new List<IteratorInstance.ObjectIterator>();
        var iterablesIterator = iterablesObj.GetIterator(_realm);
        var inputIterClosed = false;

        try
        {
            while (iterablesIterator.TryIteratorStep(out var result))
            {
                var value = result.Get(CommonProperties.Value);

                // GetIteratorFlattenable handles non-objects by throwing TypeError
                try
                {
                    var iterator = GetIteratorFlattenable(value, StringHandlingType.RejectStrings, out _);
                    iters.Add(iterator);
                }
                catch
                {
                    // IfAbruptCloseIterators: close (inputIter + iters) in reverse
                    // Per spec, reverse order of [inputIter, ...iters] = iters in reverse, then inputIter
                    inputIterClosed = true;
                    CloseIteratorsReverse(iters);
                    try { iterablesIterator.Close(CompletionType.Throw); } catch { /* ignore */ }
                    throw;
                }
            }
        }
        catch
        {
            // If iterablesIterator step throws, close collected iterators
            if (!inputIterClosed)
            {
                CloseIteratorsReverse(iters);
            }
            throw;
        }

        // Get padding iterator now that we have all iters collected
        // Per spec, if GetIterator throws, close all collected iters
        if (paddingObj != null)
        {
            try
            {
                var method = paddingObj.GetMethod(GlobalSymbolRegistry.Iterator);
                if (method is null)
                {
                    CloseIteratorsReverse(iters);
                    Throw.TypeError(_realm, "padding is not iterable");
                    return Undefined;
                }
                var iterResult = method.Call(paddingObj);
                if (iterResult is not ObjectInstance iterObj)
                {
                    CloseIteratorsReverse(iters);
                    Throw.TypeError(_realm, "Iterator result is not an object");
                    return Undefined;
                }
                paddingIterator = new IteratorInstance.ObjectIterator(iterObj);
            }
            catch
            {
                CloseIteratorsReverse(iters);
                throw;
            }
        }

        // Get padding values if we have a padding iterator
        // Per spec: read exactly iters.Count values, then close if not exhausted
        if (paddingIterator != null)
        {
            paddingValues = new JsValue[iters.Count];
            var usingIterator = true;

            try
            {
                for (var i = 0; i < iters.Count; i++)
                {
                    if (usingIterator)
                    {
                        if (paddingIterator.TryIteratorStep(out var paddingResult))
                        {
                            paddingValues[i] = paddingResult.Get(CommonProperties.Value);
                        }
                        else
                        {
                            // Padding iterator exhausted
                            usingIterator = false;
                            paddingValues[i] = Undefined;
                        }
                    }
                    else
                    {
                        paddingValues[i] = Undefined;
                    }
                }
            }
            catch
            {
                // If IteratorStepValue throws, close all collected iters
                CloseIteratorsReverse(iters);
                throw;
            }

            // If padding iterator wasn't exhausted, close it
            if (usingIterator)
            {
                try
                {
                    paddingIterator.Close(CompletionType.Normal);
                }
                catch
                {
                    // If closing padding iterator throws, close all collected iters
                    CloseIteratorsReverse(iters);
                    throw;
                }
            }
        }

        // 15. Let finishResults be a new Abstract Closure that creates an array
        JsValue FinishResults(JsValue[] values)
        {
            // Create a proper array with correct property descriptors
            var array = new JsArray(_engine, (uint) values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                array.CreateDataPropertyOrThrow((uint) i, values[i]);
            }
            return array;
        }

        // 16. Return IteratorZip(iters, mode, padding, finishResults).
        return new ZipIterator(_engine, iters, mode, paddingIterator, paddingValues, FinishResults);
    }

    /// <summary>
    /// https://tc39.es/proposal-joint-iteration/#sec-iterator.zipkeyed
    /// </summary>
    private JsValue ZipKeyed(JsValue thisObject, JsValue[] arguments)
    {
        var iterables = arguments.At(0);
        var options = arguments.At(1);

        // 1. If iterables is not an Object, throw a TypeError exception.
        if (iterables is not ObjectInstance iterablesObj)
        {
            Throw.TypeError(_realm, "Iterator.zipKeyed requires an object");
            return Undefined;
        }

        // 2. Set options to ? GetOptionsObject(options).
        var optionsObj = GetOptionsObject(options);

        // 3. Let mode be ? Get(options, "mode").
        var modeValue = optionsObj?.Get(CommonProperties.Mode) ?? Undefined;

        // 4. If mode is undefined, set mode to "shortest".
        ZipMode mode;
        if (modeValue.IsUndefined())
        {
            mode = ZipMode.Shortest;
        }
        else if (modeValue.IsString())
        {
            var modeStr = modeValue.ToString();
            mode = modeStr switch
            {
                "shortest" => ZipMode.Shortest,
                "longest" => ZipMode.Longest,
                "strict" => ZipMode.Strict,
                _ => throw new JavaScriptException(
                    _engine.Realm.Intrinsics.TypeError,
                    $"Invalid mode: {modeStr}")
            };
        }
        else
        {
            Throw.TypeError(_realm, "mode must be a string");
            return Undefined;
        }

        // 6-7. If mode is "longest", get padding option
        JsValue[]? paddingValues = null;
        IteratorInstance.ObjectIterator? paddingIterator = null;
        ObjectInstance? paddingObj = null;
        if (mode == ZipMode.Longest)
        {
            var paddingOption = optionsObj?.Get(CommonProperties.Padding) ?? Undefined;
            if (!paddingOption.IsUndefined())
            {
                if (paddingOption is not ObjectInstance pObj)
                {
                    Throw.TypeError(_realm, "padding must be an object");
                    return Undefined;
                }
                paddingObj = pObj;
            }
        }

        // Get own enumerable string-keyed properties of iterables
        // Per spec, for each key: GetOwnProperty, Get, GetIteratorFlattenable in order
        // If any step throws, close already-collected iterators
        var ownKeys = iterablesObj.GetOwnPropertyKeys();
        var iters = new List<IteratorInstance.ObjectIterator>();
        var keysList = new List<JsValue>();

        foreach (var key in ownKeys)
        {
            PropertyDescriptor desc;
            try
            {
                desc = iterablesObj.GetOwnProperty(key);
            }
            catch
            {
                CloseIteratorsReverse(iters);
                throw;
            }

            if (desc == PropertyDescriptor.Undefined || !desc.Enumerable)
            {
                continue;
            }

            JsValue value;
            try
            {
                value = iterablesObj.Get(key);
            }
            catch
            {
                CloseIteratorsReverse(iters);
                throw;
            }

            // Per spec, skip undefined values - they are not included in results
            if (value.IsUndefined())
            {
                continue;
            }

            try
            {
                var iterator = GetIteratorFlattenable(value, StringHandlingType.RejectStrings, out _);
                iters.Add(iterator);
                keysList.Add(key);
            }
            catch
            {
                CloseIteratorsReverse(iters);
                throw;
            }
        }

        // Get padding values from the padding object if present
        // Per spec, only call Get (not HasProperty)
        // If Get throws, close all collected iters
        if (paddingObj != null)
        {
            paddingValues = new JsValue[keysList.Count];
            try
            {
                for (var i = 0; i < keysList.Count; i++)
                {
                    var key = keysList[i];
                    paddingValues[i] = paddingObj.Get(key);
                }
            }
            catch
            {
                CloseIteratorsReverse(iters);
                throw;
            }
        }

        // Create finishResults closure that builds an object
        var capturedKeys = keysList.ToArray();
        JsValue FinishResults(JsValue[] values)
        {
            // Create null-prototype object
            var obj = OrdinaryObjectCreate(_engine, null);
            for (var i = 0; i < capturedKeys.Length; i++)
            {
                obj.CreateDataPropertyOrThrow(capturedKeys[i], values[i]);
            }
            return obj;
        }

        return new ZipIterator(_engine, iters, mode, paddingIterator, paddingValues, FinishResults);
    }

    /// <summary>
    /// https://tc39.es/proposal-joint-iteration/#sec-getoptionsobject
    /// </summary>
    private ObjectInstance? GetOptionsObject(JsValue options)
    {
        // 1. If options is undefined, return null (we'll treat undefined as empty options).
        if (options.IsUndefined())
        {
            return null;
        }

        // 2. If options is an Object, return options.
        if (options is ObjectInstance optionsObj)
        {
            return optionsObj;
        }

        // 3. Throw a TypeError exception.
        Throw.TypeError(_realm, "options must be an object or undefined");
        return null;
    }

    private static void CloseIteratorsReverse(List<IteratorInstance.ObjectIterator> iters)
    {
        for (var i = iters.Count - 1; i >= 0; i--)
        {
            try
            {
                iters[i].Close(CompletionType.Normal);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
