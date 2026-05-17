using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Disposable;

/// <summary>
/// Drives <see cref="DisposeCapability"/>'s state machine for call sites where the
/// async function/generator body has already finished — so we don't need to suspend
/// the function itself, only defer settlement of its return promise. Each
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
        var step = env.BeginDisposeResources(initial);
        DriveAsync(engine, env, step, continueWith);
    }

    private static void DriveAsync(
        Engine engine,
        Environment env,
        DisposeStepResult step,
        Action<Completion> continueWith)
    {
        while (!step.IsDone)
        {
            var pending = step.PendingPromise!;
            var promise = pending as JsPromise
                ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pending);

            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var next = env.ContinueDisposeResources(args.At(0), awaitThrew: false);
                DriveAsync(engine, env, next, continueWith);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var next = env.ContinueDisposeResources(args.At(0), awaitThrew: true);
                DriveAsync(engine, env, next, continueWith);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
            return; // suspended — onFulfilled/onRejected will continue the drive
        }

        continueWith(step.CompletedResult);
    }
}
