using System.Diagnostics.CodeAnalysis;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asynciteratorclose, the parts every caller shares: steps 5 to 8, which decide what the
/// close raises once what <c>return</c> answered has settled (<see cref="AsyncIteratorCloseError"/>), and the whole
/// operation for a built-in, which has no frame to suspend and awaits through a promise reaction instead
/// (<see cref="AsyncIteratorClose(CompletionType, Action{JsValue})"/>). Steps 3 to 4.c are
/// <see cref="TryStartAsyncClose(out JsValue)"/>, and the native release once the answer settles is
/// <see cref="CloseNativeStateAfterReturn"/>.
/// </summary>
internal abstract partial class IteratorInstance
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorclose steps 5 to 8, once step 4.d's Await of what <c>return</c> answered
    /// has settled to <paramref name="settled"/>, a rejection when <paramref name="rejected"/>: the error the close raises
    /// in place of <paramref name="completion"/>, or null when that completion stands. A throw completion always stands
    /// (step 5). Otherwise a rejection is raised as it is (step 6), and an answer that is not an object is a
    /// <c>TypeError</c> created in <paramref name="realm"/> (step 7). Reading or calling <c>return</c>, or the Await's
    /// PromiseResolve, throwing is passed as a rejection: innerResult is a throw completion either way.
    /// </summary>
    internal static JsValue? AsyncIteratorCloseError(Realm realm, CompletionType completion, JsValue settled, bool rejected)
    {
        // Step 5: If completion is a throw completion, return ? completion.
        if (completion == CompletionType.Throw)
        {
            return null;
        }

        // Step 6: If innerResult is a throw completion, return ? innerResult.
        if (rejected)
        {
            return settled;
        }

        // Step 7: If innerResult.[[Value]] is not an Object, throw a TypeError exception. The check is against the
        // settled value, so Promise.resolve(42) fails it exactly as 42 does.
        if (!settled.IsObject())
        {
            return realm.Intrinsics.TypeError.Construct("Iterator returned non-object");
        }

        // Step 8: Return ? completion.
        return null;
    }

    /// <summary>
    /// AsyncIteratorClose(this, <paramref name="completion"/>) for a built-in: steps 3 to 4.c here, step 4.d's Await of
    /// what <c>return</c> answered through a promise reaction, and steps 5 to 8 once it settles.
    /// <paramref name="onClosed"/> receives the error the close raises, or null when the completion stands, and runs at
    /// once when there is nothing to await: no <c>return</c> (step 4.b), or reading, calling or awaiting it threw.
    /// <para>
    /// A <see cref="JavaScriptException"/> from starting the close is innerResult's throw completion. Under a throw
    /// completion a host exception is discarded with it, as <see cref="Close"/> discards one then, but never one that
    /// ends the run (<see cref="Throw.MustPropagateHostException"/>): a constraint that trips inside <c>return</c> still
    /// stops the engine. Under any other completion a host exception, such as a host's <c>Dispose</c> throwing in the
    /// native release, is what the close raises when the engine's interop exception handler converts it
    /// (<see cref="TryConvertHostException"/>), and otherwise propagates as it is.
    /// </para>
    /// <para>
    /// A built-in iterator's native state goes as the other asynchronous closes release it: at once without a
    /// <c>return</c> or when starting the close throws, and otherwise once the answer has settled, told a throw
    /// completion whenever one leaves the close (<see cref="CloseNativeStateAfterReturn"/>).
    /// </para>
    /// </summary>
    internal void AsyncIteratorClose(CompletionType completion, Action<JsValue?> onClosed)
    {
        var engine = _engine;
        var realm = engine.Realm;

        // Steps 3 and 4.a-4.c. onClosed runs outside the try, so that nothing it raises is taken for the close's own.
        bool hasReturn;
        JsValue innerResult;
        try
        {
            hasReturn = TryStartAsyncClose(out innerResult);
        }
        catch (Exception e) when (e is JavaScriptException || !Throw.MustPropagateHostException(e))
        {
            if (!TryGetCloseError(engine, completion, e, out var error))
            {
                throw;
            }

            onClosed(AsyncIteratorCloseError(realm, completion, error, rejected: true));
            return;
        }

        if (!hasReturn)
        {
            // Step 4.b: If return is undefined, return ? completion.
            onClosed(null);
            return;
        }

        // Step 4.d: Await(innerResult). Its PromiseResolve(%Promise%, value) reads a promise's constructor, which may
        // throw, and that is an abrupt innerResult too.
        JsPromise promise;
        try
        {
            promise = PromiseConstructor.ResolveForAwaitToPromise(engine, innerResult);
        }
        catch (Exception e) when (e is JavaScriptException || !Throw.MustPropagateHostException(e))
        {
            CloseNativeStateAfterReturn(CompletionType.Throw);
            if (!TryGetCloseError(engine, completion, e, out var error))
            {
                throw;
            }

            onClosed(AsyncIteratorCloseError(realm, completion, error, rejected: true));
            return;
        }

        PromiseOperations.PerformPromiseThen(engine, promise, new AsyncCloseContinuation(this, completion, realm, onClosed));
    }

    /// <summary>
    /// The value an exception out of one of the close's own steps stands for, as innerResult's throw completion: a
    /// <see cref="JavaScriptException"/>'s error; nothing that matters under a throw completion, which wins anyway; and
    /// otherwise a host exception's conversion (<see cref="TryConvertHostException"/>). False when the engine's interop
    /// exception handler declines it, and the caller lets it propagate.
    /// </summary>
    private static bool TryGetCloseError(Engine engine, CompletionType completion, Exception exception, out JsValue error)
    {
        if (exception is JavaScriptException javaScriptException)
        {
            error = javaScriptException.Error;
            return true;
        }

        if (completion == CompletionType.Throw)
        {
            error = Undefined;
            return true;
        }

        if (TryConvertHostException(engine, exception, out var converted))
        {
            error = converted;
            return true;
        }

        error = Undefined;
        return false;
    }

    /// <summary>
    /// A host exception as the JavaScript error it becomes when the engine's interop exception handler converts it
    /// (<see cref="Options.InteropOptions.ExceptionHandler"/>, which <c>CatchClrExceptions</c> installs): what a
    /// <see cref="Runtime.Interop.ClrFunction"/> does with one on its way out. A built-in that runs in a promise reaction
    /// with no capability to reject has to convert it itself and reject its own promise, or the conversion reaches no
    /// one and that promise stays pending. False, and the caller lets the exception propagate, when the handler declines
    /// it or it ends the run (<see cref="Throw.MustPropagateHostException"/>). Call it from a <c>catch</c> block, never a
    /// filter: the conversion runs engine code, which must see the stack already unwound.
    /// </summary>
    internal static bool TryConvertHostException(Engine engine, Exception exception, [NotNullWhen(true)] out JsValue? error)
    {
        if (exception is JavaScriptException
            || Throw.MustPropagateHostException(exception)
            || !engine.Options.Interop.ExceptionHandler(exception))
        {
            error = null;
            return false;
        }

        try
        {
            Throw.FromClrException(engine, exception);
        }
        catch (JavaScriptException converted)
        {
            error = converted.Error;
            return true;
        }

        error = null;
        return false;
    }

    /// <summary>
    /// <see cref="AsyncIteratorClose(CompletionType, Action{JsValue})"/> for a caller that answers with
    /// <paramref name="capability"/>: rejected with the error the close raises, and otherwise settled with
    /// <paramref name="value"/>, rejected for a throw completion (the error it carries) and resolved for any other.
    /// </summary>
    internal void AsyncIteratorClose(CompletionType completion, JsValue value, PromiseCapability capability)
    {
        AsyncIteratorClose(completion, error =>
        {
            if (error is not null)
            {
                capability.Reject(error);
            }
            else if (completion == CompletionType.Throw)
            {
                capability.Reject(value);
            }
            else
            {
                capability.Resolve(value);
            }
        });
    }

    /// <summary>
    /// The reaction to the Await of <see cref="AsyncIteratorClose(CompletionType, Action{JsValue})"/>: releases the native
    /// state now that the answer has settled, then takes steps 5 to 8. The <c>TypeError</c> of step 7 is created in the
    /// realm that was running when the close started. A host's <c>Dispose</c> can throw out of the release only when no
    /// throw is leaving the close, and its exception is then what the close raises, converted as
    /// <see cref="TryConvertHostException"/> converts one; this reaction has no capability that would take it otherwise.
    /// Declined by the engine's interop exception handler, it propagates as it is.
    /// </summary>
    private sealed class AsyncCloseContinuation : IPromiseContinuation
    {
        private readonly IteratorInstance _record;
        private readonly CompletionType _completion;
        private readonly Realm _realm;
        private readonly Action<JsValue?> _onClosed;

        public AsyncCloseContinuation(IteratorInstance record, CompletionType completion, Realm realm, Action<JsValue?> onClosed)
        {
            _record = record;
            _completion = completion;
            _realm = realm;
            _onClosed = onClosed;
        }

        public void Invoke(Engine engine, JsValue value, ReactionType type)
        {
            var error = AsyncIteratorCloseError(_realm, _completion, value, type == ReactionType.Reject);
            try
            {
                _record.CloseNativeStateAfterReturn(_completion == CompletionType.Throw || error is not null ? CompletionType.Throw : _completion);
            }
            catch (Exception e) when (e is not JavaScriptException && !Throw.MustPropagateHostException(e))
            {
                if (!TryConvertHostException(engine, e, out var converted))
                {
                    throw;
                }

                error = converted;
            }

            _onClosed(error);
        }
    }
}
