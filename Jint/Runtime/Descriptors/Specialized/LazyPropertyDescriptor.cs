using System;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class LazyPropertyDescriptor : PropertyDescriptor
    {
        private readonly Realm _realm;
        private readonly Func<Realm, JsValue> _resolver;

        internal LazyPropertyDescriptor(Realm realm, Func<Realm, JsValue> resolver, PropertyFlag flags)
            : base(null, flags | PropertyFlag.CustomJsValue)
        {
            _realm = realm;
            _resolver = resolver;
        }

        protected internal override JsValue CustomValue
        {
            get => _value ??= _resolver(_realm);
            set => _value = value;
        }
    }
}