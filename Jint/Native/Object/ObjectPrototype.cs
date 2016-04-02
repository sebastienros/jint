using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectPrototype : ObjectInstance
    {
        private ObjectPrototype(Engine engine) : base(engine)
        {
        }

        public static ObjectPrototype CreatePrototypeObject(Engine engine, ObjectConstructor objectConstructor)
        {
            var obj = new ObjectPrototype(engine) { Extensible = true };

            obj.FastAddProperty("constructor", objectConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToObjectString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, ValueOf), true, false, true);
            FastAddProperty("hasOwnProperty", new ClrFunctionInstance(Engine, HasOwnProperty, 1), true, false, true);
            FastAddProperty("isPrototypeOf", new ClrFunctionInstance(Engine, IsPrototypeOf, 1), true, false, true);
            FastAddProperty("setPrototypeOf", new ClrFunctionInstance(Engine, SetPrototypeOf, 1), true, false, true);
            FastAddProperty("propertyIsEnumerable", new ClrFunctionInstance(Engine, PropertyIsEnumerable, 1), true, false, true);
        }

        private JsValue PropertyIsEnumerable(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            if (desc == PropertyDescriptor.Undefined)
            {
                return false;
            }
            return desc.Enumerable.HasValue && desc.Enumerable.Value;
        }

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            return o;
        }

        /// <summary>
        /// Although this is an ES6 spec item, it is crucial for object inheritance
        /// in modern JS and supported by all major engines other than IE<10.
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object.setprototypeof
        /// </summary>
        private JsValue SetPrototypeOf(JsValue thisObject, JsValue[] args)
        {
            JsValue O = JsValue.Undefined;
            JsValue proto = JsValue.Undefined;
            if (args.Length > 0)
            {
                O = args[0];
            }
            if (args.Length > 1)
            {
                proto = args[1];
            }
            // When the setPrototypeOf function is called with arguments O and proto, the following steps are taken:
            // 1 Let O be RequireObjectCoercible(O).
            // 2 ReturnIfAbrupt(O).
            TypeConverter.CheckObjectCoercible(Engine, O);
            // 3 If Type(proto) is neither Object nor Null, throw a TypeError exception.
            if (!proto.IsNull())
            {
                TypeConverter.CheckObjectCoercible(Engine, proto);
            }
            // 4 If Type(O) is not Object, return O.
            if (!O.IsObject())
            {
                return O;
            }
            // 5 Let status be O.[[SetPrototypeOf]](proto).
            // 6 ReturnIfAbrupt(status).
            var status = O.AsObject().SetPrototypeOf(proto);
            if (!status.AsBoolean())
            {
                // 7 If status is false, throw a TypeError exception.
                throw new JavaScriptException(Engine.TypeError);
            }
            // 8 Return O.
            return O;
        }

        private JsValue IsPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments[0];
            if (!arg.IsObject())
            {
                return false;
            }

            var v = arg.AsObject();

            var o = TypeConverter.ToObject(Engine, thisObject);
            while (true)
            {
                v = v.Prototype;

                if (v == null)
                {
                    return false;
                }

                if (o == v)
                {
                    return true;
                }

            }
        }

        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var toString = o.Get("toString").TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError);
            });

            return toString.Call(o, Arguments.Empty);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue ToObjectString(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject == Undefined.Instance)
            {
                return "[object Undefined]";
            }

            if (thisObject == Null.Instance)
            {
                return "[object Null]";
            }

            var o = TypeConverter.ToObject(Engine, thisObject);
            return "[object " + o.Class + "]";
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.5
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue HasOwnProperty(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(Engine, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
