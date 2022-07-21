using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.BigInt;

public sealed class BigIntInstance : ObjectInstance, IPrimitiveInstance
{
    public BigIntInstance(Engine engine, JsBigInt value)
        : base(engine, ObjectClass.Object)
    {
        BigIntData = value;
    }

    Types IPrimitiveInstance.Type => Types.BigInt;

    JsValue IPrimitiveInstance.PrimitiveValue => BigIntData;

    public JsBigInt BigIntData { get; }
}
