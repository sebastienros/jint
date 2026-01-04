#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using System.Collections;
using Jint.Native.Function;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

public sealed class ArrayConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Array");

    internal ArrayConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ArrayPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public ArrayPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["from"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
            ["fromAsync"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "fromAsync", FromAsync, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
            ["isArray"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "isArray", IsArray, 1), PropertyFlag.NonEnumerable)),
            ["of"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "of", Of, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable))
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(Engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable),
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsCallArguments arguments)
    {
        var items = arguments.At(0);
        var mapFunction = arguments.At(1);
        var callable = !mapFunction.IsUndefined() ? GetCallable(mapFunction) : null;
        var thisArg = arguments.At(2);

        if (items.IsNullOrUndefined())
        {
            Throw.TypeError(_realm, "Cannot convert undefined or null to object");
        }

        var usingIterator = GetMethod(_realm, items, GlobalSymbolRegistry.Iterator);
        if (usingIterator is not null)
        {
            ObjectInstance instance;
            if (!ReferenceEquals(this, thisObject) && thisObject is IConstructor constructor)
            {
                instance = constructor.Construct([], thisObject);
            }
            else
            {
                instance = ArrayCreate(0);
            }

            var iterator = items.GetIterator(_realm, method: usingIterator);
            var protocol = new ArrayProtocol(_engine, thisArg, instance, iterator, callable);
            protocol.Execute();
            return instance;
        }

        if (items is IObjectWrapper { Target: IEnumerable enumerable })
        {
            return ConstructArrayFromIEnumerable(enumerable);
        }

        var source = ArrayOperations.For(_realm, items, forWrite: false);
        return ConstructArrayFromArrayLike(thisObject, source, callable, thisArg);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.fromasync
    /// </summary>
    private JsValue FromAsync(JsValue thisObject, JsCallArguments arguments)
    {
        var asyncItems = arguments.At(0);
        var mapfn = arguments.At(1);
        var thisArg = arguments.At(2);

        // 1. Let C be the this value.
        var c = thisObject;

        // 2. Let promiseCapability be ! NewPromiseCapability(%Promise%).
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        // 3. Let fromAsyncClosure be a new Abstract Closure with no parameters that captures C, mapfn, and thisArg
        //    and performs the following steps when called:
        // 4. Perform ! Call(fromAsyncClosure, undefined).
        // The closure is called synchronously per spec (step 4)
        FromAsyncClosure(c, asyncItems, mapfn, thisArg, promiseCapability);

        // 5. Return promiseCapability.[[Promise]].
        return promiseCapability.PromiseInstance;
    }

    private void FromAsyncClosure(JsValue c, JsValue asyncItems, JsValue mapfn, JsValue thisArg, PromiseCapability promiseCapability)
    {
        try
        {
            FromAsyncClosureImpl(c, asyncItems, mapfn, thisArg, promiseCapability);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private void FromAsyncClosureImpl(JsValue c, JsValue asyncItems, JsValue mapfn, JsValue thisArg, PromiseCapability promiseCapability)
    {
        // a. If mapfn is undefined, let mapping be false.
        // b. Else,
        //    i. If IsCallable(mapfn) is false, throw a TypeError exception.
        //    ii. Let mapping be true.
        ICallable? callable = null;
        if (!mapfn.IsUndefined())
        {
            if (mapfn is not ICallable mapCallable)
            {
                Throw.TypeError(_realm, $"{mapfn} is not a function");
                return;
            }
            callable = mapCallable;
        }

        // c. Let usingAsyncIterator be ? GetMethod(asyncItems, @@asyncIterator).
        if (asyncItems.IsNullOrUndefined())
        {
            Throw.TypeError(_realm, "Cannot convert undefined or null to object");
            return;
        }

        var usingAsyncIterator = GetMethod(_realm, asyncItems, GlobalSymbolRegistry.AsyncIterator);

        // d. If usingAsyncIterator is undefined, then
        //    i. Let usingSyncIterator be ? GetMethod(asyncItems, @@iterator).
        ICallable? usingSyncIterator = null;
        if (usingAsyncIterator is null)
        {
            usingSyncIterator = GetMethod(_realm, asyncItems, GlobalSymbolRegistry.Iterator);
        }

        // e. If usingAsyncIterator is not undefined or usingSyncIterator is not undefined, then
        if (usingAsyncIterator is not null || usingSyncIterator is not null)
        {
            FromAsyncWithIterator(c, asyncItems, callable, thisArg, promiseCapability, usingAsyncIterator, usingSyncIterator);
        }
        else
        {
            // f. Else,
            //    i. NOTE: asyncItems is neither an AsyncIterable nor an Iterable so assume it is an array-like object.
            FromAsyncWithArrayLike(c, asyncItems, callable, thisArg, promiseCapability);
        }
    }

    private void FromAsyncWithIterator(
        JsValue c,
        JsValue asyncItems,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ICallable? usingAsyncIterator,
        ICallable? usingSyncIterator)
    {
        // ii. Let iteratorRecord be ? GetIterator(asyncItems, async).
        // We'll handle both async and sync iterators
        ObjectInstance instance;
        if (!ReferenceEquals(this, c) && c is IConstructor constructor)
        {
            instance = constructor.Construct([], c);
        }
        else
        {
            instance = ArrayCreate(0);
        }

        var target = ArrayOperations.For(instance, forWrite: true);

        if (usingAsyncIterator is not null)
        {
            // Use async iterator directly - GetIterator with Async hint handles the wrapping
            var iterator = asyncItems.GetIterator(_realm, GeneratorKind.Async, usingAsyncIterator);
            FromAsyncIteratorLoop(iterator.Instance, target, callable, thisArg, promiseCapability, 0);
        }
        else if (usingSyncIterator is not null)
        {
            // Wrap sync iterator in async wrapper
            var syncIterator = asyncItems.GetIterator(_realm, GeneratorKind.Sync, usingSyncIterator);
            var asyncIterator = new AsyncFromSyncIterator(_engine, syncIterator);
            FromAsyncIteratorLoop(asyncIterator, target, callable, thisArg, promiseCapability, 0);
        }
    }

    private void FromAsyncIteratorLoop(
        ObjectInstance iterator,
        ArrayOperations target,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ulong k)
    {
        System.Console.WriteLine($"[DEBUG] FromAsyncIteratorLoop k={k}, iterator type={iterator.GetType().Name}");
        try
        {
            // Get next method and call it
            var nextMethod = iterator.Get(CommonProperties.Next);
            if (nextMethod is not ICallable nextCallable)
            {
                promiseCapability.Reject.Call(Undefined, _realm.Intrinsics.TypeError.Construct("Iterator next is not a function"));
                return;
            }

            var next = nextCallable.Call(iterator, Arguments.Empty);

            // The result should be a promise (or wrap it in one)
            var promiseResolve = _realm.Intrinsics.Promise.PromiseResolve(next);

            var capturedK = k;
            var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                var iterResult = args.At(0);
                if (iterResult is not ObjectInstance iterResultObj)
                {
                    promiseCapability.Reject.Call(Undefined, _realm.Intrinsics.TypeError.Construct("Iterator result is not an object"));
                    return Undefined;
                }

                var done = TypeConverter.ToBoolean(iterResultObj.Get(CommonProperties.Done));
                var value = iterResultObj.Get(CommonProperties.Value);
                System.Console.WriteLine($"[DEBUG] onFulfilled k={capturedK}, done={done}, value={value}");
                if (done)
                {
                    // Iteration complete
                    target.SetLength(capturedK);
                    promiseCapability.Resolve.Call(Undefined, target.Target);
                    return Undefined;
                }

                ProcessAsyncIteratorValue(value, target, callable, thisArg, promiseCapability, capturedK, () =>
                {
                    // Queue next iteration to prevent stack overflow
                    System.Console.WriteLine($"[DEBUG] continueLoop called, will queue k={capturedK + 1}");
                    _engine.AddToEventLoop(() => FromAsyncIteratorLoop(iterator, target, callable, thisArg, promiseCapability, capturedK + 1));
                });
                return Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(_engine, "", (_, args) =>
            {
                promiseCapability.Reject.Call(Undefined, args);
                return Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(_engine, (JsPromise) promiseResolve, onFulfilled, onRejected, null!);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private void ProcessAsyncIteratorValue(
        JsValue value,
        ArrayOperations target,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ulong k,
        Action continueLoop)
    {
        // Await the value
        var valuePromise = _realm.Intrinsics.Promise.PromiseResolve(value);

        var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
        {
            var resolvedValue = args.At(0);
            ProcessMappedValue(resolvedValue, target, callable, thisArg, promiseCapability, k, continueLoop);
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, args) =>
        {
            promiseCapability.Reject.Call(Undefined, args);
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, (JsPromise) valuePromise, onFulfilled, onRejected, null!);
    }

    private void ProcessMappedValue(
        JsValue value,
        ArrayOperations target,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ulong k,
        Action continueLoop)
    {
        try
        {
            if (callable is not null)
            {
                // Apply mapfn
                var mappedValue = callable.Call(thisArg, value, k);

                // Await the mapped value
                var mappedPromise = _realm.Intrinsics.Promise.PromiseResolve(mappedValue);

                var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
                {
                    var resolvedMapped = args.At(0);
                    target.CreateDataPropertyOrThrow(k, resolvedMapped);
                    // Queue continuation to prevent stack overflow
                    _engine.AddToEventLoop(continueLoop);
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                var onRejected = new ClrFunction(_engine, "", (_, args) =>
                {
                    promiseCapability.Reject.Call(Undefined, args);
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(_engine, (JsPromise) mappedPromise, onFulfilled, onRejected, null!);
            }
            else
            {
                target.CreateDataPropertyOrThrow(k, value);
                // Queue continuation to prevent stack overflow
                _engine.AddToEventLoop(continueLoop);
            }
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private void FromAsyncWithArrayLike(
        JsValue c,
        JsValue asyncItems,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability)
    {
        try
        {
            // ii. Let arrayLike be ! ToObject(asyncItems).
            var arrayLike = TypeConverter.ToObject(_realm, asyncItems);

            // iii. Let len be ? LengthOfArrayLike(arrayLike).
            var len = arrayLike.GetLength();

            // iv. If IsConstructor(C) is true, then
            //     1. Let A be ? Construct(C, « 𝔽(len) »).
            // v. Else,
            //     1. Let A be ? ArrayCreate(len).
            ObjectInstance a;
            if (!ReferenceEquals(c, this) && c is IConstructor constructor)
            {
                a = constructor.Construct([len], c);
            }
            else
            {
                a = ArrayCreate(len);
            }

            var target = ArrayOperations.For(a, forWrite: true);
            FromAsyncArrayLikeLoop(arrayLike, target, callable, thisArg, promiseCapability, 0, len);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private void FromAsyncArrayLikeLoop(
        ObjectInstance arrayLike,
        ArrayOperations target,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ulong k,
        ulong len)
    {
        if (k >= len)
        {
            // Done
            target.SetLength(len);
            promiseCapability.Resolve.Call(Undefined, target.Target);
            return;
        }

        try
        {
            // Get the value at k
            var pk = k;
            var kValue = arrayLike.Get(pk);

            // Await the value
            var valuePromise = _realm.Intrinsics.Promise.PromiseResolve(kValue);

            var capturedK = k;
            var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
            {
                var resolvedValue = args.At(0);
                ProcessArrayLikeMappedValue(arrayLike, resolvedValue, target, callable, thisArg, promiseCapability, capturedK, len);
                return Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(_engine, "", (_, args) =>
            {
                promiseCapability.Reject.Call(Undefined, args);
                return Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(_engine, (JsPromise) valuePromise, onFulfilled, onRejected, null!);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private void ProcessArrayLikeMappedValue(
        ObjectInstance arrayLike,
        JsValue value,
        ArrayOperations target,
        ICallable? callable,
        JsValue thisArg,
        PromiseCapability promiseCapability,
        ulong k,
        ulong len)
    {
        try
        {
            if (callable is not null)
            {
                // Apply mapfn
                var mappedValue = callable.Call(thisArg, value, k);

                // Await the mapped value
                var mappedPromise = _realm.Intrinsics.Promise.PromiseResolve(mappedValue);

                var capturedK = k;
                var onFulfilled = new ClrFunction(_engine, "", (_, args) =>
                {
                    var resolvedMapped = args.At(0);
                    target.CreateDataPropertyOrThrow(capturedK, resolvedMapped);
                    // Queue next iteration to prevent stack overflow
                    _engine.AddToEventLoop(() => FromAsyncArrayLikeLoop(arrayLike, target, callable, thisArg, promiseCapability, capturedK + 1, len));
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                var onRejected = new ClrFunction(_engine, "", (_, args) =>
                {
                    promiseCapability.Reject.Call(Undefined, args);
                    return Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(_engine, (JsPromise) mappedPromise, onFulfilled, onRejected, null!);
            }
            else
            {
                target.CreateDataPropertyOrThrow(k, value);
                // Queue next iteration to prevent stack overflow
                _engine.AddToEventLoop(() => FromAsyncArrayLikeLoop(arrayLike, target, callable, thisArg, promiseCapability, k + 1, len));
            }
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }
    }

    private ObjectInstance ConstructArrayFromArrayLike(
        JsValue thisObj,
        ArrayOperations source,
        ICallable? callable,
        JsValue thisArg)
    {
        var length = source.GetLength();

        ObjectInstance a;
        if (!ReferenceEquals(thisObj, this) && thisObj is IConstructor constructor)
        {
            var argumentsList = new JsValue[] { length };
            a = Construct(constructor, argumentsList);
        }
        else
        {
            a = ArrayCreate(length);
        }

        var args = callable is not null
            ? _engine._jsValueArrayPool.RentArray(2)
            : null;

        var target = ArrayOperations.For(a, forWrite: true);
        uint n = 0;
        for (uint i = 0; i < length; i++)
        {
            var value = source.Get(i);
            if (callable is not null)
            {
                args![0] = value;
                args[1] = i;
                value = callable.Call(thisArg, args);

                // function can alter data
                length = source.GetLength();
            }

            target.CreateDataPropertyOrThrow(i, value);
            n++;
        }

        if (callable is not null)
        {
            _engine._jsValueArrayPool.ReturnArray(args!);
        }

        target.SetLength(length);
        return a;
    }

    private sealed class ArrayProtocol : IteratorProtocol
    {
        private readonly JsValue _thisArg;
        private readonly ArrayOperations _instance;
        private readonly ICallable? _callable;
        private long _index = -1;

        public ArrayProtocol(
            Engine engine,
            JsValue thisArg,
            ObjectInstance instance,
            IteratorInstance iterator,
            ICallable? callable) : base(engine, iterator, 2)
        {
            _thisArg = thisArg;
            _instance = ArrayOperations.For(instance, forWrite: true);
            _callable = callable;
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            _index++;
            JsValue jsValue;
            if (_callable is not null)
            {
                arguments[0] = currentValue;
                arguments[1] = _index;
                jsValue = _callable.Call(_thisArg, arguments);
            }
            else
            {
                jsValue = currentValue;
            }

            _instance.CreateDataPropertyOrThrow((ulong) _index, jsValue);
        }

        protected override void IterationEnd()
        {
            _instance.SetLength((ulong) (_index + 1));
        }
    }

    private JsValue Of(JsValue thisObject, JsCallArguments arguments)
    {
        var len = arguments.Length;
        ObjectInstance a;
        if (thisObject.IsConstructor)
        {
            a = ((IConstructor) thisObject).Construct([len], thisObject);
        }
        else
        {
            a = _realm.Intrinsics.Array.Construct(len);
        }

        if (a is JsArray ai)
        {
            // faster for real arrays
            for (uint k = 0; k < arguments.Length; k++)
            {
                var kValue = arguments[(int) k];
                ai.SetIndexValue(k, kValue, updateLength: k == arguments.Length - 1);
            }
        }
        else
        {
            // slower version
            for (uint k = 0; k < arguments.Length; k++)
            {
                var kValue = arguments[(int) k];
                var key = JsString.Create(k);
                a.CreateDataPropertyOrThrow(key, kValue);
            }

            a.Set(CommonProperties.Length, len, true);
        }

        return a;
    }

    private static JsValue Species(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    private static JsValue IsArray(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0);

        return IsArray(o);
    }

    private static JsValue IsArray(JsValue o)
    {
        if (!(o is ObjectInstance oi))
        {
            return JsBoolean.False;
        }

        return oi.IsArray();
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public JsArray Construct(JsCallArguments arguments)
    {
        return (JsArray) Construct(arguments, this);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var proto = _realm.Intrinsics.Function.GetPrototypeFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Array.PrototypeObject);

        // check if we can figure out good size
        var capacity = arguments.Length > 0 ? (ulong) arguments.Length : 0;
        if (arguments.Length == 1 && arguments[0].IsNumber())
        {
            var number = ((JsNumber) arguments[0])._value;
            ValidateLength(number);
            capacity = (ulong) number;
        }
        return Construct(arguments, capacity, proto);
    }

    public JsArray Construct(int capacity)
    {
        return Construct([], (uint) capacity);
    }

    public JsArray Construct(uint capacity)
    {
        return Construct([], capacity);
    }

    public JsArray Construct(JsCallArguments arguments, uint capacity)
    {
        return Construct(arguments, capacity, PrototypeObject);
    }

    private JsArray Construct(JsCallArguments arguments, ulong capacity, ObjectInstance prototypeObject)
    {
        JsArray instance;
        if (arguments.Length == 1)
        {
            switch (arguments[0])
            {
                case JsNumber number:
                    ValidateLength(number._value);
                    instance = ArrayCreate((ulong) number._value, prototypeObject);
                    break;
                case IObjectWrapper objectWrapper:
                    instance = objectWrapper.Target is IEnumerable enumerable
                        ? ConstructArrayFromIEnumerable(enumerable)
                        : ArrayCreate(0, prototypeObject);
                    break;
                case JsArray array:
                    // direct copy
                    instance = (JsArray) ConstructArrayFromArrayLike(Undefined, ArrayOperations.For(array, forWrite: false), callable: null, this);
                    break;
                default:
                    instance = ArrayCreate(capacity, prototypeObject);
                    instance._length!._value = JsNumber.PositiveZero;
                    instance.Push(arguments);
                    break;
            }
        }
        else
        {
            instance = ArrayCreate((ulong) arguments.Length, prototypeObject);
            instance._length!._value = JsNumber.PositiveZero;
            if (arguments.Length > 0)
            {
                instance.Push(arguments);
            }
        }

        return instance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraycreate
    /// </summary>
    internal JsArray ArrayCreate(ulong length, ObjectInstance? proto = null)
    {
        if (length > ArrayOperations.MaxArrayLength)
        {
            Throw.RangeError(_realm, "Invalid array length " + length);
        }

        proto ??= PrototypeObject;
        var instance = new JsArray(Engine, (uint) length, (uint) length)
        {
            _prototype = proto
        };
        return instance;
    }

    private JsArray ConstructArrayFromIEnumerable(IEnumerable enumerable)
    {
        var jsArray = Construct(Arguments.Empty);
        var tempArray = _engine._jsValueArrayPool.RentArray(1);
        foreach (var item in enumerable)
        {
            var jsItem = FromObject(Engine, item);
            tempArray[0] = jsItem;
            _realm.Intrinsics.Array.PrototypeObject.Push(jsArray, tempArray);
        }

        _engine._jsValueArrayPool.ReturnArray(tempArray);
        return jsArray;
    }

    public JsArray ConstructFast(JsValue[] contents)
    {
        var array = new JsValue[contents.Length];
        System.Array.Copy(contents, array, contents.Length);
        return new JsArray(_engine, array);
    }

    internal JsArray ConstructFast(List<JsValue> contents)
    {
        var array = new JsValue[contents.Count];
        contents.CopyTo(array);
        return new JsArray(_engine, array);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arrayspeciescreate
    /// </summary>
    internal ObjectInstance ArraySpeciesCreate(ObjectInstance originalArray, ulong length)
    {
        var isArray = originalArray.IsArray();
        if (!isArray)
        {
            return ArrayCreate(length);
        }

        var c = originalArray.Get(CommonProperties.Constructor);

        if (c.IsConstructor)
        {
            var thisRealm = _engine.ExecutionContext.Realm;
            var realmC = GetFunctionRealm(c);
            if (!ReferenceEquals(thisRealm, realmC))
            {
                if (ReferenceEquals(c, realmC.Intrinsics.Array))
                {
                    c = Undefined;
                }
            }
        }

        if (c.IsObject())
        {
            c = c.Get(GlobalSymbolRegistry.Species);
            if (c.IsNull())
            {
                c = Undefined;
            }
        }

        if (c.IsUndefined())
        {
            return ArrayCreate(length);
        }

        if (!c.IsConstructor)
        {
            Throw.TypeError(_realm, $"{c} is not a constructor");
        }

        return ((IConstructor) c).Construct([JsNumber.Create(length)], c);
    }

    internal JsArray CreateArrayFromList<T>(List<T> values) where T : JsValue
    {
        var jsArray = ArrayCreate((uint) values.Count);
        var index = 0;
        for (; index < values.Count; index++)
        {
            var item = values[index];
            jsArray.SetIndexValue((uint) index, item, false);
        }

        jsArray.SetLength((uint) index);
        return jsArray;
    }

    internal JsArray CreateArrayFromList<T>(T[] values) where T : JsValue
    {
        var jsArray = ArrayCreate((uint) values.Length);
        var index = 0;
        for (; index < values.Length; index++)
        {
            var item = values[index];
            jsArray.SetIndexValue((uint) index, item, false);
        }

        jsArray.SetLength((uint) index);
        return jsArray;
    }

    private void ValidateLength(double length)
    {
        if (length < 0 || length > ArrayOperations.MaxArrayLikeLength || ((long) length) != length)
        {
            Throw.RangeError(_realm, "Invalid array length");
        }
    }
}
