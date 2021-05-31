using System.Runtime.CompilerServices;

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakMap
{
    public class WeakMapInstance : ObjectInstance
    {
        private readonly ConditionalWeakTable<JsValue, JsValue> _table;

        public WeakMapInstance(Engine engine) : base(engine)
        {
            _table = new ConditionalWeakTable<JsValue, JsValue>();
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            return base.GetOwnProperty(property);
        }

        protected override bool TryGetProperty(JsValue property, out PropertyDescriptor descriptor)
        {
            return base.TryGetProperty(property, out descriptor);
        }

        internal bool WeakMapHas(JsValue key)
        {
            return _table.TryGetValue(key, out _);
        }

        internal bool WeakMapDelete(JsValue key)
        {
            return _table.Remove(key);
        }

        internal void WeakMapSet(JsValue key, JsValue value)
        {
            if (key.IsPrimitive())
                ExceptionHelper.ThrowTypeError(_engine, "WeakMap key must be an object, got " + key.ToString());

#if NETSTANDARD2_1
            _table.AddOrUpdate(key, value);
#else
            _table.Remove(key);
            _table.Add(key, value);
#endif
        }

        internal JsValue WeakMapGet(JsValue key)
        {
            if (!_table.TryGetValue(key, out var value))
            {
                return Undefined;
            }

            return value;
        }

    }
}
