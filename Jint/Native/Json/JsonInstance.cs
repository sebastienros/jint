using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public sealed class JsonInstance : ObjectInstance
    {
        private JsonInstance(Engine engine)
            : base(engine, objectClass: ObjectClass.JSON)
        {
        }

        public static JsonInstance CreateJsonObject(Engine engine)
        {
            var json = new JsonInstance(engine)
            {
                _prototype = engine.Object.PrototypeObject
            };
            return json;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
                ["parse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parse", Parse, 2), true, false, true),
                ["stringify"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "stringify", Stringify, 3), true, false, true)
            };
            SetProperties(properties);
            
            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("JSON", false, false, true),
            };
            SetSymbols(symbols);
        }

        private static JsValue InternalizeJSONProperty(JsValue holder, JsValue name, ICallable reviver)
        {
            JsValue temp = holder.Get(name, holder);
            if (temp is ObjectInstance val)
            {
                if (val.IsArray())
                {
                    var i = 0UL;
                    var len = TypeConverter.ToLength(val.Get(CommonProperties.Length));
                    while (i < len)
                    {
                        var prop = JsString.Create(i);
                        var newElement = InternalizeJSONProperty(val, prop, reviver);
                        if (newElement.IsUndefined())
                        {
                            val.Delete(prop);
                        }
                        else
                        {
                            val.CreateDataProperty(prop, newElement);
                        }
                        i = i + 1;
                    }
                }
                else
                {
                    var keys = val.EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind.Key);
                    foreach (var p in keys)
                    {
                        var newElement = InternalizeJSONProperty(val, p, reviver);
                        if (newElement.IsUndefined())
                        {
                            val.Delete(p);
                        }
                        else
                        {
                            val.CreateDataProperty(p, newElement);
                        }
                    }
                }
            }

            return reviver.Call(holder, new[] { name, temp });
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-json.parse
        /// </summary>
        public JsValue Parse(JsValue thisObject, JsValue[] arguments)
        {
            var jsonString = TypeConverter.ToString(arguments.At(0));
            var reviver = arguments.At(1);

            var parser = new JsonParser(_engine);
            var unfiltered = parser.Parse(jsonString);

            if (reviver.IsCallable)
            {
                var root = _engine.Object.Construct(Arguments.Empty);
                var rootName = JsString.Empty;
                root.CreateDataPropertyOrThrow(rootName, unfiltered);
                return InternalizeJSONProperty(root, rootName, (ICallable) reviver);
            }
            else
            {
                return unfiltered;
            }
        }

        public JsValue Stringify(JsValue thisObject, JsValue[] arguments)
        {
            var value = arguments.At(0);
            var replacer = arguments.At(1);
            var space = arguments.At(2);

            if (value.IsUndefined() && replacer.IsUndefined()) 
            {
                return Undefined;
            }

            var serializer = new JsonSerializer(_engine);
            return serializer.Serialize(value, replacer, space);
        }
    }
}
