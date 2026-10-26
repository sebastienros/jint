using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.AsyncGenerator;
using Jint.Native.Disposable;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// Runs https://tc39.es/ecma262/#sec-disposeresources for a statement that disposes resources as it ends: a block
/// (https://tc39.es/ecma262/#sec-block-runtime-semantics-evaluation step 6) or a <c>for</c> statement with a
/// <c>using</c> header (https://tc39.es/ecma262/#sec-runtime-semantics-forloopevaluation steps 8 and 13). Each await it
/// owes suspends the surrounding async function or async generator as an <c>await</c> expression does, with the
/// statement as the suspension point, and the replayed body re-enters that statement, which calls
/// <see cref="Resume"/>.
/// </summary>
internal static class StatementDisposal
{
    /// <summary>
    /// Advances <paramref name="step"/> until DisposeResources is done or the frame has suspended on an await it owes.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the frame suspended: the caller parks what it needs to resume and returns.
    /// </returns>
    internal static bool Drive(Engine engine, JintStatement suspensionPoint, DeclarativeEnvironment env, ref DisposeStepResult step)
    {
        while (!step.IsDone)
        {
            var pending = step.PendingPromise!;

            if (engine.ExecutionContext.AsyncFunction is { } asyncFn)
            {
                SuspendAsyncFunction(engine, asyncFn, suspensionPoint, pending);
                return true;
            }

            if (engine.ExecutionContext.AsyncGenerator is { } asyncGenerator)
            {
                if (SuspendAsyncGenerator(engine, asyncGenerator, suspensionPoint, pending, out var awaitError))
                {
                    return true;
                }

                // The await was abrupt before it could suspend: that throw completion is the dispose result's.
                step = env.ContinueDisposeResources(awaitError, awaitThrew: true);
                continue;
            }

            // Non-async context fallback. `await using` is syntactically only valid in async code, so this path is
            // reached only by unusual hosts. Sync wait.
            JsValue resolved;
            var threw = false;
            try
            {
                resolved = pending.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
            }
            catch (PromiseRejectedException e)
            {
                resolved = e.RejectedValue;
                threw = true;
            }

            step = env.ContinueDisposeResources(resolved, threw);
        }

        return false;
    }

    /// <summary>
    /// Takes the settled value of the await the frame resumed from, and advances the dispose of
    /// <paramref name="env"/> with it.
    /// </summary>
    internal static DisposeStepResult Resume(Engine engine, ISuspendable suspendable, DeclarativeEnvironment env)
    {
        // The IsResuming setter delegates to the async function's or the async generator's _isResuming, so this
        // single assignment clears both the interface flag and the underlying field.
        suspendable.IsResuming = false;

        JsValue awaitResult;
        bool awaitThrew;
        var asyncFn = engine.ExecutionContext.AsyncFunction;
        if (asyncFn is not null)
        {
            awaitResult = asyncFn._resumeValue ?? JsValue.Undefined;
            awaitThrew = asyncFn._resumeWithThrow;
            asyncFn._resumeValue = null;
            asyncFn._resumeWithThrow = false;
            asyncFn._lastAwaitNode = null;
        }
        else
        {
            // Nothing else suspends a statement mid-dispose (Drive), and the async generator's await continuation left
            // the settled value where an await expression reads it.
            var asyncGenerator = engine.ExecutionContext.AsyncGenerator!;
            awaitResult = asyncGenerator._nextValue ?? JsValue.Undefined;
            awaitThrew = asyncGenerator._resumeWithThrow;
            asyncGenerator._resumeWithThrow = false;
            asyncGenerator._lastYieldNode = null;
        }

        return env.ContinueDisposeResources(awaitResult, awaitThrew);
    }

    /// <summary>
    /// Mirror of <see cref="JintAwaitExpression.SuspendForAwait"/> for the dispose path: suspends the async function on
    /// the pending dispose promise, with handlers that resume the function.
    /// </summary>
    private static void SuspendAsyncFunction(Engine engine, AsyncFunctionInstance asyncFn, JintStatement suspensionPoint, JsValue pendingPromise)
    {
        var promise = pendingPromise as JsPromise
            ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pendingPromise);

        asyncFn._lastAwaitNode = suspensionPoint;
        asyncFn._state = AsyncFunctionState.SuspendedAwait;
        asyncFn._savedContext = engine.ExecutionContext;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = false;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = true;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
    }

    /// <summary>
    /// The async generator's counterpart of <see cref="SuspendAsyncFunction"/>: suspends the generator on
    /// <c>Await(<paramref name="pending"/>)</c> exactly as an <c>await</c> expression does
    /// (<see cref="JintAwaitExpression.SuspendAsyncGenerator"/>), with <paramref name="suspensionPoint"/> as the
    /// suspension point (sebastienros/jint#4355).
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the await is abrupt before it suspends: Await's PromiseResolve(%Promise%, value)
    /// reads a promise's <c>constructor</c>, which may throw, and <paramref name="error"/> is that throw.
    /// </returns>
    private static bool SuspendAsyncGenerator(
        Engine engine,
        AsyncGeneratorInstance asyncGenerator,
        JintStatement suspensionPoint,
        JsValue pending,
        [NotNullWhen(false)] out JsValue? error)
    {
        // A primitive, which can never be a thenable, is handed to the continuation directly.
        JsPromise? promise = null;
        if (pending is ObjectInstance)
        {
            try
            {
                promise = (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pending);
            }
            catch (JavaScriptException e)
            {
                error = e.Error;
                return false;
            }
        }

        JintAwaitExpression.SuspendAsyncGenerator(engine, asyncGenerator, suspensionPoint, promise, pending);
        error = null;
        return true;
    }
}
