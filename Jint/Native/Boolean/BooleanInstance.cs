using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Boolean;

internal class BooleanInstance : ObjectInstance, IPrimitiveInstance
{
    public BooleanInstance(Engine engine, JsBoolean value)
        : base(engine, ObjectClass.Boolean)
    {
        BooleanData = value;
    }

    Types IPrimitiveInstance.Type => Types.Boolean;

    JsValue IPrimitiveInstance.PrimitiveValue => BooleanData;

    public JsBoolean BooleanData { get; }
}
