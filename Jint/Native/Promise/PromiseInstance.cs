using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly List<TaskCompletionSource<JsValue>> _awaitingTasks = new();

        private static int count = 0;
#pragma warning disable 414
        private int id = 0;
#pragma warning restore 414

        internal PromiseState State { get; private set; }

        // valid only in settled state (Fulfilled or Rejected) 
        internal JsValue Value { get; private set; }

        internal List<PromiseReaction> PromiseRejectReactions = new();
        internal List<PromiseReaction> PromiseFulfillReactions = new();

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

        private PromiseInstance(Engine engine) : base(engine, ObjectClass.Promise)
        {
            id = count;
            count += 1;
            _prototype = engine.Promise._prototype;
        }

        internal void InvokePromiseExecutor(ICallable promiseExecutor)
        {
            var (resolve, reject) = CreateResolvingFunctions();
            promiseExecutor.Call(Undefined, new JsValue[] {resolve, reject});
        }

        // https://tc39.es/ecma262/#sec-createresolvingfunctions
        // Note that functions capture over alreadyResolved
        // that does imply that the same promise can be resolved twice but with different resolving functions
        internal (FunctionInstance resolve, FunctionInstance reject) CreateResolvingFunctions()
        {
            var alreadyResolved = false;
            var resolve = new ClrFunctionInstance(_engine, "", (thisObj, args) =>
            {
                if (alreadyResolved)
                {
                    return Undefined;
                }
                alreadyResolved = true;
                return Resolve(thisObj, args);
            }, 1, PropertyFlag.Configurable);
            
            var reject = new ClrFunctionInstance(_engine, "", (thisObj, args) =>
            {
                if (alreadyResolved)
                {
                    return Undefined;
                }
                alreadyResolved = true;
                return Reject(thisObj, args);
            }, 1, PropertyFlag.Configurable);

            return (resolve, reject);
        }

        // https://tc39.es/ecma262/#sec-promise-resolve-functions
        private JsValue Resolve(JsValue thisObj, JsValue[] arguments)
        {
            // Note that alreadyResolved logic lives in CreateResolvingFunctions method
            
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

            if (thenProp is not ICallable thenMethod)
            {
                return FulfillPromise(result);
            }

            _engine.QueuePromiseContinuation(
                PromiseOperations.NewPromiseResolveThenableJob(this, resultObj, thenMethod));

            return Undefined;
        }

        // https://tc39.es/ecma262/#sec-promise-reject-functions
        private JsValue Reject(JsValue thisObj, JsValue[] arguments)
        {
            // Note that alreadyResolved logic lives in CreateResolvingFunctions method
            
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

            Settle(PromiseState.Rejected, reason);

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

            Settle(PromiseState.Fulfilled, result);
            var reactions = PromiseFulfillReactions;
            PromiseFulfillReactions = new List<PromiseReaction>();
            PromiseRejectReactions.Clear();

            return PromiseOperations.TriggerPromiseReactions(_engine, reactions, result);
        }

        private void Settle(PromiseState state, JsValue result)
        {
            State = state;
            Value = result;

            // TODO should be just a single task perhaps? 
            // if not should the list be copied first?
            // I guess not because otherwise the task will be immediately resolved
            foreach (var taskCompletionSource in _awaitingTasks)
            {
                switch (state)
                {
                    case PromiseState.Pending:
                        throw new Exception("Promise should be in Settled state");
                    case PromiseState.Fulfilled:
                        taskCompletionSource.SetResult(result);
                        break;
                    case PromiseState.Rejected:
                        taskCompletionSource.SetException(new PromiseRejectedException(result));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state), state, null);
                }
            }
            _awaitingTasks.Clear();
        }


        internal Task<JsValue> ToTask()
        {
            Task<JsValue> AddAwaitedTask()
            {
                var task = new TaskCompletionSource<JsValue>();
                _awaitingTasks.Add(task);
                return task.Task;
            }

            return State switch
            {
                PromiseState.Pending => AddAwaitedTask(),
                PromiseState.Fulfilled => Task.FromResult(Value),
                PromiseState.Rejected => Task.FromException<JsValue>(new PromiseRejectedException(Value)),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}