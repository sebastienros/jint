using System.Reflection;
using System.Threading.Tasks;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public class PromiseInstance : ObjectInstance
    {
        private readonly FunctionInstance _promiseResolver;
        private readonly TaskCompletionSource<JsValue> _tcs = new TaskCompletionSource<JsValue>();

        public Task<JsValue> Task => _tcs.Task;
        public PromiseState State { get; private set; }

        internal PromiseInstance(Engine engine) : base(engine, ObjectClass.Promise)
        {

        }
        
        public PromiseInstance(Engine engine, FunctionInstance promiseResolver)
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

                    if (resultProperty != null)
                        returnValue = FromObject(_engine, resultProperty.GetValue(t));
                    
                    _tcs.SetResult(returnValue);
                }
            });
        }

        public static PromiseInstance CreateResolved(Engine engine, JsValue result)
        {
            var resolved = new PromiseInstance(engine)
            {
                _prototype = engine.Promise.PrototypeObject
            };

            resolved._tcs.SetResult(result);
            resolved.State = PromiseState.Resolved;

            return resolved;
        }

        public static PromiseInstance CreateRejected(Engine engine, JsValue result)
        {
            var rejected = new PromiseInstance(engine)
            {
                _prototype = engine.Promise.PrototypeObject
            };

            rejected._tcs.SetException(new PromiseRejectedException(result));
            rejected.State = PromiseState.Rejected;

            return rejected;
        }

        internal void InvokePromiseResolver()
        {
            _promiseResolver.Invoke(new ClrFunctionInstance(_engine, "", Resolve, 1), new ClrFunctionInstance(_engine, "", Reject, 1));
        }

        internal JsValue Resolve(JsValue thisValue, JsValue[] args)
        {
            var result = Undefined;

            if (args.Length >= 1)
                result = args[0];

            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetResult(result);
                State = PromiseState.Resolved;
            }

            return Undefined;
        }

        internal JsValue Reject(JsValue thisValue, JsValue[] args)
        {
            var result = Undefined;

            if (args.Length >= 1)
                result = args[0];

            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetException(new PromiseRejectedException(result));
                State = PromiseState.Rejected;
            }

            return Undefined;
        }
    }
}
