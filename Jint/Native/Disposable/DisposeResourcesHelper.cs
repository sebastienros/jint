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
/// <para>
/// The <c>realm</c> a caller passes is that of the function, generator, module or stack whose resources are disposed:
/// each Await resolves against its <c>%Promise%</c>. Only the first step can run inside that body's execution
/// context; every later one runs in the reaction job of the await before it, where the running realm is whatever
/// that job found.
/// </para>
/// </summary>
internal static class DisposeResourcesHelper
{
    public static void DisposeAndThen(
        Engine engine,
        Realm realm,
        Environment env,
        Completion initial,
        Action<Completion> continueWith)
    {
        Drive(engine, realm.Intrinsics.Promise, env.BeginDisposeResources(initial), continueWith, env.ContinueDisposeResources);
    }

    public static void DisposeAndThen(
        Engine engine,
        Realm realm,
        DisposeCapability capability,
        Completion initial,
        Action<Completion> continueWith)
    {
        Drive(engine, realm.Intrinsics.Promise, capability.BeginDispose(initial), continueWith, capability.ContinueDispose);
    }

    private static void Drive(
        Engine engine,
        PromiseConstructor promiseConstructor,
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
                promise = promiseConstructor.ResolveForAwaitToPromise(step.PendingPromise!);
            }
            catch (JavaScriptException e)
            {
                step = continueDispose(e.Error, true);
                continue;
            }

            var onFulfilled = new ClrFunction(engine, "", (_, args) =>
            {
                var next = continueDispose(args.At(0), false);
                Drive(engine, promiseConstructor, next, continueWith, continueDispose);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            var onRejected = new ClrFunction(engine, "", (_, args) =>
            {
                var next = continueDispose(args.At(0), true);
                Drive(engine, promiseConstructor, next, continueWith, continueDispose);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);

            PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
            return; // suspended — onFulfilled/onRejected will continue the drive
        }

        continueWith(step.CompletedResult);
    }
}
