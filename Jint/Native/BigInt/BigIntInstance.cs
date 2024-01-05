using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.BigInt;

internal sealed class BigIntInstance : ObjectInstance, IJsPrimitive
{
    public BigIntInstance(Engine engine, JsBigInt value)
        : base(engine, ObjectClass.Object)
    {
        BigIntData = value;
    }

    Types IJsPrimitive.Type => Types.BigInt;

    JsValue IJsPrimitive.PrimitiveValue => BigIntData;

    public JsBigInt BigIntData { get; }
}
