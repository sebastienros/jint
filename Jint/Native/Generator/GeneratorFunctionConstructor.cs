using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator
{
    internal sealed class GeneratorFunctionConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Function");

        private GeneratorFunctionConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public static GeneratorFunctionConstructor CreateGeneratorConstructor(Engine engine)
        {
            var obj = new GeneratorFunctionConstructor(engine)
            {
                PrototypeObject = GeneratorFunctionPrototype.CreatePrototypeObject(engine)
            };

            // The initial value of Function.prototype is the standard built-in Function prototype object

            // The value of the [[Prototype]] internal property of the Function constructor is the standard built-in Function prototype object
            obj._prototype = obj.PrototypeObject;

            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);
            obj._length = new PropertyDescriptor(JsNumber.One, PropertyFlag.Configurable);

            return obj;
        }

        public GeneratorFunctionPrototype PrototypeObject { get; private set; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var function = FunctionConstructor.CreateDynamicFunction(
                _engine,
                this,
                newTarget,
                FunctionKind.Generator,
                arguments);

            return function;
        }
    }
}