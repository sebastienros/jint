using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class LazyPropertyDescriptor : PropertyDescriptor
    {
        private readonly object? _state;
        private readonly Func<object?, JsValue> _resolver;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LazyPropertyDescriptor(object? state, Func<object?, JsValue> resolver, PropertyFlag flags)
            : base(null, flags | PropertyFlag.CustomJsValue)
        {
            _state = state;
            _resolver = resolver;
        }

        protected internal override JsValue? CustomValue
        {
            get => _value ??= _resolver(_state);
            set => _value = value;
        }
    }
}
