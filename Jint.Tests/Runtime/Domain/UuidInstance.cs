using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Domain;

internal class UuidInstance : ObjectInstance, IObjectWrapper
{
    protected internal override ObjectInstance GetPrototypeOf() => _prototype;

    internal new ObjectInstance _prototype;

    public JsUuid PrimitiveValue { get; set; }

    public object Target => PrimitiveValue?._value;

    public UuidInstance(Engine engine) : base(engine)
    {
    }
}