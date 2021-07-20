using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set
{
    public class SetInstance : ObjectInstance
    {
        internal readonly OrderedSet<JsValue> _set;

        public SetInstance(Engine engine)
            : base(engine, ObjectClass.Map)
        {
            _set = new OrderedSet<JsValue>();
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Size)
            {
                return new PropertyDescriptor(_set.Count, PropertyFlag.None);
            }

            return base.GetOwnProperty(property);
        }

        protected override bool TryGetProperty(JsValue property, out PropertyDescriptor descriptor)
        {
            if (property == CommonProperties.Size)
            {
                descriptor = new PropertyDescriptor(_set.Count, PropertyFlag.None);
                return true;
            }

            return base.TryGetProperty(property, out descriptor);
        }

        internal void Add(JsValue value)
        {
            _set.Add(value);
        }

        internal void Clear()
        {
            _set.Clear();
        }

        internal bool Has(JsValue key)
        {
            return _set.Contains(key);
        }

        internal bool SetDelete(JsValue key)
        {
            return _set.Remove(key);
        }

        internal void ForEach(ICallable callable, JsValue thisArg)
        {
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = this;

            for (var i = 0; i < _set._list.Count; i++)
            {
                var value = _set._list[i];
                args[0] = value;
                args[1] = value;
                callable.Call(thisArg, args);
            }

            _engine._jsValueArrayPool.ReturnArray(args);
        }

        internal ObjectInstance Entries()
        {
            return _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructEntryIterator(this);
        }

        internal ObjectInstance Values()
        {
            return _engine.Realm.Intrinsics.SetIteratorPrototype.ConstructValueIterator(this);
        }
    }
}
