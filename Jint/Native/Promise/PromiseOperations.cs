using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Promise;

internal static class PromiseOperations
{
    // https://tc39.es/ecma262/#sec-newpromisereactionjob
    //
    // 1. Let job be a new Job Abstract Closure with no parameters that captures reaction and argument and performs the following steps when called:
    //      a. Assert: reaction is a PromiseReaction Record.
    //      b. Let promiseCapability be reaction.[[Capability]].
    //      c. Let type be reaction.[[Type]].
    //      d. Let handler be reaction.[[Handler]].
    //      e. If handler is empty, then
    //          i. If type is Fulfill, let handlerResult be NormalCompletion(argument).
    //          ii. Else,
    //              1. Assert: type is Reject.
    //              2. Let handlerResult be ThrowCompletion(argument).
    //      f. Else, let handlerResult be HostCallJobCallback(handler, undefined, « argument »).
    //      g. If promiseCapability is undefined, then
    //          i. Assert: handlerResult is not an abrupt completion.
    //          ii. Return NormalCompletion(empty).
    //      h. Assert: promiseCapability is a PromiseCapability Record.
    //          i. If handlerResult is an abrupt completion, then
    //          i. Let status be Call(promiseCapability.[[Reject]], undefined, « handlerResult.[[Value]] »).
    //      j. Else,
    //          i. Let status be Call(promiseCapability.[[Resolve]], undefined, « handlerResult.[[Value]] »).
    //      k. Return Completion(status).
    //
    // The job's state is carried by the queued (reaction, argument) pair itself instead of a
    // captured closure — see EventLoopJob.
    internal static void RunReactionJob(Engine engine, PromiseReaction reaction, JsValue value)
    {
        var promiseCapability = reaction.Capability;

        if (reaction.Continuation is { } continuation)
        {
            // Engine-internal handler (e.g. an await continuation): invoked directly, no JS
            // function object involved. These reactions never carry a capability, so mirror
            // the JS-handler path below: a JavaScriptException that escapes has no reject
            // target and is dropped.
            try
            {
                continuation.Invoke(engine, value, reaction.Type);
            }
            catch (JavaScriptException)
            {
            }
        }
        else if (reaction.Handler is ICallable handler)
        {
            try
            {
                var result = handler.Call(JsValue.Undefined, value);
                // If promiseCapability is undefined, just return (spec step g)
                promiseCapability?.Resolve(result);
            }
            catch (JavaScriptException e)
            {
                // If promiseCapability is undefined, this is an assertion failure per spec
                // but we need to handle it gracefully
                promiseCapability?.Reject(e.Error);
            }
        }
        else
        {
            // If no handler, capability must be defined per spec
            switch (reaction.Type)
            {
                case ReactionType.Fulfill:
                    promiseCapability?.Resolve(value);
                    break;

                case ReactionType.Reject:
                    promiseCapability?.Reject(value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reaction), "Unknown reaction type");
            }
        }
    }

    // https://tc39.es/ecma262/#sec-newpromiseresolvethenablejob
    // The abstract operation NewPromiseResolveThenableJob takes arguments promiseToResolve, thenable, and then. It performs the following steps when called:
    //
    // 1. Let job be a new Job Abstract Closure with no parameters that captures promiseToResolve,
    // thenable, and then and performs the following steps when called:

    //  a. Let resolvingFunctions be CreateResolvingFunctions(promiseToResolve).
    //  b. Let thenCallResult be HostCallJobCallback(then, thenable, « resolvingFunctions.[[Resolve]], resolvingFunctions.[[Reject]] »).
    //  c. If thenCallResult is an abrupt completion, then
    //      i. Let status be Call(resolvingFunctions.[[Reject]], undefined, « thenCallResult.[[Value]] »).
    //      ii. Return Completion(status).
    //  d. Return Completion(thenCallResult).
    // .....Realm stuff....
    // 6. Return the Record { [[Job]]: job, [[Realm]]: thenRealm }.
    internal static Action NewPromiseResolveThenableJob(JsPromise promise, ObjectInstance thenable, ICallable thenMethod)
    {
        return () =>
        {
            var (resolve, reject) = promise.CreateResolvingFunctions();

            try
            {
                thenMethod.Call(thenable, resolve as JsValue, reject);
            }
            catch (JavaScriptException e)
            {
                reject.Call(JsValue.Undefined, [e.Error]);
            }
        };
    }

    // https://tc39.es/ecma262/#sec-triggerpromisereactions
    //
    // 1. For each element reaction of reactions, do
    // a. Let job be NewPromiseReactionJob(reaction, argument).
    // b. Perform HostEnqueuePromiseJob(job.[[Job]], job.[[Realm]]).
    // 2. Return undefined.
    internal static JsValue TriggerPromiseReactions(Engine engine, List<PromiseReaction>? reactions, JsValue result)
    {
        if (reactions is not null)
        {
            foreach (var reaction in reactions)
            {
                engine.AddToEventLoop(reaction, result);
            }
        }

        return JsValue.Undefined;
    }

    // https://tc39.es/ecma262/#sec-performpromisethen
    internal static JsValue PerformPromiseThen(
        Engine engine,
        JsPromise promise,
        JsValue onFulfilled,
        JsValue onRejected,
        PromiseCapability? resultCapability)
    {
        var wasAlreadyHandled = promise.PromiseIsHandled;

        switch (promise.State)
        {
            case PromiseState.Pending:
                (promise.PromiseFulfillReactions ??= new List<PromiseReaction>()).Add(new PromiseReaction(ReactionType.Fulfill, resultCapability, onFulfilled));
                (promise.PromiseRejectReactions ??= new List<PromiseReaction>()).Add(new PromiseReaction(ReactionType.Reject, resultCapability, onRejected));
                break;

            case PromiseState.Fulfilled:
                engine.AddToEventLoop(new PromiseReaction(ReactionType.Fulfill, resultCapability, onFulfilled), promise.Value);

                break;
            case PromiseState.Rejected:
                engine.AddToEventLoop(new PromiseReaction(ReactionType.Reject, resultCapability, onRejected), promise.Value);

                break;
            default:
                Throw.ArgumentOutOfRangeException();
                break;
        }

        // https://tc39.es/ecma262/#sec-performpromisethen
        // 12. Set promise.[[PromiseIsHandled]] to true.
        promise.PromiseIsHandled = true;

        // If this promise was previously rejected without a handler, notify the host
        // that it's now been handled (HostPromiseRejectionTracker "handle" operation).
        if (promise.State == PromiseState.Rejected && !wasAlreadyHandled)
        {
            engine._host.HostPromiseRejectionTracker(promise, PromiseRejectionOperation.Handle);
        }

        //13. If resultCapability is undefined, then
        //      a. Return undefined
        //14. Else
        //      a. Return resultCapability.[[Promise]]
        if (resultCapability is null)
        {
            return JsValue.Undefined;
        }

        return resultCapability.PromiseInstance;
    }

    /// <summary>
    /// PerformPromiseThen for engine-internal continuations (the Await abstract operation's
    /// steps 3-10): both reactions carry <paramref name="continuation"/> in place of the spec's
    /// onFulfilled/onRejected closures — those closures are unobservable from user code, so no
    /// JS function objects are materialized — and there is no result capability.
    /// https://tc39.es/ecma262/#await
    /// </summary>
    internal static void PerformPromiseThen(Engine engine, JsPromise promise, IPromiseContinuation continuation)
    {
        var wasAlreadyHandled = promise.PromiseIsHandled;

        switch (promise.State)
        {
            case PromiseState.Pending:
                (promise.PromiseFulfillReactions ??= new List<PromiseReaction>()).Add(new PromiseReaction(ReactionType.Fulfill, Capability: null, Handler: null, continuation));
                (promise.PromiseRejectReactions ??= new List<PromiseReaction>()).Add(new PromiseReaction(ReactionType.Reject, Capability: null, Handler: null, continuation));
                break;

            case PromiseState.Fulfilled:
                engine.AddToEventLoop(new PromiseReaction(ReactionType.Fulfill, Capability: null, Handler: null, continuation), promise.Value);
                break;

            case PromiseState.Rejected:
                engine.AddToEventLoop(new PromiseReaction(ReactionType.Reject, Capability: null, Handler: null, continuation), promise.Value);
                break;

            default:
                Throw.ArgumentOutOfRangeException();
                break;
        }

        // 12. Set promise.[[PromiseIsHandled]] to true.
        promise.PromiseIsHandled = true;

        if (promise.State == PromiseState.Rejected && !wasAlreadyHandled)
        {
            engine._host.HostPromiseRejectionTracker(promise, PromiseRejectionOperation.Handle);
        }
    }
}
