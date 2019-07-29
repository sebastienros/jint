using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public class PromiseInstance : ObjectInstance
    {
        private readonly FunctionInstance _promiseResolver;
        private readonly TaskCompletionSource<JsValue> _tcs = new TaskCompletionSource<JsValue>();

        public Task<JsValue> Task => _tcs.Task;
        public PromiseState State { get; private set; }

        public PromiseInstance(Engine engine, FunctionInstance promiseResolver)
            : base(engine, ObjectClass.Promise)
        {
            _promiseResolver = promiseResolver;
        }

        internal void InvokePromiseResolver()
        {
            _promiseResolver.Invoke(new ClrFunctionInstance(_engine, "", Resolve, 1), new ClrFunctionInstance(_engine, "", Reject, 1));
        }

        private JsValue Resolve(JsValue thisValue, JsValue[] args)
        {
            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetResult(args[0]);
                State = PromiseState.Resolved;
            }

            return JsValue.Undefined;
        }

        private JsValue Reject(JsValue thisValue, JsValue[] args)
        {
            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Resolving)
            {
                _tcs.SetException(new PromiseRejectedException());
                State = PromiseState.Rejected;
            }

            return JsValue.Undefined;
        }
    }
}
