using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public sealed class ErrorConstructor : FunctionInstance, IConstructor
    {
        private readonly JsString _name;

        internal ErrorConstructor(
            Engine engine,
            Realm realm,
            ObjectInstance functionPrototype,
            ObjectInstance objectPrototype,
            JsString name)
            : base(engine, realm, name)
        {
            _name = name;
            _prototype = functionPrototype;
            PrototypeObject = new ErrorPrototype(engine, realm, this, objectPrototype, name, ObjectClass.Object);
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        public ErrorPrototype PrototypeObject { get; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            return Construct(arguments, this);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var o = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Error.PrototypeObject,
                static (engine, realm, state) => new ErrorInstance(engine));

            var jsValue = arguments.At(0);
            if (!jsValue.IsUndefined())
            {
                var msg = TypeConverter.ToString(jsValue);
                var msgDesc = new PropertyDescriptor(msg, true, false, true);
                o.DefinePropertyOrThrow("message", msgDesc);
            }

            var lastSyntaxNode = _engine.GetLastSyntaxNode();
            var stackString = lastSyntaxNode == null ? Undefined : _engine.CallStack.BuildCallStackString(lastSyntaxNode.Location);
            var stackDesc = new PropertyDescriptor(stackString, PropertyFlag.NonEnumerable);
            o.DefinePropertyOrThrow(CommonProperties.Stack, stackDesc);

            var options = arguments.At(1);

            if (options is ObjectInstance oi && oi.HasProperty("cause"))
            {
                var cause = oi.Get("cause");
                var causeDesc = new PropertyDescriptor(cause, PropertyFlag.NonEnumerable);
                o.DefinePropertyOrThrow("cause", causeDesc);
            }

            return o;
        }
    }
}
