using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public struct ResolvingFunctions
    {
        public ResolvingFunctions(Action<JsValue> resolveCallback, Action<JsValue> rejectCallback)
        {
            ResolveCallback = resolveCallback;
            RejectCallback = rejectCallback;
        }

        public Action<JsValue> ResolveCallback { get; private set; }
        public Action<JsValue> RejectCallback { get; private set; }
    }

    public class PromiseInstance : ObjectInstance
    {
        private ICallable _promiseExecutor;

        // private ResolvingFunctions _resolvingFunctions = null;
        private readonly TaskCompletionSource<JsValue> _tcs = new TaskCompletionSource<JsValue>();

        public Task<JsValue> Task => _tcs.Task;
        public PromiseState State { get; private set; }


        public static PromiseInstance FromExecutor(Engine engine, ICallable executor, ObjectInstance prototype)
        {
            var promise = new PromiseInstance(engine, executor)
            {
                _prototype = prototype,
                // _promiseExecutor = executor,
                // _resolvingFunctions = resolvingFunctions
            };
            
            return promise;
        }

        internal PromiseInstance(Engine engine) : base(engine, ObjectClass.Promise)
        {
            _prototype = engine.Promise._prototype;
        }

        internal PromiseInstance(Engine engine, ICallable promiseExecutor)
            : this(engine)
        {
            _promiseExecutor = promiseExecutor;
        }

        public PromiseInstance(Engine engine, Task wrappedTask)
            : this(engine)
        {
            wrappedTask.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    var returnValue = Undefined;

                    //  If the task returns a value
                    var taskType = t.GetType();
                    var resultProperty = taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);

                    if (resultProperty != null && resultProperty.PropertyType.Name != "VoidTaskResult")
                        returnValue = FromObject(_engine, resultProperty.GetValue(t));

                    _tcs.SetResult(returnValue);
                    return;
                }

                _tcs.SetException(new PromiseRejectedException(FromObject(Engine,
                    t.Exception?.InnerExceptions.FirstOrDefault() ??
                    new Exception("An unhandled exception was thrown"))));
            });
        }

        internal void InvokePromiseExecutor()
        {
            _promiseExecutor.Call(
                this,
                new JsValue[]
                {
                    new ClrFunctionInstance(_engine, "", Resolve, 1, PropertyFlag.Configurable),
                    new ClrFunctionInstance(_engine, "", Reject, 1, PropertyFlag.Configurable)
                });
        }

        internal JsValue Resolve(JsValue thisObj, JsValue[] arguments)
        {
            var result = arguments.At(0);

            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetResult(result);
                State = PromiseState.Resolved;
            }

            return this;
        }

        internal JsValue Reject(JsValue thisObj, JsValue[] arguments)
        {
            var result = arguments.At(0);

            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetException(new PromiseRejectedException(result));
                State = PromiseState.Rejected;
            }

            return this;
        }
    }
}