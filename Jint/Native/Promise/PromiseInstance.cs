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
    public class PromiseInstance : ObjectInstance
    {
        private readonly ICallable _promiseResolver;
        private readonly TaskCompletionSource<JsValue> _tcs = new TaskCompletionSource<JsValue>();

        public Task<JsValue> Task => _tcs.Task;
        public PromiseState State { get; private set; }

        internal PromiseInstance(Engine engine) : base(engine, ObjectClass.Promise)
        {
            _prototype = engine.Promise._prototype;
        }

        public PromiseInstance(Engine engine, ICallable promiseResolver)
            : this(engine)
        {
            _promiseResolver = promiseResolver;
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

                _tcs.SetException(new PromiseRejectedException(FromObject(Engine, t.Exception?.InnerExceptions.FirstOrDefault() ?? new Exception("An unhandled exception was thrown"))));
            });
        }

        internal void InvokePromiseResolver()
        {
            _promiseResolver.Call(
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
