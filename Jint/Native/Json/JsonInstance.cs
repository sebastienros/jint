using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Json;

[JsObject]
internal sealed partial class JsonInstance : BuiltinShapeObject
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString JsonToStringTag = new("JSON");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/proposal-json-parse-with-source/#sec-json.israwjson
    /// </summary>
    [JsFunction]
    private static JsValue IsRawJSON(JsValue thisObject, JsValue o)
    {
        // If Type(O) is Object and O has an [[IsRawJSON]] internal slot, return true.
        // Return false otherwise.
        return o is JsRawJson;
    }

    /// <summary>
    /// https://tc39.es/proposal-json-parse-with-source/#sec-json.rawjson
    /// </summary>
    [JsFunction]
    private JsValue RawJSON(JsValue thisObject, JsValue text)
    {
        // 1. Let jsonString be ? ToString(text).
        // ToString has no realm to raise from — a Symbol argument produces the engine-less TypeError that
        // the statement list would otherwise attribute to whichever realm is running. Re-raise it here so
        // a cross-realm `otherGlobal.JSON.rawJSON(Symbol())` throws that realm's TypeError.
        string jsonString;
        try
        {
            jsonString = TypeConverter.ToString(text);
        }
        catch (TypeErrorException ex)
        {
            Throw.TypeError(_realm, ex.Message);
            return Undefined;
        }

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
    /// <remarks>
    /// The reviver is taken as an already-built <see cref="CallbackInvoker"/>, by <c>in</c> so the walk
    /// passes a pointer rather than copying the struct down every level. It is built once in
    /// <see cref="Parse"/> — the reviver is fixed for the whole parse, and nothing a reviver does
    /// (mutating the holder, deleting keys, throwing, being a revoked proxy) can change how it must be
    /// invoked. One invoker serves the whole recursion without its argument array being aliased across
    /// levels: a level fills and invokes only after every child level has finished doing so.
    /// </remarks>
    private JsValue InternalizeJSONProperty(
        JsValue holder,
        JsValue name,
        in CallbackInvoker reviver,
        JsonParseNode? parseNode,
        string jsonSource)
    {
        var val = holder.Get(name);

        if (val is ObjectInstance obj)
        {
            if (obj.IsSpecArray())
            {
                var i = 0UL;
                var len = TypeConverter.ToLength(obj.Get(CommonProperties.Length));
                var elements = parseNode?.Elements;
                while (i < len)
                {
                    var prop = JsString.Create(i);
                    var elementNode = elements != null && (int) i < elements.Count ? elements[(int) i] : null;
                    var newElement = InternalizeJSONProperty(obj, prop, in reviver, elementNode, jsonSource);
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
                    var newElement = InternalizeJSONProperty(obj, p, in reviver, entryNode, jsonSource);
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

        // The context object is built fresh per key (its "source" property depends on this key's parse
        // node), so all three arguments vary and none of them can be hoisted into the invoker.
        return reviver.Call(holder, name, val, context);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-json.parse
    /// </summary>
    [JsFunction]
    private JsValue Parse(JsValue thisObject, JsValue text, JsValue reviver)
    {
        var jsonString = TypeConverter.ToString(text);

        var parser = new JsonParser(_engine);

        if (reviver.HasCall)
        {
            // Parse with source tracking
            var parseResult = parser.ParseWithSourceInfo(jsonString);
            var root = _realm.Intrinsics.Object.Construct(Arguments.Empty);
            var rootName = JsString.Empty;
            root.CreateDataPropertyOrThrow(rootName, parseResult.Value);

            // Arity is always three: this implementation follows the json-parse-with-source proposal,
            // where the reviver's third argument is the context object, and InternalizeJSONProperty
            // constructs and passes one unconditionally (only its "source" property is conditional).
            var invoker = CallbackInvoker.Rent(_engine, (ICallable) reviver, 3);
            try
            {
                // A reviver throwing is ordinary JavaScript, and so is a Delete on a frozen holder, so
                // the walk exits this way often enough to bracket the rent: the array would otherwise be
                // forfeited to the pool and re-created for the next document, losing exactly the
                // allocation Rent exists to avoid. The finally is affordable here because it wraps one
                // recursive call rather than a per-element loop.
                return InternalizeJSONProperty(root, rootName, in invoker, parseResult.Node, jsonString);
            }
            finally
            {
                invoker.Return();
            }
        }
        else
        {
            var unfiltered = parser.Parse(jsonString);
            return unfiltered;
        }
    }

    [JsFunction]
    private JsValue Stringify(JsValue thisObject, JsValue value, JsValue replacer, JsValue space)
    {
        if (value.IsUndefined() && replacer.IsUndefined())
        {
            return Undefined;
        }

        var serializer = new JsonSerializer(_engine);
        return serializer.Serialize(value, replacer, space);
    }
}
