using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public sealed class ErrorConstructor : FunctionInstance, IConstructor
    {
        private readonly JsString _name;
        private static readonly JsString _functionName = new JsString("Error");

        internal ErrorConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectInstance objectPrototype,
            JsString name)
            : base(engine, realm, name)
        {
            _name = name;
            _prototype = functionPrototype;
            PrototypeObject = new ErrorPrototype(engine, realm, this, objectPrototype, name);
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberOne;
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
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
                _ => PrototypeObject,
                static (e, state) => new ErrorInstance(e, (JsString) state),
                _name);

            var jsValue = arguments.At(0);
            if (!jsValue.IsUndefined())
            {
                var msg = TypeConverter.ToString(jsValue);
                var msgDesc = new PropertyDescriptor(msg, true, false, true);
                o.DefinePropertyOrThrow("message", msgDesc);
            }

            var lastSyntaxNode = _engine.GetLastSyntaxNode();
            var stackString = lastSyntaxNode == null ? Undefined : _engine.CallStack.BuildCallStackString(lastSyntaxNode.Location);
            var stackDesc = new PropertyDescriptor(stackString, true, false, true);
            o.DefinePropertyOrThrow(CommonProperties.Stack, stackDesc);

            return o;
        }

        public ErrorPrototype PrototypeObject { get; private set; }

        protected internal override ObjectInstance GetPrototypeOf()
        {
            return _name._value != "Error" ? _realm.Intrinsics.Error : _prototype;
        }
    }
}
