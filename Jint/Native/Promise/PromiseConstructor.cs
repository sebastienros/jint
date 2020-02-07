using System.Linq;
using System.Threading.Tasks;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public sealed class PromiseConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Promise");

        private PromiseConstructor(Engine engine)
            : base(engine, _functionName, false)
        {
        }

        public static PromiseConstructor CreatePromiseConstructor(Engine engine)
        {
            var obj = new PromiseConstructor(engine);
            obj._prototype = PromisePrototype.CreatePrototypeObject(engine, obj);
            obj._length = new PropertyDescriptor(1, PropertyFlag.Configurable);
            obj._prototypeDescriptor = new PropertyDescriptor(obj._prototype, PropertyFlag.AllForbidden);
            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["resolve"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "resolve", Resolve, 1, lengthFlags), propertyFlags)),
                ["reject"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "reject", Reject, 1, lengthFlags), propertyFlags)),
                ["all"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "all", All, 1, lengthFlags), propertyFlags)),
                ["race"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "race", Race, 1, lengthFlags), propertyFlags)),
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "undefined is not a promise");
            }

            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue receiver)
        {
            if (!(arguments.At(0) is ICallable promiseResolver))
            {
                return ExceptionHelper.ThrowTypeError<ObjectInstance>(
                    _engine,
                    $"Promise resolver {(arguments.At(0))} is not a function");
            }

            var instance = new PromiseInstance(Engine, promiseResolver);

            instance.InvokePromiseResolver();

            return instance;
        }

        private JsValue Resolve(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine, "PromiseResolve called on non-object");
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

            var promiseCapability = NewPromiseCapability(thisObj);
            promiseCapability.Resolve(Undefined, new[] { x });
            return promiseCapability;
        }

        private PromiseInstance Reject(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine, "PromiseReject called on non-object");
            }

            var r = arguments.At(0);

            var promiseCapability = NewPromiseCapability(thisObj);
            promiseCapability.Reject(Undefined, new[] { r });
            return promiseCapability;
        }

        private JsValue All(JsValue thisObj, JsValue[] arguments)
        {
            var c = thisObj;
            if (!c.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine, "PromiseReject called on non-object");
            }

            var s = c.Get(GlobalSymbolRegistry.Species);
            if (!s.IsNullOrUndefined())
            {
                c = s;
            }

            var promiseCapability = NewPromiseCapability(c);

            var iterable = arguments.At(0);
            var iterator = iterable.GetIterator(_engine);
            var items = iterator.CopyToList();

            if (items.Count == 0)
            {
                return promiseCapability.Resolve(Undefined, new JsValue[] { Engine.Array.ConstructFast(0) });
            }

            var chainedPromise = new PromiseInstance(Engine);

            var promises = items.OfType<PromiseInstance>().ToArray();

            if (promises.Length == 0)
            {
                Engine.QueuePromiseContinuation(() =>
                {
                    chainedPromise.Resolve(Undefined, new JsValue[] { Engine.Array.Construct(items.ToArray()) });
                });
            }
            else
            {
                Task.WhenAll(promises.Select(p => p.Task)).ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        Engine.QueuePromiseContinuation(() =>
                        {
                            var resolvedItems = items.Select(i =>
                            {
                                if (i is PromiseInstance promise)
                                    return promise.Task.Result;

                                return i;

                            }).ToArray();

                            chainedPromise.Resolve(Undefined, new JsValue[] {Engine.Array.Construct(resolvedItems)});
                        });

                        return;
                    }


                    Engine.QueuePromiseContinuation(() =>
                    {
                        var error = Undefined;

                        if (t.Exception?.InnerExceptions.FirstOrDefault() is PromiseRejectedException jsEx)
                            error = jsEx.RejectedValue;

                        chainedPromise.Reject(Undefined, new[] {error});
                    });

                });
            }

            return chainedPromise;
        }

        private PromiseInstance Race(JsValue thisObj, JsValue[] arguments)
        {
            var c = thisObj;
            if (!c.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine, "PromiseReject called on non-object");
            }

            var s = c.Get(GlobalSymbolRegistry.Species);
            if (!s.IsNullOrUndefined())
            {
                c = s;
            }
            
            var chainedPromise = NewPromiseCapability(c);
            var iterable = arguments.At(0);
            var iterator = iterable.GetIterator(_engine);
            
            var items = iterator.CopyToList();

            //  If no promises passed then the spec says to pend forever!
            if (items.Count == 0)
                return chainedPromise;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (item is PromiseInstance promise)
                {
                    if (promise.Task.Status == TaskStatus.RanToCompletion)
                        Engine.QueuePromiseContinuation(() => { chainedPromise.Resolve(Undefined, new[] { promise.Task.Result }); });
                    else if (promise.Task.IsFaulted || promise.Task.IsCanceled)
                        Engine.QueuePromiseContinuation(() =>
                        {
                            var error = Undefined;

                            if (promise.Task.Exception?.InnerExceptions.FirstOrDefault() is PromiseRejectedException jsEx)
                                error = jsEx.RejectedValue;

                            chainedPromise.Reject(Undefined, new[] { error });
                        });
                    else
                        continue;
                }
                else
                    Engine.QueuePromiseContinuation(() => { chainedPromise.Resolve(Undefined, new[] {item}); });

                return chainedPromise;
            }
            
            //  Else all unresolved promises so wait first
            var promises = items.Cast<PromiseInstance>().ToArray();

            Task.WhenAny(promises.Select(p => p.Task)).ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    Engine.QueuePromiseContinuation(() => { chainedPromise.Resolve(Undefined, new[] {t.Result.Result}); });

                    return;
                }

                Engine.QueuePromiseContinuation(() =>
                {
                    var error = Undefined;

                    if (t.Exception?.InnerExceptions.FirstOrDefault() is PromiseRejectedException jsEx)
                        error = jsEx.RejectedValue;

                    chainedPromise.Reject(Undefined, new[] {error});
                });

            });

            return chainedPromise;
        }

        private PromiseInstance NewPromiseCapability(JsValue c)
        {
            var constructor = AssertConstructor(_engine, c);

            var executor = new PromiseInstance(_engine);
            var test = Construct(constructor, new JsValue[] { executor });
            var promiseCapability = executor;
            return promiseCapability;
        }
    }
}