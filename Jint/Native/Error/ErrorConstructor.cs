using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public sealed class ErrorConstructor : FunctionInstance, IConstructor
    {
        private JsString _name;
        private static readonly JsString _functionName = new JsString("Error");

        public ErrorConstructor(Engine engine) : base(engine, _functionName)
        {
        }

        public static ErrorConstructor CreateErrorConstructor(Engine engine, JsString name)
        {
            var obj = new ErrorConstructor(engine)
            {
                _name = name,
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Error constructor is the Function prototype object (15.11.3)
            obj.PrototypeObject = ErrorPrototype.CreatePrototypeObject(engine, obj, name);

            obj._length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;

            // The initial value of Error.prototype is the Error prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var o = OrdinaryCreateFromConstructor(
                newTarget,
                PrototypeObject, 
                static (e, state) => new ErrorInstance(e, (JsString) state),
                _name);

            var jsValue = arguments.At(0);
            if (!jsValue.IsUndefined())
            {
                var msg = TypeConverter.ToString(jsValue);
                var msgDesc = new PropertyDescriptor(msg, true, false, true);
                o.DefinePropertyOrThrow("message", msgDesc);
            }

            return o;
        }

        public ErrorPrototype PrototypeObject { get; private set; }

        protected internal override ObjectInstance GetPrototypeOf()
        {
            return _name._value != "Error" ? _engine.Error : _prototype;
        }
    }
}
