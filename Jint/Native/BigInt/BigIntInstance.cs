using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.BigInt;

public class BigIntInstance : ObjectInstance, IPrimitiveInstance
{
    public BigIntInstance(Engine engine)
        : base(engine, ObjectClass.Object)
    {
    }

    public BigIntInstance(Engine engine, JsBigInt value)
        : this(engine)
    {
        BigIntData = value;
    }

    Types IPrimitiveInstance.Type => Types.BigInt;

    JsValue IPrimitiveInstance.PrimitiveValue => BigIntData;

    public JsBigInt BigIntData { get; internal init; }
}