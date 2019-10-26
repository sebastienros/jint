using Jint.Collections;
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
        private BooleanConstructor _booleanConstructor;

        private BooleanPrototype(Engine engine) : base(engine)
        {
        }

        public static BooleanPrototype CreatePrototypeObject(Engine engine, BooleanConstructor booleanConstructor)
        {
            var obj = new BooleanPrototype(engine)
            {
                Prototype = engine.Object.PrototypeObject,
                PrimitiveValue = false,
                Extensible = true,
                _booleanConstructor = booleanConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(3)
            {
                ["constructor"] = new PropertyDescriptor(_booleanConstructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToBooleanString, 0, PropertyFlag.Configurable), true, false, true),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true)
            };
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj._type == InternalTypes.Boolean)
            {
                return thisObj;
            }

            if (thisObj is BooleanInstance bi)
            {
                return bi.PrimitiveValue;
            }

            return ExceptionHelper.ThrowTypeError<JsValue>(Engine);
        }

        private JsValue ToBooleanString(JsValue thisObj, JsValue[] arguments)
        {
            var b = ValueOf(thisObj, Arguments.Empty);
            return ((JsBoolean) b)._value ? "true" : "false";
        }
    }
}