using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class LazyPropertyDescriptor<T> : PropertyDescriptor
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
        get => _value ??= _resolver(_state);
        set => _value = value;
    }
}
