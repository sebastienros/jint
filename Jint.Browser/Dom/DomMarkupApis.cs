using Jint.Browser.Dom.Views;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Sanitization;
using Jint.HtmlParser.Serialization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Browser.Dom;

/// <summary>
/// HTML's shadow-root-aware markup members: <c>getHTML</c>, <c>setHTML</c>, <c>setHTMLUnsafe</c> on
/// <c>Element</c> and <c>ShadowRoot</c>, and the <c>Document.parseHTML</c> / <c>parseHTMLUnsafe</c> statics.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#html-serialization-methods and
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#safe-and-unsafe-html-parsing. Every
/// sanitizing step is the parser package's <see cref="HtmlSanitizer"/>; what is here is the WebIDL
/// conversions and the tree insertion the page owns.
/// </remarks>
internal static class DomMarkupApis
{
    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-element-gethtml</summary>
    internal static JsValue GetHtml(DomRealm realm, Node node, JsValue[] arguments, string member)
    {
        // GetHTMLOptions, lexicographically: serializableShadowRoots, then shadowRoots.
        var options = Dictionary(realm, DomConvert.At(arguments, 0), member, "GetHTMLOptions");
        var serializable = options?.Get("serializableShadowRoots") is { } flag && !flag.IsUndefined()
            && TypeConverter.ToBoolean(flag);
        List<ShadowRoot>? roots = null;
        var sequence = options?.Get("shadowRoots") ?? JsValue.Undefined;
        if (!sequence.IsUndefined())
        {
            if (sequence is not ObjectInstance)
            {
                Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member
                    + "': The provided value cannot be converted to a sequence.");
            }

            roots = [];
            var iterator = sequence.GetIterator(realm.OwningRealm);
            while (iterator.TryIteratorStepValue(out var item))
            {
                if (item is not IDomWrapper { DomTarget: ShadowRoot root })
                {
                    Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member
                        + "': Failed to convert value to 'ShadowRoot'.");
                    return JsValue.Undefined;
                }

                roots.Add(root);
            }
        }

        var document = node.OwnerDocument!;
        var scripting = realm.ScriptingEnabled && DomBrowsingContext.Of(document) is not null;
        return JsString.Create(HtmlMarkupSerializer.SerializeChildren(node,
            new HtmlSerializationOptions(scripting, serializable, roots),
            checkpoint: _ => realm.Engine.Constraints.Check(), cancellationToken: realm.CancellationToken));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-element-sethtml and
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-element-sethtmlunsafe.
    /// </summary>
    internal static JsValue SetElementHtml(DomRealm realm, Element element, JsValue[] arguments, bool safe, string member)
    {
        var html = DomConvert.RequiredText(arguments, 0, member);
        var (runScripts, sanitizer) = ReadSetOptions(realm, DomConvert.At(arguments, 1), safe, member);
        Node target = element.TemplateContent ?? (Node) element;
        SetAndFilter(realm, element, target, html, sanitizer, safe, runScripts);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-shadowroot-sethtml and
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-shadowroot-sethtmlunsafe.
    /// </summary>
    internal static JsValue SetShadowRootHtml(DomRealm realm, ShadowRoot root, JsValue[] arguments, bool safe, string member)
    {
        var html = DomConvert.RequiredText(arguments, 0, member);
        var (runScripts, sanitizer) = ReadSetOptions(realm, DomConvert.At(arguments, 1), safe, member);
        SetAndFilter(realm, root.Host, root, html, sanitizer, safe, runScripts);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-parsehtml and
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-parsehtmlunsafe.
    /// </summary>
    internal static JsValue ParseHtml(DomRealm realm, JsValue[] arguments, bool safe)
    {
        var member = safe ? "Document.parseHTML" : "Document.parseHTMLUnsafe";
        var html = DomConvert.RequiredText(arguments, 0, member);
        var options = Dictionary(realm, DomConvert.At(arguments, 1), member, safe ? "SetHTMLOptions" : "ParseHTMLUnsafeOptions");
        var sanitizer = ReadSanitizer(realm, options, safe, member);

        // Steps 2-3: an HTML document whose URL is about:blank, with no browsing context, so scripting is
        // disabled for it; its origin is the relevant global's, as DOMParser's documents' are.
        var document = Document.CreateHtml();
        DomDocumentMetadata.Initialize(document, DomDocumentMetadata.CreatorOrigin(realm));
        var session = new HtmlParserSession(document, new HtmlParseOptions { ScriptingEnabled = false },
            new HtmlDocumentContext(AllowDeclarativeShadowRoots: true));
        session.AppendInput(html, isFinal: true);
        while (true)
        {
            realm.Engine.Constraints.Check();
            var step = session.Drive(4096, realm.CancellationToken);
            if (step.Kind == HtmlParseStepKind.Complete) break;
            if (step.Kind != HtmlParseStepKind.Yielded)
            {
                throw new InvalidOperationException("The native HTML parser could not complete the supplied input: " + step.Kind + ".");
            }
        }

        HtmlSanitizer.Sanitize(document, sanitizer, safe, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return realm.WrapNode(document);
    }

    /// <summary>
    /// <c>Document.parseHTML</c> and <c>Document.parseHTMLUnsafe</c>: WebIDL static operations, so own
    /// properties of the interface object that are writable, enumerable and configurable
    /// (https://webidl.spec.whatwg.org/#es-operations).
    /// </summary>
    internal static void InstallDocumentStatics(DomRealm realm, ObjectInstance interfaceObject)
    {
        interfaceObject.DefineOwnPropertyUnchecked("parseHTML", new PropertyDescriptor(
            new ClrFunction(realm.Engine, "parseHTML", (_, arguments) => ParseHtml(realm, arguments, safe: true),
                length: 1, lengthFlags: PropertyFlag.Configurable),
            PropertyFlag.ConfigurableEnumerableWritable));
        interfaceObject.DefineOwnPropertyUnchecked("parseHTMLUnsafe", new PropertyDescriptor(
            new ClrFunction(realm.Engine, "parseHTMLUnsafe", (_, arguments) => ParseHtml(realm, arguments, safe: false),
                length: 1, lengthFlags: PropertyFlag.Configurable),
            PropertyFlag.ConfigurableEnumerableWritable));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#set-and-filter-html, from step 2
    /// (the sanitizer, step 3, has been read with the options).
    /// </summary>
    private static void SetAndFilter(DomRealm realm, Element context, Node target, string html,
        SanitizerConfiguration sanitizer, bool safe, bool runScripts)
    {
        // Step 2: setHTML never puts children under a script element, of either namespace.
        if (safe && context.LocalName == "script" && context.NamespaceUri is Namespaces.Html or Namespaces.Svg)
        {
            return;
        }

        List<Element>? hosts = null;
        var fragment = DomFragmentParser.ParseHtmlWithShadowRoots(realm, html, context, target, runScripts,
            host => (hosts ??= []).Add(host));
        HtmlSanitizer.Sanitize(fragment, sanitizer, safe, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();

        realm.RecordSubtree(fragment);
        target.ReplaceChildren(fragment);
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, target);

        if (hosts is null)
        {
            return;
        }

        var parser = Runtime.PageRuntime.FindBrowsingContext(realm.Engine, target.OwnerDocument!)?.Parser;
        foreach (var host in hosts)
        {
            // A root the sanitizer removed, with its host, was never inserted and loads nothing.
            if (host.AttachedShadowRoot is { } root && parser is not null)
            {
                parser.WatchShadowRoot(root);
            }
        }
    }

    /// <summary>
    /// <c>SetHTMLOptions</c> or <c>SetHTMLUnsafeOptions</c>, lexicographically: <c>runScripts</c> (the unsafe
    /// variant only), then <c>sanitizer</c>, then "get a sanitizer instance from options".
    /// </summary>
    private static (bool RunScripts, SanitizerConfiguration Sanitizer) ReadSetOptions(DomRealm realm, JsValue value,
        bool safe, string member)
    {
        var options = Dictionary(realm, value, member, safe ? "SetHTMLOptions" : "SetHTMLUnsafeOptions");
        var runScripts = false;
        if (!safe && options?.Get("runScripts") is { } flag && !flag.IsUndefined())
        {
            runScripts = TypeConverter.ToBoolean(flag);
        }

        return (runScripts, ReadSanitizer(realm, options, safe, member));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#get-a-sanitizer-instance-from-options:
    /// the <c>sanitizer</c> member, <c>(Sanitizer or SanitizerConfig or SanitizerPresets)</c>, defaulting to
    /// <c>"default"</c> for the safe methods and to <c>{}</c> for the unsafe ones.
    /// </summary>
    private static SanitizerConfiguration ReadSanitizer(DomRealm realm, ObjectInstance? options, bool safe, string member)
    {
        var jsRealm = realm.OwningRealm;
        var value = options?.Get("sanitizer") ?? JsValue.Undefined;
        SanitizerConfiguration configuration;
        if (value.IsUndefined())
        {
            if (safe)
            {
                return SanitizerBuiltins.SafeDefault();
            }

            configuration = new SanitizerConfiguration();
        }
        else if (value is JsSanitizer sanitizer)
        {
            return sanitizer.Configuration;
        }
        else if (value.IsNull() || value is ObjectInstance)
        {
            configuration = SanitizerConversion.ReadConfig(jsRealm, value, member);
        }
        else
        {
            SanitizerConversion.RequirePreset(jsRealm, value, member);
            return SanitizerBuiltins.SafeDefault();
        }

        // A dictionary is configured afresh, with permissive defaults only for the unsafe methods.
        SanitizerConversion.Configure(jsRealm, configuration, permissiveDefaults: !safe, "Failed to execute '" + member + "'");
        return configuration;
    }

    private static ObjectInstance? Dictionary(DomRealm realm, JsValue value, string member, string type)
    {
        if (value.IsNullOrUndefined())
        {
            return null;
        }

        if (value is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member + "': The provided value is not of type '" + type + "'.");
            return null;
        }

        return dictionary;
    }
}
