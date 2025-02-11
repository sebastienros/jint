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
    private static Action NewPromiseReactionJob(PromiseReaction reaction, JsValue value)
    {
        return () =>
        {
            var promiseCapability = reaction.Capability;

            if (reaction.Handler is ICallable handler)
            {
                try
                {
                    var result = handler.Call(JsValue.Undefined, value);
                    promiseCapability.Resolve.Call(JsValue.Undefined, result);
                }
                catch (JavaScriptException e)
                {
                    promiseCapability.Reject.Call(JsValue.Undefined, e.Error);
                }
            }
            else
            {
                switch (reaction.Type)
                {
                    case ReactionType.Fulfill:
                        promiseCapability.Resolve.Call(JsValue.Undefined, value);
                        break;

                    case ReactionType.Reject:
                        promiseCapability.Reject.Call(JsValue.Undefined, value);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(reaction), "Unknown reaction type");
                }
            }
        };
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
    internal static JsValue TriggerPromiseReactions(Engine engine, List<PromiseReaction> reactions, JsValue result)
    {
        foreach (var reaction in reactions)
        {
            engine.AddToEventLoop(NewPromiseReactionJob(reaction, result));
        }

        return JsValue.Undefined;
    }

    // https://tc39.es/ecma262/#sec-performpromisethen
    internal static JsValue PerformPromiseThen(
        Engine engine,
        JsPromise promise,
        JsValue onFulfilled,
        JsValue onRejected,
        PromiseCapability resultCapability)
    {
        var fulfilReaction = new PromiseReaction(ReactionType.Fulfill, resultCapability, onFulfilled);
        var rejectReaction = new PromiseReaction(ReactionType.Reject, resultCapability, onRejected);

        switch (promise.State)
        {
            case PromiseState.Pending:
                promise.PromiseFulfillReactions.Add(fulfilReaction);
                promise.PromiseRejectReactions.Add(rejectReaction);
                break;

            case PromiseState.Fulfilled:
                engine.AddToEventLoop(NewPromiseReactionJob(fulfilReaction, promise.Value));

                break;
            case PromiseState.Rejected:
                engine.AddToEventLoop(NewPromiseReactionJob(rejectReaction, promise.Value));

                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException();
                break;
        }

        //https://tc39.es/ecma262/#sec-performpromisethen
        //...
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
}
