using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// The state of a lazy property whose factory wants both the engine and something the host supplied, packed
/// so that <see cref="LazyPropertyDescriptor{T}"/> can carry it in its single state field.
/// </summary>
/// <remarks>
/// This exists to keep a per-engine registration allocation-free apart from the descriptor itself. Handing
/// <see cref="LazyPropertyDescriptor{T}"/> a lambda that closes over the host's state would allocate a display
/// class and a delegate for every property, on every engine; packing the state into a struct lets the resolver
/// be a <see langword="static"/> lambda, which the compiler caches once per instantiation.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EngineAndState<TState>(Engine Engine, TState State, Func<Engine, TState, JsValue> Factory);

internal sealed class LazyPropertyDescriptor<T> : PropertyDescriptor, IFieldBackedLazyDescriptor
{
    private readonly T _state;
    private readonly Func<T, JsValue> _resolver;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazyPropertyDescriptor(T state, Func<T, JsValue> resolver, PropertyFlag flags)
        : base(null, flags | PropertyFlag.CustomJsValue)
    {
        _flags &= ~PropertyFlag.NonData;
        _state = state;
        _resolver = resolver;
    }

    protected internal override JsValue? CustomValue
    {
        get
        {
            var value = _value;
            if (value is null)
            {
                // A resolver returning null would leave the pair in its "not materialized yet" state and
                // re-run on every read. Substituting Undefined here rather than making each caller wrap its
                // factory in a null-guarding lambda is what lets the registration APIs hand their own
                // delegate straight to this constructor: a wrapper allocated per registration is one thing,
                // but Engine.AddLazyGlobal registers per engine, so a host installing globals on
                // every request paid a closure pair for each of them.
                _value = value = _resolver(_state) ?? JsValue.Undefined;
            }

            if (value is not null)
            {
                // Once materialized this is semantically a plain data descriptor; clearing the
                // flag lets value reads/writes skip the CustomValue indirection and admits the
                // descriptor to the global-binding and member-write inline caches. Reached with a
                // value already stored when something wrote the inherited field directly —
                // ObjectInstance.Set's dictionary fast path does exactly that — which is just as
                // materialized, and would otherwise stay flagged for the descriptor's whole life
                // because this getter would never see a null again.
                _flags &= ~PropertyFlag.CustomJsValue;
            }

            return value;
        }
        set
        {
            _value = value;
            if (value is not null)
            {
                // A write materializes too: the resolver can never run after this, since the getter
                // above now finds a stored value. Same rejoin, same reason.
                _flags &= ~PropertyFlag.CustomJsValue;
            }
        }
    }
}
