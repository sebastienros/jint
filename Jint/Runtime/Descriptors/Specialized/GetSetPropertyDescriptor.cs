using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class GetSetPropertyDescriptor : PropertyDescriptor
    {
        private JsValue _get;
        private JsValue _set;

        public GetSetPropertyDescriptor(JsValue get, JsValue set, bool? enumerable = null, bool? configurable = null)
        : base(null, writable: null, enumerable: enumerable, configurable: configurable)
        {
            _get = get;
            _set = set;
        }

        internal GetSetPropertyDescriptor(JsValue get, JsValue set, PropertyFlag flags)
            : base(null, flags)
        {
            _get = get;
            _set = set;
        }

        public GetSetPropertyDescriptor(PropertyDescriptor descriptor) : base(descriptor)
        {
            _get = descriptor.Get;
            _set = descriptor.Set;
        }

        public override JsValue Get => _get;
        public override JsValue Set => _set;

        internal void SetGet(JsValue getter)
        {
            _get = getter;
        }
        
        internal void SetSet(JsValue setter)
        {
            _set = setter;
        }
    }
}