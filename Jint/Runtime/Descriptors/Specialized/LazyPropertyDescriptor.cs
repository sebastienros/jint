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
        get
        {
            var value = _value;
            if (value is null)
            {
                _value = value = _resolver(_state);
                // Once materialized this is semantically a plain data descriptor; clearing the
                // flag lets value reads/writes skip the CustomValue indirection and admits the
                // descriptor to the global-binding and member-write inline caches.
                _flags &= ~PropertyFlag.CustomJsValue;
            }
            return value;
        }
        set => _value = value;
    }
}
