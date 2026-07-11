using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise;

/// <summary>
/// https://tc39.es/ecma262/#sec-promisecapability-records
/// </summary>
/// <remarks>
/// Operates in one of two modes:
/// <para>
/// Intrinsic fast mode (<see cref="_promise"/> is not null): the capability was created by
/// <see cref="PromiseConstructor.NewPromiseCapability"/> for a realm's %Promise% intrinsic.
/// The spec's GetCapabilitiesExecutor dance is unobservable there (the executor and both
/// resolving functions are engine-created and handed only to engine code), so the promise is
/// created directly and <see cref="Resolve"/>/<see cref="Reject"/> settle it without
/// materializing JS-callable resolving functions. The function objects are created lazily by
/// <see cref="ResolveObj"/>/<see cref="RejectObj"/> only when they must escape to user code
/// (e.g. Promise.withResolvers or a combinator invoking a user-visible then).
/// </para>
/// <para>
/// Spec mode: for any other constructor the executor dance ran and the captured resolve/reject
/// callables are invoked, exactly as before.
/// </para>
/// </remarks>
internal sealed class PromiseCapability
{
    private readonly Engine _engine;
    internal readonly JsValue PromiseInstance;

    // Intrinsic fast mode: settle the promise directly, guarded by [[AlreadyResolved]].
    private readonly JsPromise? _promise;
    private bool _alreadyResolved;

    // Spec mode: the resolve/reject functions captured from the executor.
    private readonly ICallable? _resolve;
    private readonly ICallable? _reject;

    private JsValue? _resolveObj;
    private JsValue? _rejectObj;

    internal PromiseCapability(Engine engine, JsPromise promise)
    {
        _engine = engine;
        _promise = promise;
        PromiseInstance = promise;
    }

    internal PromiseCapability(
        Engine engine,
        JsValue promiseInstance,
        ICallable resolve,
        ICallable reject,
        JsValue resolveObj,
        JsValue rejectObj)
    {
        _engine = engine;
        PromiseInstance = promiseInstance;
        _resolve = resolve;
        _reject = reject;
        _resolveObj = resolveObj;
        _rejectObj = rejectObj;
    }

    /// <summary>
    /// The JS-visible resolve function object, materialized on demand in intrinsic fast mode.
    /// </summary>
    internal JsValue ResolveObj => _resolveObj ??= CreateResolvingFunction(rejecting: false);

    /// <summary>
    /// The JS-visible reject function object, materialized on demand in intrinsic fast mode.
    /// </summary>
    internal JsValue RejectObj => _rejectObj ??= CreateResolvingFunction(rejecting: true);

    private ClrFunction CreateResolvingFunction(bool rejecting)
    {
        // Matches CreateResolvingFunctions: an anonymous built-in with length 1 sharing this
        // capability's [[AlreadyResolved]] state (Resolve/Reject below carry the guard).
        return rejecting
            ? new ClrFunction(_engine, "", (_, args) =>
            {
                Reject(args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable)
            : new ClrFunction(_engine, "", (_, args) =>
            {
                Resolve(args.At(0));
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// Call(promiseCapability.[[Resolve]], undefined, « value »).
    /// </summary>
    internal void Resolve(JsValue value)
    {
        if (_promise is not null)
        {
            if (!_alreadyResolved)
            {
                _alreadyResolved = true;
                _promise.Resolve(value);
            }

            return;
        }

        _resolve!.Call(JsValue.Undefined, value);
    }

    /// <summary>
    /// Call(promiseCapability.[[Reject]], undefined, « reason »).
    /// </summary>
    internal void Reject(JsValue reason)
    {
        if (_promise is not null)
        {
            if (!_alreadyResolved)
            {
                _alreadyResolved = true;
                _promise.Reject(reason);
            }

            return;
        }

        _reject!.Call(JsValue.Undefined, reason);
    }
}
