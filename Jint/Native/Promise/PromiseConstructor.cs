using System.Collections.Generic;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    internal sealed record PromiseCapability(
        JsValue PromiseInstance,
        ICallable Resolve,
        ICallable Reject,
        JsValue RejectObj
    );

    public sealed class PromiseConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Promise");

        internal PromisePrototype PrototypeObject { get; private set; }

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

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["resolve"] =
                    new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "resolve", Resolve, 1, lengthFlags),
                        propertyFlags)),
                ["reject"] =
                    new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "reject", Reject, 1, lengthFlags),
                        propertyFlags)),
                ["all"] = new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "all", All, 1, lengthFlags),
                    propertyFlags)),
                ["race"] = new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "race", Race, 1, lengthFlags),
                    propertyFlags)),
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0,
                        PropertyFlag.Configurable),
                    set: Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Constructor Promise requires 'new'");
            return null;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Constructor Promise requires 'new'");
            }

            var promiseExecutor = arguments.At(0) as ICallable;
            if (promiseExecutor is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, $"Promise executor {(arguments.At(0))} is not a function");
            }

            var instance = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Promise.PrototypeObject,
                static(engine, realm, _) => new PromiseInstance(engine));

            var (resolve, reject) = instance.CreateResolvingFunctions();
            promiseExecutor.Call(Undefined, new JsValue[] {resolve, reject});

            return instance;
        }

        // The abstract operation PromiseResolve takes arguments C (a constructor) and x (an ECMAScript language value).
        // It returns a new promise resolved with x. It performs the following steps when called:
        //
        // 1. Assert: Type(C) is Object.
        // 2. If IsPromise(x) is true, then
        //     a. Let xConstructor be ? Get(x, "constructor").
        // b. If SameValue(xConstructor, C) is true, return x.
        // 3. Let promiseCapability be ? NewPromiseCapability(C).
        //     4. Perform ? Call(promiseCapability.[[Resolve]], undefined, « x »).
        // 5. Return promiseCapability.[[Promise]].
        internal JsValue Resolve(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "PromiseResolve called on non-object");
            }

            if (thisObj is not IConstructor)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Promise.resolve invoked on a non-constructor value");
            }

            JsValue x = arguments.At(0);
            if (x.IsPromise())
            {
                var xConstructor = x.Get(CommonProperties.Constructor);
                if (SameValue(xConstructor, thisObj))
                {
                    return x;
                }
            }

            var (instance, resolve, _, _) = NewPromiseCapability(_engine, thisObj);

            resolve.Call(Undefined, new[] {x});

            return instance;
        }

        private JsValue Reject(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Promise.reject called on non-object");
            }

            if (thisObj is not IConstructor)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Promise.reject invoked on a non-constructor value");
            }

            var r = arguments.At(0);

            var (instance, _, reject, _) = NewPromiseCapability(_engine, thisObj);

            reject.Call(Undefined, new[] {r});

            return instance;
        }

        // https://tc39.es/ecma262/#sec-promise.all
        // The all function returns a new promise which is fulfilled with an array of fulfillment values for the passed promises,
        // or rejects with the reason of the first passed promise that rejects. It resolves all elements of the passed iterable to promises as it runs this algorithm.
        //
        // 1. Let C be the this value.
        // 2. Let promiseCapability be ? NewPromiseCapability(C).
        //     3. Let promiseResolve be GetPromiseResolve(C).
        //     4. IfAbruptRejectPromise(promiseResolve, promiseCapability).
        //     5. Let iteratorRecord be GetIterator(iterable).
        //     6. IfAbruptRejectPromise(iteratorRecord, promiseCapability).
        //     7. Let result be PerformPromiseAll(iteratorRecord, C, promiseCapability, promiseResolve).
        //     8. If result is an abrupt completion, then
        //     a. If iteratorRecord.[[Done]] is false, set result to IteratorClose(iteratorRecord, result).
        // b. IfAbruptRejectPromise(result, promiseCapability).
        // 9. Return Completion(result)
        private JsValue All(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Promise.all called on non-object");
            }

            //2. Let promiseCapability be ? NewPromiseCapability(C).
            var (resultingPromise, resolve, reject, rejectObj) = NewPromiseCapability(_engine, thisObj);

            //3. Let promiseResolve be GetPromiseResolve(C).
            // 4. IfAbruptRejectPromise(promiseResolve, promiseCapability).
            ICallable promiseResolve;
            try
            {
                promiseResolve = GetPromiseResolve(thisObj);
            }
            catch (JavaScriptException e)
            {
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }


            IteratorInstance iterator;
            // 5. Let iteratorRecord be GetIterator(iterable).
            // 6. IfAbruptRejectPromise(iteratorRecord, promiseCapability).

            try
            {
                if (arguments.Length == 0)
                {
                    ExceptionHelper.ThrowTypeError(_realm, "no arguments were passed to Promise.all");
                }

                var iterable = arguments.At(0);

                iterator = iterable.GetIterator(_realm);
            }
            catch (JavaScriptException e)
            {
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }

            var results = new List<JsValue>();
            bool doneIterating = false;

            void ResolveIfFinished()
            {
                // that means all of them were resolved
                // Note that "Undefined" is not null, thus the logic is sound, even though awkward
                // also note that it is important to check if we are done iterating.
                // if "then" method is sync then it will be resolved BEFORE the next iteration cycle
                if (results.TrueForAll(static x => x != null) && doneIterating)
                {
                    var array = _realm.Intrinsics.Array.ConstructFast(results);
                    resolve.Call(Undefined, new JsValue[] { array });
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
                        reject.Call(Undefined, new[] {e.Error});
                        return resultingPromise;
                    }

                    // note that null here is important
                    // it will help to detect if all inner promises were resolved
                    // In F# it would be Option<JsValue>
                    results.Add(null);

                    var item = promiseResolve.Call(thisObj, new JsValue[] {value});
                    var thenProps = item.Get("then");
                    if (thenProps is ICallable thenFunc)
                    {
                        var capturedIndex = index;

                        var fulfilled = false;
                        var onSuccess =
                            new ClrFunctionInstance(_engine, "", (_, args) =>
                            {
                                if (!fulfilled)
                                {
                                    fulfilled = true;
                                    results[capturedIndex] = args.At(0);
                                    ResolveIfFinished();
                                }

                                return Undefined;
                            }, 1, PropertyFlag.Configurable);

                        thenFunc.Call(item, new JsValue[] {onSuccess, rejectObj});
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
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }

            // if there were not items but the iteration was successful
            // e.g. "[]" empty array as an example
            // resolve the promise sync
            if (results.Count == 0)
            {
                resolve.Call(Undefined, new JsValue[] {_realm.Intrinsics.Array.ConstructFast(0)});
            }

            return resultingPromise;
        }

        // https://tc39.es/ecma262/#sec-promise.race
        private JsValue Race(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Promise.all called on non-object");
            }

            // 2. Let promiseCapability be ? NewPromiseCapability(C).
            var (resultingPromise, resolve, reject, rejectObj) = NewPromiseCapability(_engine, thisObj);

            // 3. Let promiseResolve be GetPromiseResolve(C).
            // 4. IfAbruptRejectPromise(promiseResolve, promiseCapability).
            ICallable promiseResolve;
            try
            {
                promiseResolve = GetPromiseResolve(thisObj);
            }
            catch (JavaScriptException e)
            {
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }


            IteratorInstance iterator;
            // 5. Let iteratorRecord be GetIterator(iterable).
            // 6. IfAbruptRejectPromise(iteratorRecord, promiseCapability).

            try
            {
                if (arguments.Length == 0)
                {
                    ExceptionHelper.ThrowTypeError(_realm, "no arguments were passed to Promise.all");
                }

                var iterable = arguments.At(0);

                iterator = iterable.GetIterator(_realm);
            }
            catch (JavaScriptException e)
            {
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }

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
                        reject.Call(Undefined, new[] {e.Error});
                        return resultingPromise;
                    }

                    // h. Let nextPromise be ? Call(promiseResolve, constructor, « nextValue »).
                    var nextPromise = promiseResolve.Call(thisObj, new JsValue[] {nextValue});

                    // i. Perform ? Invoke(nextPromise, "then", « resultCapability.[[Resolve]], resultCapability.[[Reject]] »).

                    _engine.Invoke(nextPromise, "then", new[] {resolve as JsValue, rejectObj});
                } while (true);
            }
            catch (JavaScriptException e)
            {
                // 8. If result is an abrupt completion, then
                // a. If iteratorRecord.[[Done]] is false, set result to IteratorClose(iteratorRecord, result).
                //     b. IfAbruptRejectPromise(result, promiseCapability).
                iterator.Close(CompletionType.Throw);
                reject.Call(Undefined, new[] {e.Error});
                return resultingPromise;
            }

            // 9. Return Completion(result).
            // Note that PerformPromiseRace returns a Promise instance in success case
            return resultingPromise;
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

            JsValue resolveArg = null;
            JsValue rejectArg = null;

            JsValue Executor(JsValue thisObj, JsValue[] arguments)
            {
                // 25.4.1.5.1 GetCapabilitiesExecutor Functions
                // 3. If promiseCapability.[[Resolve]] is not undefined, throw a TypeError exception.
                // 4. If promiseCapability.[[Reject]] is not undefined, throw a TypeError exception.
                // 5. Set promiseCapability.[[Resolve]] to resolve.
                // 6. Set promiseCapability.[[Reject]] to reject.
                if (resolveArg != null && resolveArg != Undefined ||
                    rejectArg != null && rejectArg != Undefined)
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm, "executor was already called with not undefined args");
                }

                resolveArg = arguments.At(0);
                rejectArg = arguments.At(1);

                return Undefined;
            }

            var executor = new ClrFunctionInstance(engine, "", Executor, 2, PropertyFlag.Configurable);

            var instance = ctor.Construct(new JsValue[] {executor}, c);

            ICallable resolve = null;
            ICallable reject = null;

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

            return new PromiseCapability(instance, resolve, reject, rejectArg);
        }
    }
}