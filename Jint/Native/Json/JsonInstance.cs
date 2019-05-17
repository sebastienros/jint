using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public sealed class JsonInstance : ObjectInstance
    {
        private JsValue _reviver;

        private JsonInstance(Engine engine)
            : base(engine, objectClass: "JSON")
        {
            Extensible = true;
        }

        public static JsonInstance CreateJsonObject(Engine engine)
        {
            var json = new JsonInstance(engine)
            {
                Prototype = engine.Object.PrototypeObject
            };
            return json;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(2)
            {
                ["parse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parse", Parse, 2), true, false, true),
                ["stringify"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "stringify", Stringify, 3), true, false, true)
            };
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
                        var newValue = AbstractWalkOperation(valAsArray, TypeConverter.ToString(i));
                        if (newValue.IsUndefined())
                        {
                            valAsArray.Delete(TypeConverter.ToString(i), false);
                        }
                        else
                        {
                            valAsArray.DefineOwnProperty
                            (
                                TypeConverter.ToString(i),
                                new PropertyDescriptor
                                (
                                    value: newValue,
                                    PropertyFlag.ConfigurableEnumerableWritable
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
                                    PropertyFlag.ConfigurableEnumerableWritable
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
                        PropertyFlag.ConfigurableEnumerableWritable
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
            if (value.IsUndefined() && replacer.IsUndefined()) {
                return Undefined;
            }

            return serializer.Serialize(value, replacer, space);
        }
    }
}
