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
        private readonly Realm _realm;

        internal JsonInstance(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine)
        {
            _realm = realm;
            _prototype = objectPrototype;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
#pragma warning disable 618
                ["parse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parse", Parse, 2, PropertyFlag.Configurable), true, false, true),
                ["stringify"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "stringify", Stringify, 3, PropertyFlag.Configurable), true, false, true)
#pragma warning restore 618
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
            var temp = holder.Get(name);
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
        [Obsolete("Method will be made private, use JsonParser directly")]
        public JsValue Parse(JsValue thisObject, JsValue[] arguments)
        {
            var jsonString = TypeConverter.ToString(arguments.At(0));
            var reviver = arguments.At(1);

            var parser = new JsonParser(_engine);
            var unfiltered = parser.Parse(jsonString);

            if (reviver.IsCallable)
            {
                var root = _realm.Intrinsics.Object.Construct(Arguments.Empty);
                var rootName = JsString.Empty;
                root.CreateDataPropertyOrThrow(rootName, unfiltered);
                return InternalizeJSONProperty(root, rootName, (ICallable) reviver);
            }
            else
            {
                return unfiltered;
            }
        }

        [Obsolete("Method will be made private, use JsonSerializer directly")]
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
