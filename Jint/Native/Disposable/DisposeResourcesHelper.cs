using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Disposable;

/// <summary>
/// Drives <see cref="DisposeCapability"/>'s state machine for call sites where the
/// async function/generator/module body has already finished — so we don't need to
/// suspend the function itself, only defer settlement of its return promise. Each
/// state-machine Suspend is chained via <c>Promise.then</c>, and the caller-provided
/// <c>continueWith</c> callback fires when the full dispose chain has completed.
/// </summary>
internal static class DisposeResourcesHelper
{
    public static void DisposeAndThen(
        Engine engine,
        Environment env,
        Completion initial,
        Action<Completion> continueWith)
    {
        Drive(engine, env.BeginDisposeResources(initial), continueWith, env.ContinueDisposeResources);
    }

    public static void DisposeAndThen(
        Engine engine,
        DisposeCapability capability,
        Completion initial,
        Action<Completion> continueWith)
    {
        Drive(engine, capability.BeginDispose(initial), continueWith, capability.ContinueDispose);
    }

    private static void Drive(
        Engine engine,
        DisposeStepResult step,
        Action<Completion> continueWith,
        Func<JsValue, bool, DisposeStepResult> continueDispose)
    {
        while (!step.IsDone)
        {
            // Await, step 2: PromiseResolve(%Promise%, value) reads a promise's constructor, which may throw. That is an
            // abrupt await, whose throw completion is the dispose result's (DisposeResources, step 4.e.iii).
            JsPromise promise;
            try
            {
                promise = PromiseConstructor.ResolveForAwaitToPromise(engine, step.PendingPromise!);
            }
            catch (JavaScriptException e)
            {
                step = continueDispose(e.Error, true);
                continue;
            }

            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var next = continueDispose(args.At(0), false);
                Drive(engine, next, continueWith, continueDispose);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var next = continueDispose(args.At(0), true);
                Drive(engine, next, continueWith, continueDispose);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
            return; // suspended — onFulfilled/onRejected will continue the drive
        }

        continueWith(step.CompletedResult);
    }
}
