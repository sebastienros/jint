using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Proxy
{
    public sealed class ProxyConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _name = new JsString("Proxy");
        private static readonly JsString PropertyProxy = new JsString("proxy");
        private static readonly JsString PropertyRevoke = new JsString("revoke");

        internal ProxyConstructor(
            Engine engine,
            Realm realm)
            : base(engine, realm, _name)
        {
            _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Constructor Proxy requires 'new'");
            return null;
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/index.html#sec-proxy-object-internal-methods-and-internal-slots-construct-argumentslist-newtarget
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var target = arguments.At(0);
            var handler = arguments.At(1);

            if (!target.IsObject() || !handler.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Cannot create proxy with a non-object as target or handler");
            }
            return Construct(target.AsObject(), handler.AsObject());
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(1, checkExistingKeys: false)
            {
                ["revocable"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "revocable", Revocable, 2, PropertyFlag.Configurable), true, true, true)
            };
            SetProperties(properties);
        }

        protected internal override ObjectInstance GetPrototypeOf()
        {
            return _realm.Intrinsics.Function.Prototype;
        }

        public ProxyInstance Construct(ObjectInstance target, ObjectInstance handler)
        {
            if (target is ProxyInstance targetProxy && targetProxy._handler is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            if (handler is ProxyInstance handlerProxy && handlerProxy._handler is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            var instance = new ProxyInstance(Engine, target, handler);
            return instance;
        }

        private JsValue Revocable(JsValue thisObject, JsValue[] arguments)
        {
            var p = Construct(arguments, thisObject);

            System.Func<JsValue, JsValue[], JsValue> revoke = (JsValue thisObject, JsValue[] arguments) =>
            {
                var proxy = (ProxyInstance) p;
                proxy._handler = null;
                proxy._target = null;
                return Undefined;
            };

            var result = _realm.Intrinsics.Object.Construct(System.Array.Empty<JsValue>());
            result.DefineOwnProperty(PropertyRevoke, new PropertyDescriptor(new ClrFunctionInstance(_engine, name: null, revoke, 0, PropertyFlag.Configurable), PropertyFlag.ConfigurableEnumerableWritable));
            result.DefineOwnProperty(PropertyProxy, new PropertyDescriptor(p, PropertyFlag.ConfigurableEnumerableWritable));
            return result;
        }
    }
}
