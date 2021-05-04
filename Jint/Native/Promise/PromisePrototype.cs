using System;
using System.Linq;
using System.Threading.Tasks;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public sealed class PromisePrototype : ObjectInstance
    {
        private PromiseConstructor _promiseConstructor;

        private PromisePrototype(Engine engine) : base(engine)
        {
        }

        public static PromisePrototype CreatePrototypeObject(Engine engine, PromiseConstructor promiseConstructor)
        {
            var obj = new PromisePrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _promiseConstructor = promiseConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_promiseConstructor, PropertyFlag.NonEnumerable),
                ["then"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "then", Then, 2, lengthFlags),
                    propertyFlags),
                ["catch"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "catch", Catch, 1, lengthFlags),
                    propertyFlags),
                ["finally"] =
                    new PropertyDescriptor(new ClrFunctionInstance(Engine, "finally", Finally, 1, lengthFlags),
                        propertyFlags)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] =
                    new PropertyDescriptor(new JsString("Promise"), PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        // https://tc39.es/ecma262/#sec-promise.prototype.then
        // When the then method is called with arguments onFulfilled and onRejected,
        // the following steps are taken:
        //
        // 1. Let promise be the this value.
        // 2. If IsPromise(promise) is false, throw a TypeError exception.
        // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
        // 4. Let resultCapability be ? NewPromiseCapability(C).
        // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
        private JsValue Then(JsValue thisValue, JsValue[] args)
        {
            // 1. Let promise be the this value.
            // 2. If IsPromise(promise) is false, throw a TypeError exception.
            var promise = thisValue as PromiseInstance ?? ExceptionHelper.ThrowTypeError<PromiseInstance>(_engine,
                "Method Promise.prototype.then called on incompatible receiver");

            // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
            var ctor = SpeciesConstructor(promise, _engine.Promise);
            
            // 4. Let resultCapability be ? NewPromiseCapability(C).
            var capability = PromiseConstructor.NewPromiseCapabilityCustom(_engine, ctor as JsValue);

            // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
            return PromiseOperations.PerformPromiseThen(_engine, promise, args.At(0), args.At(1), capability);
        }


       

        private JsValue PerformPromise_(JsValue thisValue, JsValue[] args)
        {
            var promise = thisValue as PromiseInstance;

            if (promise == null)
            {
                ExceptionHelper.ThrowTypeError(_engine,
                    "Method Promise.prototype.then called on incompatible receiver");
                return null;
            }


            var chainedPromise = new PromiseInstance(Engine);

            var resolvedCallback = (args.Length >= 1 ? args[0] : null) as FunctionInstance ?? Undefined;
            var rejectedCallback = (args.Length >= 2 ? args[1] : null) as FunctionInstance ?? Undefined;

            promise.Task.ContinueWith(t =>
            {
                var continuation = (Action) (() => { });

                if (t.Status == TaskStatus.RanToCompletion)
                {
                    if (resolvedCallback == Undefined)
                    {
                        continuation = () =>
                        {
                            //  If no success callback then simply pass the return value to the next promise in chain
                            chainedPromise.Resolve(null, new[] {t.Result});
                        };
                    }
                    else
                    {
                        continuation = () =>
                        {
                            JsValue result;

                            try
                            {
                                result = resolvedCallback.Invoke(t.Result);
                            }
                            catch (Exception ex)
                            {
                                var error = Undefined;

                                if (ex is JavaScriptException jsEx)
                                    error = jsEx.Error;

                                chainedPromise.Reject(Undefined, new[] {error});
                                return;
                            }

                            void HandleNestedPromiseResult(JsValue nestedResult)
                            {
                                //  If the result is a promise then we want to chain to this result instead!
                                if (nestedResult is PromiseInstance resultPromise)
                                {
                                    resultPromise.Task.ContinueWith(ct =>
                                    {
                                        if (ct.Status == TaskStatus.RanToCompletion)
                                            HandleNestedPromiseResult(ct.Result);

                                        else if (ct.Status == TaskStatus.Faulted || ct.Status == TaskStatus.Canceled)
                                        {
                                            var rejectValue = Undefined;

                                            if (ct.Exception?.InnerExceptions.FirstOrDefault() is
                                                PromiseRejectedException promiseRejection)
                                                rejectValue = promiseRejection.RejectedValue;

                                            _engine.QueuePromiseContinuation(() =>
                                                chainedPromise.Reject(null, new[] {rejectValue}));
                                        }
                                    });
                                }
                                else
                                    _engine.QueuePromiseContinuation(() =>
                                        chainedPromise.Resolve(null, new[] {nestedResult}));
                            }

                            HandleNestedPromiseResult(result);
                        };
                    }
                }


                else if (t.IsFaulted || t.IsCanceled)
                {
                    var rejectValue = Undefined;

                    if (t.Exception?.InnerExceptions.FirstOrDefault() is PromiseRejectedException promiseRejection)
                        rejectValue = promiseRejection.RejectedValue;

                    if (rejectedCallback == Undefined)
                    {
                        continuation = () =>
                        {
                            //  If no error callback then simply pass the error value to the next promise in chain
                            chainedPromise.Reject(null, new[] {rejectValue});
                        };
                    }
                    else
                    {
                        continuation = () =>
                        {
                            rejectedCallback.Invoke(rejectValue);

                            //  Chain is restored after handling catch
                            chainedPromise.Resolve(Undefined, new[] {Undefined});
                        };
                    }
                }

                _engine.QueuePromiseContinuation(continuation);
            });

            return chainedPromise;
        }

        private JsValue Catch(JsValue thisValue, JsValue[] args) =>
            Then(thisValue, new[] {Undefined, args.Length >= 1 ? args[0] : Undefined});

        private JsValue Finally(JsValue thisValue, JsValue[] args)
        {
            var promise = thisValue as PromiseInstance;

            if (promise == null)
            {
                ExceptionHelper.ThrowTypeError(_engine,
                    "Method Promise.prototype.then called on incompatible receiver");
                return null;
            }

            var chainedPromise = new PromiseInstance(Engine);

            var callback = (args.Length >= 1 ? args[0] : null) as FunctionInstance ?? Undefined;

            promise.Task.ContinueWith(t =>
            {
                _engine.QueuePromiseContinuation(() =>
                {
                    try
                    {
                        if (callback != Undefined)
                            callback.Invoke();
                    }
                    catch (Exception ex)
                    {
                        var error = Undefined;

                        if (ex is JavaScriptException jsEx)
                            error = jsEx.Error;

                        chainedPromise.Reject(Undefined, new[] {error});
                        return;
                    }

                    chainedPromise.Resolve(Undefined, new[] {t.Result});
                });
            });

            return chainedPromise;
        }
    }
}