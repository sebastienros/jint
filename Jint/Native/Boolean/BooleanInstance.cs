using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Boolean;

internal class BooleanInstance : ObjectInstance, IJsPrimitive
{
    public BooleanInstance(Engine engine, JsBoolean value)
        : base(engine, ObjectClass.Boolean)
    {
        BooleanData = value;
    }

    Types IJsPrimitive.Type => Types.Boolean;

    JsValue IJsPrimitive.PrimitiveValue => BooleanData;

    public JsBoolean BooleanData { get; }
}
