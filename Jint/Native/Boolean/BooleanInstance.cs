using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Boolean
{
    public class BooleanInstance : ObjectInstance, IPrimitiveInstance
    {
        public BooleanInstance(Engine engine, JsBoolean value)
            : base(engine, ObjectClass.Boolean)
        {
            BooleanData = value;
        }

        Types IPrimitiveInstance.Type => Types.Boolean;

        JsValue IPrimitiveInstance.PrimitiveValue => BooleanData;

        public JsValue BooleanData { get; }
    }
}
