using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Boolean
{
    public sealed class BooleanConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Boolean");

        private BooleanConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public static BooleanConstructor CreateBooleanConstructor(Engine engine)
        {
            var obj = new BooleanConstructor(engine);

            // The value of the [[Prototype]] internal property of the Boolean constructor is the Function prototype object
            obj._prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = BooleanPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(JsNumber.One, PropertyFlag.Configurable);

            // The initial value of Boolean.prototype is the Boolean prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return false;
            }

            return TypeConverter.ToBoolean(arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            return Construct(TypeConverter.ToBoolean(arguments.At(0)));
        }

        public BooleanPrototype PrototypeObject { get; private set; }

        public BooleanInstance Construct(bool value)
        {
            return Construct(value ? JsBoolean.True : JsBoolean.False);
        }
        
        public BooleanInstance Construct(JsBoolean value)
        {
            var instance = new BooleanInstance(Engine)
            {
                _prototype = PrototypeObject,
                PrimitiveValue = value,
            };

            return instance;
        }
    }
}
