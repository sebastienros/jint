using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise;

internal sealed record PromiseCapability(
    JsValue PromiseInstance,
    ICallable Resolve,
    ICallable Reject,
    JsValue ResolveObj,
    JsValue RejectObj);

internal sealed class PromiseConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Promise");

    internal PromiseConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PromisePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PromisePrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(8, checkExistingKeys: false)
        {
            ["all"] = new(new PropertyDescriptor(new ClrFunction(Engine, "all", All, 1, LengthFlags), PropertyFlags)),
            ["allSettled"] = new(new PropertyDescriptor(new ClrFunction(Engine, "allSettled", AllSettled, 1, LengthFlags), PropertyFlags)),
            ["any"] = new(new PropertyDescriptor(new ClrFunction(Engine, "any", Any, 1, LengthFlags), PropertyFlags)),
            ["race"] = new(new PropertyDescriptor(new ClrFunction(Engine, "race", Race, 1, LengthFlags), PropertyFlags)),
            ["reject"] = new(new PropertyDescriptor(new ClrFunction(Engine, "reject", Reject, 1, LengthFlags), PropertyFlags)),
            ["resolve"] = new(new PropertyDescriptor(new ClrFunction(Engine, "resolve", Resolve, 1, LengthFlags), PropertyFlags)),
            ["try"] = new(new PropertyDescriptor(new ClrFunction(Engine, "try", Try, 1, LengthFlags), PropertyFlags)),
            ["withResolvers"] = new(new PropertyDescriptor(new ClrFunction(Engine, "withResolvers", WithResolvers , 0, LengthFlags), PropertyFlags)),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(
                get: new ClrFunction(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable),
                set: Undefined, PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-promise-executor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Constructor Promise requires 'new'");
        }

        if (arguments.At(0) is not ICallable executor)
        {
            ExceptionHelper.ThrowTypeError(_realm, $"Promise executor {(arguments.At(0))} is not a function");
            return null;
        }

        var promise = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Promise.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsPromise(engine));

        var (resolve, reject) = promise.CreateResolvingFunctions();
        try
        {
            executor.Call(Undefined, resolve, reject);
        }
        catch (JavaScriptException e)
        {
            reject.Call(JsValue.Undefined, [e.Error]);
        }

        return promise;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-promise.resolve
    /// </summary>
    internal JsValue Resolve(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsObject())
        {
            ExceptionHelper.ThrowTypeError(_realm, "PromiseResolve called on non-object");
        }

        if (thisObject is not IConstructor)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Promise.resolve invoked on a non-constructor value");
        }

        var x = arguments.At(0);
        return PromiseResolve(thisObject, x);
    }

    private JsObject WithResolvers(JsValue thisObject, JsCallArguments arguments)
    {
        var promiseCapability = NewPromiseCapability(_engine, thisObject);
        var obj = OrdinaryObjectCreate(_engine, _engine.Realm.Intrinsics.Object.PrototypeObject);
        obj.CreateDataPropertyOrThrow("promise", promiseCapability.PromiseInstance);
        obj.CreateDataPropertyOrThrow("resolve", promiseCapability.ResolveObj);
        obj.CreateDataPropertyOrThrow("reject", promiseCapability.RejectObj);
        return obj;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-promise-resolve
    /// </summary>
    private JsValue PromiseResolve(JsValue thisObject, JsValue x)
    {
        if (x.IsPromise())
        {
            var xConstructor = x.Get(CommonProperties.Constructor);
            if (SameValue(xConstructor, thisObject))
            {
                return x;
            }
        }

        var capability = NewPromiseCapability(_engine, thisObject);

        capability.Resolve.Call(Undefined, x);

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-promise.reject
    /// </summary>
    private JsValue Reject(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsObject())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Promise.reject called on non-object");
        }

        if (thisObject is not IConstructor)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Promise.reject invoked on a non-constructor value");
        }

        var r = arguments.At(0);

        var capability = NewPromiseCapability(_engine, thisObject);

        capability.Reject.Call(Undefined, r);

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/proposal-promise-try/
    /// </summary>
    private JsValue Try(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsObject())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Promise.try called on non-object");
        }

        var callbackfn = arguments.At(0);
        var promiseCapability = NewPromiseCapability(_engine, thisObject);

        try
        {
            var status = callbackfn.Call(Undefined, arguments.AsSpan().Slice(1).ToArray());
            promiseCapability.Resolve.Call(Undefined, status);
        }
        catch (JavaScriptException e)
        {
            promiseCapability.Reject.Call(Undefined, e.Error);
        }

        return promiseCapability.PromiseInstance;
    }

    // This helper methods executes the first 6 steps in the specs belonging to static Promise methods like all, any etc.
    // If it returns false, that means it has an error and it is already rejected
    // If it returns true, the logic specific to the calling function should continue executing
    private bool TryGetPromiseCapabilityAndIterator(JsValue thisObject, JsCallArguments arguments, string callerName, out PromiseCapability capability, out ICallable promiseResolve, out IteratorInstance iterator)
    {
        if (!thisObject.IsObject())
        {
            ExceptionHelper.ThrowTypeError(_realm, $"{callerName} called on non-object");
        }

        //2. Let promiseCapability be ? NewPromiseCapability(C).
        capability = NewPromiseCapability(_engine, thisObject);
        var reject = capability.Reject;

        //3. Let promiseResolve be GetPromiseResolve(C).
        // 4. IfAbruptRejectPromise(promiseResolve, promiseCapability).
        try
        {
            promiseResolve = GetPromiseResolve(thisObject);
        }
        catch (JavaScriptException e)
        {
            reject.Call(Undefined, e.Error);
            promiseResolve = null!;
            iterator = null!;
            return false;
        }


        // 5. Let iteratorRecord be GetIterator(iterable).
        // 6. IfAbruptRejectPromise(iteratorRecord, promiseCapability).

        try
        {
            if (arguments.Length == 0)
            {
                ExceptionHelper.ThrowTypeError(_realm, $"no arguments were passed to {callerName}");
            }

            var iterable = arguments.At(0);

            iterator = iterable.GetIterator(_realm);
        }
        catch (JavaScriptException e)
        {
            reject.Call(Undefined, e.Error);
            iterator = null!;
            return false;
        }

        return true;
    }

    // https://tc39.es/ecma262/#sec-promise.all
    private JsValue All(JsValue thisObject, JsCallArguments arguments)
    {
        if (!TryGetPromiseCapabilityAndIterator(thisObject, arguments, "Promise.all", out var capability, out var promiseResolve, out var iterator))
            return capability.PromiseInstance;

        var results = new List<JsValue>();
        bool doneIterating = false;

        void ResolveIfFinished()
        {
            // that means all of them were resolved
            // Note that "Undefined" is not null, thus the logic is sound, even though awkward
            // also note that it is important to check if we are done iterating.
            // if "then" method is sync then it will be resolved BEFORE the next iteration cycle
            if (results.TrueForAll(static x => x is not null) && doneIterating)
            {
                var array = _realm.Intrinsics.Array.ConstructFast(results);
                capability.Resolve.Call(Undefined, array);
            }
        }

        // 27.2.4.1.2 PerformPromiseAll ( iteratorRecord, constructor, resultCapability, promiseResolve )
        // https://tc39.es/ecma262/#sec-performpromiseall
        try
        {
            int index = 0;

            do
            {
                JsValue value;
                try
                {
                    if (!iterator.TryIteratorStep(out var nextItem))
                    {
                        doneIterating = true;

                        ResolveIfFinished();
                        break;
                    }

                    value = nextItem.Get(CommonProperties.Value);
                }
                catch (JavaScriptException e)
                {
                    capability.Reject.Call(Undefined, e.Error);
                    return capability.PromiseInstance;
                }

                // note that null here is important
                // it will help to detect if all inner promises were resolved
                // In F# it would be Option<JsValue>
                results.Add(null!);

                var item = promiseResolve.Call(thisObject, value);
                var thenProps = item.Get("then");
                if (thenProps is ICallable thenFunc)
                {
                    var capturedIndex = index;

                    var alreadyCalled = false;
                    var onSuccess =
                        new ClrFunction(_engine, "", (_, args) =>
                        {
                            if (!alreadyCalled)
                            {
                                alreadyCalled = true;
                                results[capturedIndex] = args.At(0);
                                ResolveIfFinished();
                            }

                            return Undefined;
                        }, 1, PropertyFlag.Configurable);

                    thenFunc.Call(item, onSuccess, capability.RejectObj);
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(_realm, "Passed non Promise-like value");
                }

                index += 1;
            } while (true);
        }
        catch (JavaScriptException e)
        {
            iterator.Close(CompletionType.Throw);
            capability.Reject.Call(Undefined, e.Error);
            return capability.PromiseInstance;
        }

        return capability.PromiseInstance;
    }

    // https://tc39.es/ecma262/#sec-promise.allsettled
    private JsValue AllSettled(JsValue thisObject, JsCallArguments arguments)
    {
        if (!TryGetPromiseCapabilityAndIterator(thisObject, arguments, "Promise.allSettled", out var capability, out var promiseResolve, out var iterator))
            return capability.PromiseInstance;

        var results = new List<JsValue>();
        bool doneIterating = false;

        void ResolveIfFinished()
        {
            // that means all of them were resolved
            // Note that "Undefined" is not null, thus the logic is sound, even though awkward
            // also note that it is important to check if we are done iterating.
            // if "then" method is sync then it will be resolved BEFORE the next iteration cycle
            if (results.TrueForAll(static x => x is not null) && doneIterating)
            {
                var array = _realm.Intrinsics.Array.ConstructFast(results);
                capability.Resolve.Call(Undefined, array);
            }
        }

        // 27.2.4.1.2 PerformPromiseAll ( iteratorRecord, constructor, resultCapability, promiseResolve )
        // https://tc39.es/ecma262/#sec-performpromiseall
        try
        {
            int index = 0;

            do
            {
                JsValue value;
                try
                {
                    if (!iterator.TryIteratorStep(out var nextItem))
                    {
                        doneIterating = true;

                        ResolveIfFinished();
                        break;
                    }

                    value = nextItem.Get(CommonProperties.Value);
                }
                catch (JavaScriptException e)
                {
                    capability.Reject.Call(Undefined, e.Error);
                    return capability.PromiseInstance;
                }

                // note that null here is important
                // it will help to detect if all inner promises were resolved
                // In F# it would be Option<JsValue>
                results.Add(null!);

                var item = promiseResolve.Call(thisObject, value);
                var thenProps = item.Get("then");
                if (thenProps is ICallable thenFunc)
                {
                    var capturedIndex = index;

                    var alreadyCalled = false;
                    var onSuccess =
                        new ClrFunction(_engine, "", (_, args) =>
                        {
                            if (!alreadyCalled)
                            {
                                alreadyCalled = true;

                                var res = Engine.Realm.Intrinsics.Object.Construct(2);
                                res.FastSetDataProperty("status", "fulfilled");
                                res.FastSetDataProperty("value", args.At(0));
                                results[capturedIndex] = res;

                                ResolveIfFinished();
                            }

                            return Undefined;
                        }, 1, PropertyFlag.Configurable);
                    var onFailure =
                        new ClrFunction(_engine, "", (_, args) =>
                        {
                            if (!alreadyCalled)
                            {
                                alreadyCalled = true;

                                var res = Engine.Realm.Intrinsics.Object.Construct(2);
                                res.FastSetDataProperty("status", "rejected");
                                res.FastSetDataProperty("reason", args.At(0));
                                results[capturedIndex] = res;

                                ResolveIfFinished();
                            }

                            return Undefined;
                        }, 1, PropertyFlag.Configurable);

                    thenFunc.Call(item, onSuccess, onFailure);
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(_realm, "Passed non Promise-like value");
                }

                index += 1;
            } while (true);
        }
        catch (JavaScriptException e)
        {
            iterator.Close(CompletionType.Throw);
            capability.Reject.Call(Undefined, e.Error);
            return capability.PromiseInstance;
        }

        return capability.PromiseInstance;
    }

    // https://tc39.es/ecma262/#sec-promise.any
    private JsValue Any(JsValue thisObject, JsCallArguments arguments)
    {
        if (!TryGetPromiseCapabilityAndIterator(thisObject, arguments, "Promise.any", out var capability, out var promiseResolve, out var iterator))
        {
            return capability.PromiseInstance;
        }

        var errors = new List<JsValue>();
        var doneIterating = false;

        void RejectIfAllRejected()
        {
            // that means all of them were rejected
            // Note that "Undefined" is not null, thus the logic is sound, even though awkward
            // also note that it is important to check if we are done iterating.
            // if "then" method is sync then it will be resolved BEFORE the next iteration cycle

            if (errors.TrueForAll(static x => x is not null) && doneIterating)
            {
                var array = _realm.Intrinsics.Array.ConstructFast(errors);

                capability.Reject.Call(Undefined, Construct(_realm.Intrinsics.AggregateError, [array]));
            }
        }

        // https://tc39.es/ecma262/#sec-performpromiseany
        try
        {
            var index = 0;

            do
            {
                ObjectInstance? nextItem = null;
                JsValue value;
                try
                {
                    if (!iterator.TryIteratorStep(out nextItem))
                    {
                        doneIterating = true;
                        RejectIfAllRejected();
                        break;
                    }

                    value = nextItem.Get(CommonProperties.Value);
                }
                catch (JavaScriptException e)
                {
                    if (nextItem?.Get("done")?.AsBoolean() == false)
                    {
                        throw;
                    }
                    errors.Add(e.Error);
                    continue;
                }

                // note that null here is important
                // it will help to detect if all inner promises were rejected
                // In F# it would be Option<JsValue>
                errors.Add(null!);

                var item = promiseResolve.Call(thisObject, value);
                var thenProps = item.Get("then");
                if (thenProps is ICallable thenFunc)
                {
                    var capturedIndex = index;

                    var alreadyCalled = false;

                    var onError =
                        new ClrFunction(_engine, "", (_, args) =>
                        {
                            if (!alreadyCalled)
                            {
                                alreadyCalled = true;
                                errors[capturedIndex] = args.At(0);
                                RejectIfAllRejected();
                            }

                            return Undefined;
                        }, 1, PropertyFlag.Configurable);

                    thenFunc.Call(item, capability.ResolveObj, onError);
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(_realm, "Passed non Promise-like value");
                }

                index += 1;
            } while (true);
        }
        catch (JavaScriptException e)
        {
            iterator.Close(CompletionType.Throw);
            capability.Reject.Call(Undefined, e.Error);
            return capability.PromiseInstance;
        }

        return capability.PromiseInstance;
    }

    // https://tc39.es/ecma262/#sec-promise.race
    private JsValue Race(JsValue thisObject, JsCallArguments arguments)
    {
        if (!TryGetPromiseCapabilityAndIterator(thisObject, arguments, "Promise.race", out var capability, out var promiseResolve, out var iterator))
            return capability.PromiseInstance;

        // 7. Let result be PerformPromiseRace(iteratorRecord, C, promiseCapability, promiseResolve).
        // https://tc39.es/ecma262/#sec-performpromiserace
        try
        {
            do
            {
                JsValue nextValue;
                try
                {
                    if (!iterator.TryIteratorStep(out var nextItem))
                    {
                        break;
                    }

                    nextValue = nextItem.Get(CommonProperties.Value);
                }
                catch (JavaScriptException e)
                {
                    capability.Reject.Call(Undefined, e.Error);
                    return capability.PromiseInstance;
                }

                // h. Let nextPromise be ? Call(promiseResolve, constructor, « nextValue »).
                var nextPromise = promiseResolve.Call(thisObject, nextValue);

                // i. Perform ? Invoke(nextPromise, "then", « resultCapability.[[Resolve]], resultCapability.[[Reject]] »).

                _engine.Invoke(nextPromise, "then", [(JsValue) capability.Resolve, capability.RejectObj]);
            } while (true);
        }
        catch (JavaScriptException e)
        {
            // 8. If result is an abrupt completion, then
            // a. If iteratorRecord.[[Done]] is false, set result to IteratorClose(iteratorRecord, result).
            //     b. IfAbruptRejectPromise(result, promiseCapability).
            iterator.Close(CompletionType.Throw);
            capability.Reject.Call(Undefined, e.Error);
            return capability.PromiseInstance;
        }

        // 9. Return Completion(result).
        // Note that PerformPromiseRace returns a Promise instance in success case
        return capability.PromiseInstance;
    }


    // https://tc39.es/ecma262/#sec-getpromiseresolve
    // 27.2.4.1.1 GetPromiseResolve ( promiseConstructor )
    // The abstract operation GetPromiseResolve takes argument promiseConstructor. It performs the following steps when called:
    //
    // 1. Assert: IsConstructor(promiseConstructor) is true.
    //     2. Let promiseResolve be ? Get(promiseConstructor, "resolve").
    //     3. If IsCallable(promiseResolve) is false, throw a TypeError exception.
    // 4. Return promiseResolve.
    private ICallable GetPromiseResolve(JsValue promiseConstructor)
    {
        AssertConstructor(_engine, promiseConstructor);

        var resolveProp = promiseConstructor.Get("resolve");
        if (resolveProp is ICallable resolve)
        {
            return resolve;
        }

        ExceptionHelper.ThrowTypeError(_realm, "resolve is not a function");
        // Note: throws right before return
        return null;
    }

    // https://tc39.es/ecma262/#sec-newpromisecapability
    // The abstract operation NewPromiseCapability takes argument C.
    // It attempts to use C as a constructor in the fashion of the built-in Promise constructor to create a Promise
    // object and extract its resolve and reject functions.
    // The Promise object plus the resolve and reject functions are used to initialize a new PromiseCapability Record.
    // It performs the following steps when called:
    //
    // 1. If IsConstructor(C) is false, throw a TypeError exception.
    // 2. NOTE: C is assumed to be a constructor function that supports the parameter conventions of the Promise constructor (see 27.2.3.1).
    //     3. Let promiseCapability be the PromiseCapability Record { [[Promise]]: undefined, [[Resolve]]: undefined, [[Reject]]: undefined }.
    // 4. Let steps be the algorithm steps defined in GetCapabilitiesExecutor Functions.
    // 5. Let length be the number of non-optional parameters of the function definition in GetCapabilitiesExecutor Functions.
    // 6. Let executor be ! CreateBuiltinFunction(steps, length, "", « [[Capability]] »).
    // 7. Set executor.[[Capability]] to promiseCapability.
    // 8. Let promise be ? Construct(C, « executor »).
    // 9. If IsCallable(promiseCapability.[[Resolve]]) is false, throw a TypeError exception.
    // 10. If IsCallable(promiseCapability.[[Reject]]) is false, throw a TypeError exception.
    // 11. Set promiseCapability.[[Promise]] to promise.
    // 12. Return promiseCapability.
    internal static PromiseCapability NewPromiseCapability(Engine engine, JsValue c)
    {
        var ctor = AssertConstructor(engine, c);

        JsValue? resolveArg = null;
        JsValue? rejectArg = null;

        JsValue Executor(JsValue thisObject, JsCallArguments arguments)
        {
            // 25.4.1.5.1 GetCapabilitiesExecutor Functions
            // 3. If promiseCapability.[[Resolve]] is not undefined, throw a TypeError exception.
            // 4. If promiseCapability.[[Reject]] is not undefined, throw a TypeError exception.
            // 5. Set promiseCapability.[[Resolve]] to resolve.
            // 6. Set promiseCapability.[[Reject]] to reject.
            if (resolveArg is not null && resolveArg != Undefined ||
                rejectArg is not null && rejectArg != Undefined)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "executor was already called with not undefined args");
            }

            resolveArg = arguments.At(0);
            rejectArg = arguments.At(1);

            return Undefined;
        }

        var executor = new ClrFunction(engine, "", Executor, 2, PropertyFlag.Configurable);

        var instance = ctor.Construct([executor], c);

        ICallable? resolve = null;
        ICallable? reject = null;

        if (resolveArg is ICallable resFunc)
        {
            resolve = resFunc;
        }
        else
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "resolve is not a function");
        }

        if (rejectArg is ICallable rejFunc)
        {
            reject = rejFunc;
        }
        else
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, "reject is not a function");
        }

        return new PromiseCapability(
            PromiseInstance: instance,
            Resolve: resolve,
            Reject: reject,
            RejectObj: rejectArg,
            ResolveObj: resolveArg);
    }
}
