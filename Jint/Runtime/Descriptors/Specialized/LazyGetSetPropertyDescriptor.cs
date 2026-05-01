using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

/// <summary>
/// Accessor counterpart to <see cref="LazyPropertyDescriptor{T}"/>: defers allocation of the
/// get and/or set <see cref="JsValue"/> instances until first access. The source generator emits
/// this for <c>[JsAccessor]</c> / <c>[JsSymbolAccessor]</c> slots, so prototypes with many never-read
/// accessors (e.g. Temporal date/time prototypes with 10-20 each) avoid allocating the dispatcher
/// <c>Function</c> per slot at <c>Initialize()</c> time.
/// </summary>
internal sealed class LazyGetSetPropertyDescriptor<T> : PropertyDescriptor where T : class
{
    private readonly T _state;
    private readonly Func<T, JsValue>? _getResolver;
    private readonly Func<T, JsValue>? _setResolver;
    private JsValue? _get;
    private JsValue? _set;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazyGetSetPropertyDescriptor(T state, Func<T, JsValue>? getResolver, Func<T, JsValue>? setResolver, PropertyFlag flags)
        : base(null, flags)
    {
        _flags |= PropertyFlag.NonData;
        _flags &= ~PropertyFlag.WritableSet;
        _flags &= ~PropertyFlag.Writable;
        _state = state;
        _getResolver = getResolver;
        _setResolver = setResolver;
    }

    public override JsValue? Get => _getResolver is null ? null : (_get ??= _getResolver(_state));
    public override JsValue? Set => _setResolver is null ? null : (_set ??= _setResolver(_state));
}
