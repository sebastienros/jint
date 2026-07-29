using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

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
                _value = value = _resolver(_state);
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
