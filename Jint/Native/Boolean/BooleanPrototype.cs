using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Boolean
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.6.4
    /// </summary>
    public sealed class BooleanPrototype : BooleanInstance
    {
        private BooleanPrototype(Engine engine) : base(engine)
        {
        }

        public static BooleanPrototype CreatePrototypeObject(Engine engine, BooleanConstructor booleanConstructor)
        {
            var obj = new BooleanPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = false;
            obj.Extensible = true;

            obj.FastAddProperty("constructor", booleanConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToBooleanString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, ValueOf), true, false, true);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            var B = thisObj;
            if (B.IsBoolean())
            {
                return B;
            }
            else
            {
                var o = B.TryCast<BooleanInstance>();
                if (o != null)
                {
                    return o.PrimitiveValue;
                }
                else
                {
                    throw new JavaScriptException(Engine.TypeError);
                }
            }
        }

        private JsValue ToBooleanString(JsValue thisObj, JsValue[] arguments)
        {
            var b = ValueOf(thisObj, Arguments.Empty);
            return b.AsBoolean() ? "true" : "false";
        }
    }
}