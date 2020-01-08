using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Proxy
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/index.html#sec-proxy-object-internal-methods-and-internal-slots
    /// </summary>
    public sealed class ProxyPrototype : ObjectInstance
    {
        private ProxyConstructor _proxyConstructor;

        private ProxyPrototype(Engine engine) : base(engine)
        {
        }

        public static ProxyPrototype CreatePrototypeObject(Engine engine, ProxyConstructor proxyConstructor)
        {
            var obj = new ProxyPrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _proxyConstructor = proxyConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(3)
            {
                ["constructor"] = new PropertyDescriptor(_proxyConstructor, PropertyFlag.NonEnumerable)
            };
        }
    }
}