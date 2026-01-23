using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json;

internal sealed class JsonInstance : ObjectInstance
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
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["parse"] = new PropertyDescriptor(new ClrFunction(Engine, "parse", Parse, 2, PropertyFlag.Configurable), true, false, true),
            ["stringify"] = new PropertyDescriptor(new ClrFunction(Engine, "stringify", Stringify, 3, PropertyFlag.Configurable), true, false, true),
            ["rawJSON"] = new PropertyDescriptor(new ClrFunction(Engine, "rawJSON", RawJSON, 1, PropertyFlag.Configurable), true, false, true),
            ["isRawJSON"] = new PropertyDescriptor(new ClrFunction(Engine, "isRawJSON", IsRawJSON, 1, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("JSON", false, false, true),
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/proposal-json-parse-with-source/#sec-json.israwjson
    /// </summary>
    private static JsValue IsRawJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0);
        // If Type(O) is Object and O has an [[IsRawJSON]] internal slot, return true.
        // Return false otherwise.
        return o is JsRawJson;
    }

    /// <summary>
    /// https://tc39.es/proposal-json-parse-with-source/#sec-json.rawjson
    /// </summary>
    private JsValue RawJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var text = arguments.At(0);

        // 1. Let jsonString be ? ToString(text).
        var jsonString = TypeConverter.ToString(text);

        // 2. Throw a SyntaxError exception if jsonString is the empty String,
        //    or if either the first or last code unit of jsonString is a
        //    JSON white space code unit.
        if (jsonString.Length == 0)
        {
            Throw.SyntaxError(_realm, "JSON.rawJSON text cannot be empty");
        }

        var first = jsonString[0];
        var last = jsonString[jsonString.Length - 1];
        if (IsJsonWhiteSpace(first) || IsJsonWhiteSpace(last))
        {
            Throw.SyntaxError(_realm, "JSON.rawJSON text cannot have leading or trailing whitespace");
        }

        // 3. Parse StringToCodePoints(jsonString) as a JSON text as specified in ECMA-404.
        //    Throw a SyntaxError exception if it is not a valid JSON text
        //    or if its outermost value is an object or array.
        var parser = new JsonParser(_engine);
        JsValue parsed;
        try
        {
            parsed = parser.Parse(jsonString);
        }
        catch (JavaScriptException)
        {
            Throw.SyntaxError(_realm, "JSON.rawJSON: invalid JSON text");
            return Undefined;
        }

        // Check that it's not an object or array
        if (parsed is ObjectInstance)
        {
            Throw.SyntaxError(_realm, "JSON.rawJSON cannot be called with object or array");
        }

        // 4-8. Create and return the frozen object
        JsValue result = JsRawJson.Create(_engine, jsonString);
        return result;
    }

    private static bool IsJsonWhiteSpace(char ch)
    {
        return ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r';
    }

    /// <summary>
    /// Internalizes a JSON property with source text tracking for the reviver.
    /// https://tc39.es/proposal-json-parse-with-source/#sec-internalizejsonproperty
    /// </summary>
    private JsValue InternalizeJSONProperty(
        JsValue holder,
        JsValue name,
        ICallable reviver,
        JsonParseNode? parseNode,
        string jsonSource)
    {
        var val = holder.Get(name);

        if (val is ObjectInstance obj)
        {
            if (obj.IsArray())
            {
                var i = 0UL;
                var len = TypeConverter.ToLength(obj.Get(CommonProperties.Length));
                var elements = parseNode?.Elements;
                while (i < len)
                {
                    var prop = JsString.Create(i);
                    var elementNode = elements != null && (int) i < elements.Count ? elements[(int) i] : null;
                    var newElement = InternalizeJSONProperty(obj, prop, reviver, elementNode, jsonSource);
                    if (newElement.IsUndefined())
                    {
                        obj.Delete(prop);
                    }
                    else
                    {
                        obj.CreateDataProperty(prop, newElement);
                    }
                    i = i + 1;
                }
            }
            else
            {
                var keys = obj.EnumerableOwnProperties(EnumerableOwnPropertyNamesKind.Key);
                var entries = parseNode?.Entries;
                foreach (var p in keys)
                {
                    JsonParseNode? entryNode = null;
                    if (entries != null)
                    {
                        var keyStr = TypeConverter.ToString(p);
                        entries.TryGetValue(keyStr, out entryNode);
                    }
                    var newElement = InternalizeJSONProperty(obj, p, reviver, entryNode, jsonSource);
                    if (newElement.IsUndefined())
                    {
                        obj.Delete(p);
                    }
                    else
                    {
                        obj.CreateDataProperty(p, newElement);
                    }
                }
            }
        }

        // Create context object
        var context = _realm.Intrinsics.Object.Construct(Arguments.Empty);

        // For primitive values with a parse node, add the source property only if value hasn't been modified
        if (parseNode != null && parseNode.IsPrimitive && val is not ObjectInstance)
        {
            // Only include source if the value matches the originally parsed value
            if (parseNode.OriginalValue != null && JsValue.SameValue(val, parseNode.OriginalValue))
            {
                var sourceText = jsonSource.Substring(parseNode.Start, parseNode.End - parseNode.Start);
                context.CreateDataPropertyOrThrow(CommonProperties.Source, new JsString(sourceText));
            }
        }

        return reviver.Call(holder, name, val, context);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-json.parse
    /// </summary>
    private JsValue Parse(JsValue thisObject, JsCallArguments arguments)
    {
        var jsonString = TypeConverter.ToString(arguments.At(0));
        var reviver = arguments.At(1);

        var parser = new JsonParser(_engine);

        if (reviver.IsCallable)
        {
            // Parse with source tracking
            var parseResult = parser.ParseWithSourceInfo(jsonString);
            var root = _realm.Intrinsics.Object.Construct(Arguments.Empty);
            var rootName = JsString.Empty;
            root.CreateDataPropertyOrThrow(rootName, parseResult.Value);
            return InternalizeJSONProperty(root, rootName, (ICallable) reviver, parseResult.Node, jsonString);
        }
        else
        {
            var unfiltered = parser.Parse(jsonString);
            return unfiltered;
        }
    }

    private JsValue Stringify(JsValue thisObject, JsCallArguments arguments)
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
