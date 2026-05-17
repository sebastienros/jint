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
            var pending = step.PendingPromise!;
            var promise = pending as JsPromise
                ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pending);

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
