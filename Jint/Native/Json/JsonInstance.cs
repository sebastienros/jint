using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public sealed class JsonInstance : ObjectInstance
    {
        private readonly Engine _engine;
        private JsValue _reviver;

        private JsonInstance(Engine engine)
            : base(engine)
        {
            _engine = engine;
            Extensible = true;
        }

        public override string Class
        {
            get
            {
                return "JSON";
            }
        }

        public static JsonInstance CreateJsonObject(Engine engine)
        {
            var json = new JsonInstance(engine);
            json.Prototype = engine.Object.PrototypeObject;
            return json;
        }

        public void Configure()
        {
            FastAddProperty("parse", new ClrFunctionInstance(Engine, Parse, 2), true, false, true);
            FastAddProperty("stringify", new ClrFunctionInstance(Engine, Stringify, 3), true, false, true);
        }

        private JsValue AbstractWalkOperation(ObjectInstance thisObject, string prop)
        {
            JsValue value = thisObject.Get(prop);
            if (value.IsObject())
            {
                var valueAsObject = value.AsObject();
                if (valueAsObject.Class == "Array")
                {
                    var valAsArray = value.AsArray();
                    var i = 0;
                    var arrLen = valAsArray.GetLength();
                    while (i < arrLen)
                    {
                        var newValue = AbstractWalkOperation(valAsArray, i.ToString());
                        if (newValue.IsUndefined())
                        {
                            valAsArray.Delete(i.ToString(), false);
                        }
                        else
                        {
                            valAsArray.DefineOwnProperty
                            (
                                i.ToString(),
                                new PropertyDescriptor
                                (
                                    value: newValue,
                                    writable: true,
                                    enumerable: true,
                                    configurable: true
                                ),
                                false
                            );
                        }
                        i = i + 1;
                    }
                }
                else
                {
                    var keys = valueAsObject.GetOwnProperties();
                    foreach (var p in keys)
                    {
                        var newElement = AbstractWalkOperation(valueAsObject, p.Key);
                        if (newElement.IsUndefined())
                        {
                            valueAsObject.Delete(p.Key, false);
                        }
                        else
                        {
                            valueAsObject.DefineOwnProperty(
                                p.Key,
                                new PropertyDescriptor
                                (
                                    value: newElement,
                                    writable: true,
                                    enumerable: true,
                                    configurable: true
                                ),
                                false
                            );
                        }
                    }
                }
            }
            return _reviver.Invoke(thisObject, new JsValue[] { prop, value });
        }

        public JsValue Parse(JsValue thisObject, JsValue[] arguments)
        {
            var parser = new JsonParser(_engine);
            var res = parser.Parse(TypeConverter.ToString(arguments[0]));
            if (arguments.Length > 1)
            {
                this._reviver = arguments[1];
                ObjectInstance revRes = ObjectConstructor.CreateObjectConstructor(_engine).Construct(Arguments.Empty);
                revRes.DefineOwnProperty(
                    "",
                    new PropertyDescriptor(
                        value: res,
                        writable: true,
                        enumerable: true,
                        configurable: true
                    ),
                    false
                );
                return AbstractWalkOperation(revRes, "");
            }
            return res;
        }

        public JsValue Stringify(JsValue thisObject, JsValue[] arguments)
        {
            JsValue
                value = Undefined,
                replacer = Undefined,
                space = Undefined;

            if (arguments.Length > 2)
            {
                space = arguments[2];
            }

            if (arguments.Length > 1)
            {
                replacer = arguments[1];
            }

            if (arguments.Length > 0)
            {
                value = arguments[0];
            }

            var serializer = new JsonSerializer(_engine);
            if (ReferenceEquals(value, Undefined) && ReferenceEquals(replacer, Undefined)) {
                return Undefined;
            }

            return serializer.Serialize(value, replacer, space);
        }
    }
}
