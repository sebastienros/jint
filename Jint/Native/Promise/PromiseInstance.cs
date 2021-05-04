using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    internal enum ReactionType
    {
        Fulfill,
        Reject
    }

    internal struct PromiseReaction
    {
        public PromiseCapability Capability { get; set; }
        public ReactionType Type { get; set; }
        public JsValue Handler { get; set; }
    }

    public class PromiseInstance : ObjectInstance
    {
        private readonly TaskCompletionSource<JsValue> _tcs = new TaskCompletionSource<JsValue>();

        public Task<JsValue> Task => _tcs.Task;
        internal PromiseState State { get; private set; }

        // valid only in settled state (Fulfilled or Rejected) 
        internal JsValue Value { get; private set; }

        internal List<PromiseReaction> PromiseRejectReactions = new();
        internal List<PromiseReaction> PromiseFulfillReactions = new();

        // Note that this should be a prop attached to the resolve/reject functions
        private bool AlreadyResolved { get; set; }

        public static PromiseInstance New(Engine engine, ObjectInstance prototype)
        {
            var promise = new PromiseInstance(engine)
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

        internal void InvokePromiseExecutor(ICallable promiseExecutor)
        {
            var (resolve, reject) = CreateResolvingFunctions();
            promiseExecutor.Call(Undefined, new JsValue[] {resolve, reject});
        }

        internal (FunctionInstance resolve, FunctionInstance reject) CreateResolvingFunctions()
        {
            var resolve = new ClrFunctionInstance(_engine, "", Resolve, 1, PropertyFlag.Configurable);
            var reject = new ClrFunctionInstance(_engine, "", Reject, 1, PropertyFlag.Configurable);

            return (resolve, reject);
        }

        // https://tc39.es/ecma262/#sec-promise-resolve-functions
        internal JsValue Resolve(JsValue thisObj, JsValue[] arguments)
        {
            if (AlreadyResolved)
            {
                return Undefined;
            }

            AlreadyResolved = true;

            var result = arguments.At(0);

            if (result == this)
            {
                ExceptionHelper.ThrowTypeError(_engine,
                    "Cannot resolve Promise with itself");
                return Undefined;
            }

            if (result is not ObjectInstance resultObj)
            {
                return FulfillPromise(result);
            }

            JsValue thenProp;
            try
            {
                thenProp = resultObj.Get("then");
            }
            catch (JavaScriptException e)
            {
                return RejectPromise(e.Error);
            }

            //  Only first resolve/reject is actioned.  Further calls are invalid and ignored
            if (State == PromiseState.Pending)
            {
                _tcs.SetResult(result);
                State = PromiseState.Fulfilled;
            }

            if (thenProp is not ICallable thenMethod)
            {
                return FulfillPromise(result);
            }

            _engine.QueuePromiseContinuation(
                PromiseOperations.NewPromiseResolveThenableJob(this, resultObj, thenMethod));

            return Undefined;
        }
        
        // https://tc39.es/ecma262/#sec-promise-reject-functions
        internal JsValue Reject(JsValue thisObj, JsValue[] arguments)
        {
            if (AlreadyResolved)
            {
                return Undefined;
            }

            AlreadyResolved = true;
            
            var reason = arguments.At(0);

            return RejectPromise(reason);
        }


        // https://tc39.es/ecma262/#sec-rejectpromise
        // 1. Assert: The value of promise.[[PromiseState]] is pending.
        // 2. Let reactions be promise.[[PromiseRejectReactions]].
        // 3. Set promise.[[PromiseResult]] to reason.
        // 4. Set promise.[[PromiseFulfillReactions]] to undefined.
        // 5. Set promise.[[PromiseRejectReactions]] to undefined.
        // 6. Set promise.[[PromiseState]] to rejected.
        // 7. If promise.[[PromiseIsHandled]] is false, perform HostPromiseRejectionTracker(promise, "reject").
        // 8. Return TriggerPromiseReactions(reactions, reason).
        private JsValue RejectPromise(JsValue reason)
        {
            if (State != PromiseState.Pending)
            {
                // TODO what is a proper way to assert?
                throw new Exception("Promise should be in Pending state");
            }

            State = PromiseState.Rejected;
            Value = reason;
            
            var reactions = PromiseRejectReactions;
            PromiseRejectReactions = new List<PromiseReaction>();
            PromiseFulfillReactions.Clear();

            // Note that this part is skipped because there is no tracking yet
            // 7. If promise.[[PromiseIsHandled]] is false, perform HostPromiseRejectionTracker(promise, "reject").

            return PromiseOperations.TriggerPromiseReactions(_engine, reactions, reason);
        }

        // https://tc39.es/ecma262/#sec-fulfillpromise
        private JsValue FulfillPromise(JsValue result)
        {
            if (State != PromiseState.Pending)
            {
                // TODO what is a proper way to assert?
                throw new Exception("Promise should be in Pending state");
            }

            State = PromiseState.Fulfilled;
            Value = result;
            var reactions = PromiseFulfillReactions;
            PromiseFulfillReactions = new List<PromiseReaction>();
            PromiseRejectReactions.Clear();

            return PromiseOperations.TriggerPromiseReactions(_engine, reactions, result);
        }


       
    }
}