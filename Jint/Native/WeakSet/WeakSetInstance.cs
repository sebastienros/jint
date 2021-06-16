using System.Runtime.CompilerServices;

using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.WeakSet
{
    public class WeakSetInstance : ObjectInstance
    {
        private static readonly object _tableValue = new object();

        private readonly ConditionalWeakTable<JsValue, object> _table;

        public WeakSetInstance(Engine engine) : base(engine)
        {
            _table = new ConditionalWeakTable<JsValue, object>();
        }

        internal bool WeakSetHas(JsValue value)
        {
            return _table.TryGetValue(value, out _);
        }

        internal bool WeakSetDelete(JsValue value)
        {
            return _table.Remove(value);
        }

        internal void WeakSetAdd(JsValue value)
        {
            if (value.IsPrimitive())
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "WeakSet value must be an object, got " + value.ToString());
            }

#if NETSTANDARD2_1
            _table.AddOrUpdate(value, _tableValue);
#else
            _table.Remove(value);
            _table.Add(value, _tableValue);
#endif
        }

    }
}
