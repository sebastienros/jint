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
            var properties = new PropertyDictionary(15, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_promiseConstructor, PropertyFlag.NonEnumerable),
                ["then"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "then", Then, 1, PropertyFlag.Configurable), true, false, true),
                ["catch"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "catch", Catch, 1, PropertyFlag.Configurable), true, false, true)
            };
            SetProperties(properties);
        }
        
        public JsValue Then(JsValue thisValue, JsValue[] args)
        {
            if (!(thisValue is PromiseInstance promise))
                throw ExceptionHelper.ThrowTypeError(_engine, "Method Promise.prototype.then called on incompatible receiver");
            
            promise.Task.ContinueWith(t =>
            {
                if (args.Length == 0 || !(args[0] is FunctionInstance thenCallback))
                    return;

                _engine.QueuePromiseContinuation(() => thenCallback.Invoke(t.Result));

            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return promise;
        }

        public JsValue Catch(JsValue thisValue, JsValue[] args)
        {
            if (!(thisValue is PromiseInstance promise))
                throw ExceptionHelper.ThrowTypeError(_engine, "Method Promise.prototype.catch called on incompatible receiver");

            promise.Task.ContinueWith(t =>
            {
                if (args.Length == 0 || !(args[0] is FunctionInstance catchCallback))
                    return;

                _engine.QueuePromiseContinuation(() => catchCallback.Invoke(Undefined));

            }, TaskContinuationOptions.OnlyOnFaulted);

            return promise;
        }

    }
}