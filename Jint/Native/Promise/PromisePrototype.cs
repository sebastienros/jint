using System;
using System.Linq;
using System.Threading.Tasks;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
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
            var properties = new PropertyDictionary(4, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_promiseConstructor, PropertyFlag.NonEnumerable),
                ["then"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "then", Then, 1, PropertyFlag.Configurable), true, false, true),
                ["catch"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "catch", Catch, 1, PropertyFlag.Configurable), true, false, true),
                ["finally"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "finally", Finally, 0, PropertyFlag.Configurable), true, false, true)
            };
            SetProperties(properties);
        }

        public JsValue Then(JsValue thisValue, JsValue[] args)
        {
            var promise = thisValue as PromiseInstance;

            if (promise == null)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Method Promise.prototype.then called on incompatible receiver");
                return null;
            }

            var chainedPromise = new PromiseInstance(Engine)
            {
                _prototype = _promiseConstructor.PrototypeObject
            };

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
                            chainedPromise.Resolve(null, new[] { t.Result });
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

                                            if (ct.Exception?.InnerExceptions.FirstOrDefault() is PromiseRejectedException promiseRejection)
                                                rejectValue = promiseRejection.RejectedValue;

                                            _engine.QueuePromiseContinuation(() => chainedPromise.Reject(null, new[] {rejectValue}));
                                        }

                                    });
                                }
                                else
                                    _engine.QueuePromiseContinuation(() => chainedPromise.Resolve(null, new[] {nestedResult}));
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
                            chainedPromise.Reject(null, new[] { rejectValue });
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

        public JsValue Catch(JsValue thisValue, JsValue[] args) => Then(thisValue, new[] { Undefined, args.Length >= 1 ? args[0] : Undefined });

        public JsValue Finally(JsValue thisValue, JsValue[] args)
        {
            var promise = thisValue as PromiseInstance;

            if (promise == null)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Method Promise.prototype.then called on incompatible receiver");
                return null;
            }

            var chainedPromise = new PromiseInstance(Engine)
            {
                _prototype = _promiseConstructor.PrototypeObject
            };

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