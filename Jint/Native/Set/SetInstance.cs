using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Set
{
    public class SetInstance : ObjectInstance
    {
        internal readonly OrderedSet<JsValue> _set;

        public SetInstance(Engine engine)
            : base(engine, objectClass: "Map")
        {
            _set = new OrderedSet<JsValue>();
        }

        /// Implementation from ObjectInstance official specs as the one
        /// in ObjectInstance is optimized for the general case and wouldn't work
        /// for arrays
        public override void Put(in Key propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(Engine);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.TryCast<ICallable>();
                setter.Call(this, new[] {value});
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Size)
            {
                return new PropertyDescriptor(_set.Count, PropertyFlag.None);
            }

            return base.GetOwnProperty(propertyName);
        }

        protected override bool TryGetProperty(in Key propertyName, out PropertyDescriptor descriptor)
        {
            if (propertyName == KnownKeys.Size)
            {
                descriptor = new PropertyDescriptor(_set.Count, PropertyFlag.None);
                return true;
            }

            return base.TryGetProperty(propertyName, out descriptor);
        }

        public void Add(JsValue value)
        {
            _set.Add(value);
        }

        public void Clear()
        {
            _set.Clear();
        }

        public bool Has(JsValue key)
        {
            return _set.Contains(key);
        }

        public bool Delete(JsValue key)
        {
            return _set.Remove(key);
        }

        public void ForEach(ICallable callable, JsValue thisArg)
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

        public ObjectInstance Entries()
        {
            return _engine.Iterator.ConstructEntryIterator(this);
        }

        public ObjectInstance Values()
        {
            return _engine.Iterator.Construct(_set._list);
        }
    }
}
