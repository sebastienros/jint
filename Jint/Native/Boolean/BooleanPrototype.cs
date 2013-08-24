using System;
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

            obj.FastAddProperty("constructor", booleanConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToBooleanString), false, false, false);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, object>(Engine, ValueOf), false, false, false);
        }

        private object ValueOf(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object ToBooleanString(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }
    }
}