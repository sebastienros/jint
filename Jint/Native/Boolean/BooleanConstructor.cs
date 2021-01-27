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
        
        public BooleanPrototype PrototypeObject { get; private set; }

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
        /// https://tc39.es/ecma262/#sec-boolean-constructor-boolean-value
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var b = TypeConverter.ToBoolean(arguments.At(0)) 
                ? JsBoolean.True
                : JsBoolean.False;

            if (newTarget.IsUndefined())
            {
                return Construct(b);
            }

            var o = OrdinaryCreateFromConstructor(newTarget, PrototypeObject, static (engine, state) => new BooleanInstance(engine, (JsBoolean) state), b);
            return Construct(b);
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
