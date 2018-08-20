using Jint.Runtime;
using Jint.Runtime.Descriptors;
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

            obj.SetOwnProperty("constructor", new PropertyDescriptor(booleanConstructor, PropertyFlag.NonEnumerable));

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToBooleanString, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            var B = thisObj;
            if (B.IsBoolean())
            {
                return B;
            }

            var o = B.TryCast<BooleanInstance>();
            if (!ReferenceEquals(o, null))
            {
                return o.PrimitiveValue;
            }

            ExceptionHelper.ThrowTypeError(Engine);
            return null;
        }

        private JsValue ToBooleanString(JsValue thisObj, JsValue[] arguments)
        {
            var b = ValueOf(thisObj, Arguments.Empty);
            return ((JsBoolean) b)._value ? "true" : "false";
        }
    }
}