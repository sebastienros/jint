using System.Linq;
using System.Threading.Tasks;
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
    public sealed class PromiseConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Promise");

        private PromiseConstructor(Engine engine)
            : base(engine, _functionName, false)
        {
        }

        public PromisePrototype PrototypeObject { get; private set; }

        public static PromiseConstructor CreatePromiseConstructor(Engine engine)
        {
            var obj = new PromiseConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Set constructor is the Function prototype object
            obj.PrototypeObject = PromisePrototype.CreatePrototypeObject(engine, obj);
            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);
            obj._prototype = obj.PrototypeObject;

            return obj;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
                ["resolve"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "resolve", Resolve, 1), PropertyFlag.NonEnumerable)),
                ["reject"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "reject", Reject, 1), PropertyFlag.NonEnumerable)),
                ["all"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "all", All, 1), PropertyFlag.NonEnumerable)),
                ["race"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "race", Race, 1), PropertyFlag.NonEnumerable)),
            };
            SetProperties(properties);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Constructor Set requires 'new'");
            }

            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue receiver)
        {
            FunctionInstance promiseResolver = null;

            if (arguments.Length == 0 || (promiseResolver = arguments[0] as FunctionInstance) == null)
                ExceptionHelper.ThrowTypeError(_engine, $"Promise resolver {(arguments.Length >= 1 ? arguments[0].Type.ToString() : Undefined.ToString())} is not a function");

            var instance = new PromiseInstance(Engine, promiseResolver)
            {
                _prototype = PrototypeObject
            };

            instance.InvokePromiseResolver();

            return instance;
        }

        public PromiseInstance Resolve(JsValue thisRef, JsValue[] args) => PromiseInstance.CreateResolved(Engine, args.Length >= 1 ? args[0] : Undefined);
        public PromiseInstance Reject(JsValue thisRef, JsValue[] args) => PromiseInstance.CreateRejected(Engine, args.Length >= 1 ? args[0] : Undefined);

        public PromiseInstance All(JsValue thisRef, JsValue[] args)
        {
            if (args.Length == 0 || !(args[0] is ObjectInstance iteratorObj) || iteratorObj.HasProperty(GlobalSymbolRegistry.Iterator) == false)
                throw ExceptionHelper.ThrowTypeError(Engine, $"undefined is not iterable (cannot read property {GlobalSymbolRegistry.Iterator})");

            var iteratorCtor = iteratorObj.GetProperty(GlobalSymbolRegistry.Iterator).Value;
            var iterator = iteratorCtor.Invoke(iteratorObj, new JsValue[0]) as IteratorInstance;
            var items = iterator.CopyToArray();

            if (items.Length == 0)
                return Resolve(Undefined, new[] { Engine.Array.ConstructFast(0) });

            var chainedPromise = new PromiseInstance(Engine)
            {
                _prototype = Engine.Promise.PrototypeObject
            };

            var promises = items.OfType<PromiseInstance>().ToArray();

            if (promises.Length == 0)
            {
                Engine.QueuePromiseContinuation(() =>
                {
                    chainedPromise.Resolve(Undefined, new JsValue[] { Engine.Array.Construct(items) });
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

        public PromiseInstance Race(JsValue thisRef, JsValue[] args)
        {
            if (args.Length == 0 || !(args[0] is ObjectInstance iteratorObj) || iteratorObj.HasProperty(GlobalSymbolRegistry.Iterator) == false)
                throw ExceptionHelper.ThrowTypeError(Engine, $"undefined is not iterable (cannot read property {GlobalSymbolRegistry.Iterator})");

            var iteratorCtor = iteratorObj.GetProperty(GlobalSymbolRegistry.Iterator).Value;
            var iterator = iteratorCtor.Invoke(iteratorObj, new JsValue[0]) as IteratorInstance;
            var items = iterator.CopyToArray();

            var chainedPromise = new PromiseInstance(Engine)
            {
                _prototype = Engine.Promise.PrototypeObject
            };

            //  If no promises passed then the spec says to pend forever!
            if (items.Length == 0)
                return chainedPromise;

            for (var i = 0; i < items.Length; i++)
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
    }
}