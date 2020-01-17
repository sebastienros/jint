using System;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class LazyPropertyDescriptor : PropertyDescriptor
    {
        private readonly Func<JsValue> _resolver;

        internal LazyPropertyDescriptor(Func<JsValue> resolver, PropertyFlag flags)
            : base(null, flags | PropertyFlag.CustomJsValue)
        {
            _resolver = resolver;
        }

        protected internal override JsValue CustomValue
        {
            get => _value ??= _resolver();
            set => _value = value;
        }
    }
}