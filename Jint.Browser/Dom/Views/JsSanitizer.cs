using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.HtmlParser.Sanitization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// HTML's <c>Sanitizer</c>: a <see cref="SanitizerConfiguration"/> a page can build, query and modify, and
/// hand to <c>setHTML</c>, <c>setHTMLUnsafe</c>, <c>parseHTML</c> and <c>parseHTMLUnsafe</c>.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#the-sanitizer-interface. The
/// configuration and every algorithm over it are the parser package's; this type is the WebIDL half — the
/// receiver check and the conversions to and from <c>SanitizerConfig</c>, which <see cref="SanitizerConversion"/>
/// holds.
/// </remarks>
internal sealed class JsSanitizer : ObjectInstance
{
    internal JsSanitizer(Engine engine, ObjectInstance prototype, SanitizerConfiguration configuration) : base(engine)
    {
        Prototype = prototype;
        Configuration = configuration;
    }

    internal SanitizerConfiguration Configuration { get; }

    public override string ToString() => "[object Sanitizer]";

    internal static JsSanitizer Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsSanitizer sanitizer)
        {
            return sanitizer;
        }

        var message = "Failed to execute '" + member + "' on 'Sanitizer': Illegal invocation";
        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-sanitizer-constructor —
    /// <c>(SanitizerConfig or SanitizerPresets) configuration = "default"</c>, configured with permissive
    /// defaults either way.
    /// </summary>
    internal static JsSanitizer Construct(PageRuntime runtime, ObjectInstance prototype, JsValue[] arguments)
    {
        var realm = runtime.Engine.Realm;
        var value = DomConvert.At(arguments, 0);
        SanitizerConfiguration configuration;
        if (value.IsUndefined())
        {
            configuration = SanitizerBuiltins.SafeDefault();
        }
        else if (value.IsNull() || value is ObjectInstance)
        {
            configuration = SanitizerConversion.ReadConfig(realm, value, "Sanitizer");
        }
        else
        {
            SanitizerConversion.RequirePreset(realm, value, "Sanitizer");
            configuration = SanitizerBuiltins.SafeDefault();
        }

        SanitizerConversion.Configure(realm, configuration, permissiveDefaults: true, "Failed to construct 'Sanitizer'");
        return new JsSanitizer(runtime.Engine, prototype, configuration);
    }

    internal JsValue Get() => SanitizerConversion.ToJs(Engine, Configuration.ToSorted());

    internal JsValue AllowElement(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.allowElement");
        return Bool(Configuration.AllowElement(SanitizerConversion.ReadElementWithAttributes(Engine.Realm, arguments[0])));
    }

    internal JsValue RemoveElement(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.removeElement");
        return Bool(Configuration.RemoveElement(SanitizerConversion.ReadElement(Engine.Realm, arguments[0])));
    }

    internal JsValue ReplaceElementWithChildren(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.replaceElementWithChildren");
        return Bool(Configuration.ReplaceElementWithChildren(SanitizerConversion.ReadElement(Engine.Realm, arguments[0])));
    }

    internal JsValue AllowProcessingInstruction(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.allowProcessingInstruction");
        return Bool(Configuration.AllowProcessingInstruction(SanitizerConversion.ReadProcessingInstruction(Engine.Realm, arguments[0])));
    }

    internal JsValue RemoveProcessingInstruction(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.removeProcessingInstruction");
        return Bool(Configuration.RemoveProcessingInstruction(SanitizerConversion.ReadProcessingInstruction(Engine.Realm, arguments[0])));
    }

    internal JsValue AllowAttribute(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.allowAttribute");
        return Bool(Configuration.AllowAttribute(SanitizerConversion.ReadAttribute(Engine.Realm, arguments[0])));
    }

    internal JsValue RemoveAttribute(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.removeAttribute");
        return Bool(Configuration.RemoveAttribute(SanitizerConversion.ReadAttribute(Engine.Realm, arguments[0])));
    }

    internal JsValue SetComments(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.setComments");
        return Bool(Configuration.SetComments(TypeConverter.ToBoolean(arguments[0])));
    }

    internal JsValue SetDataAttributes(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.setDataAttributes");
        return Bool(Configuration.SetDataAttributes(TypeConverter.ToBoolean(arguments[0])));
    }

    internal JsValue SetJavascriptUrls(JsValue[] arguments)
    {
        DomConvert.Require(arguments, 0, "Sanitizer.setJavascriptURLs");
        return Bool(Configuration.SetJavascriptUrls(TypeConverter.ToBoolean(arguments[0])));
    }

    internal JsValue RemoveUnsafe() => Bool(Configuration.RemoveUnsafe());

    private static JsBoolean Bool(bool value) => value ? JsBoolean.True : JsBoolean.False;
}

/// <summary>
/// The WebIDL conversions between script values and <see cref="SanitizerConfiguration"/>: the
/// <c>SanitizerConfig</c> dictionary and its item unions in, and <c>get()</c>'s dictionary out.
/// </summary>
/// <remarks>
/// https://webidl.spec.whatwg.org/#js-dictionary reads a dictionary's members in lexicographic order, a
/// derived dictionary's after its base's, and that order is observable through getters, so each reader here
/// is written in it. Each item is canonicalized as it is read
/// (https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#canonicalize-a-sanitizer-name):
/// a string becomes a name in the item's default namespace, and an empty namespace becomes null.
/// </remarks>
internal static class SanitizerConversion
{
    /// <summary>Reads a <c>SanitizerConfig</c>; <see langword="null"/> and <see langword="undefined"/> are the empty one.</summary>
    internal static SanitizerConfiguration ReadConfig(Realm realm, JsValue value, string member)
    {
        var config = new SanitizerConfiguration();
        var dictionary = Dictionary(realm, value, member);
        if (dictionary is null)
        {
            return config;
        }

        var item = dictionary.Get("attributes");
        if (!item.IsUndefined()) config.Attributes = List(realm, item, ReadAttribute);
        item = dictionary.Get("comments");
        if (!item.IsUndefined()) config.Comments = TypeConverter.ToBoolean(item);
        item = dictionary.Get("dataAttributes");
        if (!item.IsUndefined()) config.DataAttributes = TypeConverter.ToBoolean(item);
        item = dictionary.Get("elements");
        if (!item.IsUndefined()) config.Elements = List(realm, item, ReadElementWithAttributes);
        item = dictionary.Get("javascriptURLs");
        if (!item.IsUndefined()) config.JavascriptUrls = TypeConverter.ToBoolean(item);
        item = dictionary.Get("processingInstructions");
        if (!item.IsUndefined()) config.ProcessingInstructions = List(realm, item, ReadProcessingInstruction);
        item = dictionary.Get("removeAttributes");
        if (!item.IsUndefined()) config.RemoveAttributes = List(realm, item, ReadAttribute);
        item = dictionary.Get("removeElements");
        if (!item.IsUndefined()) config.RemoveElements = List(realm, item, ReadElement);
        item = dictionary.Get("removeProcessingInstructions");
        if (!item.IsUndefined()) config.RemoveProcessingInstructions = List(realm, item, ReadProcessingInstruction);
        item = dictionary.Get("replaceWithChildrenElements");
        if (!item.IsUndefined()) config.ReplaceWithChildrenElements = List(realm, item, ReadElement);
        return config;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitizer-configure: canonicalize,
    /// then refuse an invalid configuration with a <c>TypeError</c>.
    /// </summary>
    internal static void Configure(Realm realm, SanitizerConfiguration configuration, bool permissiveDefaults, string failure)
    {
        configuration.Canonicalize(permissiveDefaults);
        if (!configuration.IsValid())
        {
            Throw.TypeError(realm, failure + ": The sanitizer configuration is not valid.");
        }
    }

    /// <summary>The <c>SanitizerPresets</c> enumeration, whose one value is <c>"default"</c>.</summary>
    internal static void RequirePreset(Realm realm, JsValue value, string member)
    {
        var text = TypeConverter.ToString(value);
        if (text != "default")
        {
            Throw.TypeError(realm, "Failed to execute '" + member + "': The provided value '" + text
                + "' is not a valid enum value of type SanitizerPresets.");
        }
    }

    /// <summary><c>(DOMString or SanitizerElementNamespaceWithAttributes)</c>.</summary>
    internal static SanitizerElementRule ReadElementWithAttributes(Realm realm, JsValue value)
    {
        if (value is not ObjectInstance && !value.IsNullOrUndefined())
        {
            return new SanitizerElementRule(SanitizerName.Html(TypeConverter.ToString(value)));
        }

        var dictionary = value as ObjectInstance;
        var name = ReadNamespaced(realm, dictionary, Namespaces.Html);
        List<SanitizerName>? attributes = null;
        List<SanitizerName>? removeAttributes = null;
        var item = dictionary?.Get("attributes") ?? JsValue.Undefined;
        if (!item.IsUndefined()) attributes = List(realm, item, ReadAttribute);
        item = dictionary?.Get("removeAttributes") ?? JsValue.Undefined;
        if (!item.IsUndefined()) removeAttributes = List(realm, item, ReadAttribute);
        return new SanitizerElementRule(name, attributes, removeAttributes);
    }

    /// <summary><c>(DOMString or SanitizerElementNamespace)</c>.</summary>
    internal static SanitizerName ReadElement(Realm realm, JsValue value)
        => value is not ObjectInstance && !value.IsNullOrUndefined()
            ? SanitizerName.Html(TypeConverter.ToString(value))
            : ReadNamespaced(realm, value as ObjectInstance, Namespaces.Html);

    /// <summary><c>(DOMString or SanitizerAttributeNamespace)</c>.</summary>
    internal static SanitizerName ReadAttribute(Realm realm, JsValue value)
        => value is not ObjectInstance && !value.IsNullOrUndefined()
            ? SanitizerName.Attribute(TypeConverter.ToString(value))
            : ReadNamespaced(realm, value as ObjectInstance, null);

    /// <summary><c>(DOMString or SanitizerProcessingInstruction)</c>.</summary>
    internal static string ReadProcessingInstruction(Realm realm, JsValue value)
    {
        if (value is not ObjectInstance && !value.IsNullOrUndefined())
        {
            return TypeConverter.ToString(value);
        }

        var target = (value as ObjectInstance)?.Get("target") ?? JsValue.Undefined;
        if (target.IsUndefined())
        {
            Throw.TypeError(realm, "Failed to read the 'target' property from 'SanitizerProcessingInstruction': Required member is undefined.");
        }

        return TypeConverter.ToString(target);
    }

    /// <summary>
    /// <c>get()</c>'s <c>SanitizerConfig</c>, converted to a fresh object whose present members appear in
    /// lexicographic order, per https://webidl.spec.whatwg.org/#js-dictionary.
    /// </summary>
    internal static JsValue ToJs(Engine engine, SanitizerConfiguration config)
    {
        var result = new JsObject(engine);
        if (config.Attributes is { } attributes) Put(result, "attributes", Names(engine, attributes));
        if (config.Comments is { } comments) Put(result, "comments", comments);
        if (config.DataAttributes is { } dataAttributes) Put(result, "dataAttributes", dataAttributes);
        if (config.Elements is { } elements)
        {
            var items = new JsValue[elements.Count];
            for (var i = 0; i < items.Length; i++)
            {
                var element = elements[i];
                var item = Name(engine, element.Name);
                if (element.Attributes is { } local) Put(item, "attributes", Names(engine, local));
                if (element.RemoveAttributes is { } localRemove) Put(item, "removeAttributes", Names(engine, localRemove));
                items[i] = item;
            }

            Put(result, "elements", new JsArray(engine, items));
        }

        if (config.JavascriptUrls is { } javascriptUrls) Put(result, "javascriptURLs", javascriptUrls);
        if (config.ProcessingInstructions is { } instructions) Put(result, "processingInstructions", Targets(engine, instructions));
        if (config.RemoveAttributes is { } removeAttributes) Put(result, "removeAttributes", Names(engine, removeAttributes));
        if (config.RemoveElements is { } removeElements) Put(result, "removeElements", Names(engine, removeElements));
        if (config.RemoveProcessingInstructions is { } removeInstructions) Put(result, "removeProcessingInstructions", Targets(engine, removeInstructions));
        if (config.ReplaceWithChildrenElements is { } replace) Put(result, "replaceWithChildrenElements", Names(engine, replace));
        return result;
    }

    private static SanitizerName ReadNamespaced(Realm realm, ObjectInstance? dictionary, string? defaultNamespace)
    {
        // Base dictionary members, lexicographically: `name`, then `namespace`.
        var nameValue = dictionary?.Get("name") ?? JsValue.Undefined;
        if (nameValue.IsUndefined())
        {
            Throw.TypeError(realm, "Failed to read the 'name' property from the sanitizer item: Required member is undefined.");
        }

        var name = TypeConverter.ToString(nameValue);
        var namespaceValue = dictionary!.Get("namespace");
        var ns = namespaceValue.IsUndefined() ? defaultNamespace
            : namespaceValue.IsNull() ? null
            : TypeConverter.ToString(namespaceValue);
        return new SanitizerName(name, ns is { Length: 0 } ? null : ns);
    }

    private static ObjectInstance? Dictionary(Realm realm, JsValue value, string member)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, "Failed to execute '" + member + "': The provided value is not of type 'SanitizerConfig'.");
            return null;
        }

        return dictionary;
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-sequence — iterated through the iterator protocol.</summary>
    private static List<T> List<T>(Realm realm, JsValue value, Func<Realm, JsValue, T> convert)
    {
        if (value is not ObjectInstance)
        {
            Throw.TypeError(realm, "The provided value cannot be converted to a sequence.");
        }

        var iterator = value.GetIterator(realm);
        var list = new List<T>();
        while (iterator.TryIteratorStepValue(out var item))
        {
            list.Add(convert(realm, item));
        }

        return list;
    }

    private static JsObject Name(Engine engine, SanitizerName name)
    {
        var item = new JsObject(engine);
        Put(item, "name", name.Name);
        Put(item, "namespace", name.Namespace is null ? JsValue.Null : JsString.Create(name.Namespace));
        return item;
    }

    private static JsArray Names(Engine engine, List<SanitizerName> names)
    {
        var items = new JsValue[names.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Name(engine, names[i]);
        }

        return new JsArray(engine, items);
    }

    private static JsArray Targets(Engine engine, List<string> targets)
    {
        var items = new JsValue[targets.Count];
        for (var i = 0; i < items.Length; i++)
        {
            var item = new JsObject(engine);
            Put(item, "target", targets[i]);
            items[i] = item;
        }

        return new JsArray(engine, items);
    }

    private static void Put(ObjectInstance target, string name, JsValue value) => target.CreateDataPropertyOrThrow(name, value);

    private static void Put(ObjectInstance target, string name, string value) => Put(target, name, JsString.Create(value));

    private static void Put(ObjectInstance target, string name, bool value) => Put(target, name, value ? JsBoolean.True : JsBoolean.False);
}
