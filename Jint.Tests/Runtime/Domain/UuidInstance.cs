using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Domain
{
    /// <summary>
    /// Needs to be public, https://github.com/sebastienros/jint/issues/1144
    /// </summary>
    public class UuidInstance : ObjectInstance, IObjectWrapper
    {
        protected internal override ObjectInstance GetPrototypeOf() => _prototype;

        internal new ObjectInstance _prototype;

        public JsUuid PrimitiveValue { get; set; }

        public object Target => PrimitiveValue?._value;

        public UuidInstance(Engine engine) : base(engine)
        {
        }
    }
}
