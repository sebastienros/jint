using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using System.Xml;

namespace Documentation.Samples;

public static class HtmlParserSamples
{
    public static void FirstDocument()
    {
        #region docs:parser-first-document
        var document = MarkupParser.ParseHtml("<main><p>Hello</p></main>");
        var body = document.DocumentElement!.LastChild!;
        Console.WriteLine(MarkupSerializer.ToHtml(body.FirstChild!));
        #endregion
    }

    public static void HtmlFragment()
    {
        #region docs:parser-html-fragment
        var document = Document.CreateHtml();
        var table = document.CreateElement("table");
        document.AppendChild(table);
        var fragment = MarkupParser.ParseHtmlFragment("<tr><td>Hello</td></tr>", table);
        table.AppendChild(fragment);
        Console.WriteLine(MarkupSerializer.ToHtml(table));
        #endregion
    }

    public static void XmlFragment()
    {
        #region docs:parser-xml-fragment
        var document = MarkupParser.ParseXml("<catalog xmlns='urn:catalog'><item/></catalog>");
        var root = document.DocumentElement!;
        var fragment = MarkupParser.ParseXmlFragment("<next/>", root);
        root.AppendChild(fragment);
        Console.WriteLine(((Element) root.LastChild!).NamespaceUri); // urn:catalog
        #endregion
    }

    public static void Mutations()
    {
        #region docs:parser-mutations
        var document = MarkupParser.ParseXml("<root/>");
        var root = document.DocumentElement!;
        using var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            ChildList = true,
            Attributes = true,
            AttributeOldValue = true,
            Subtree = true
        });
        var item = document.CreateElement("item");
        root.AppendChild(item);
        item.SetAttribute("id", "one");
        foreach (var record in subscription.TakeRecords())
        {
            Console.WriteLine(record.Kind);
        }
        #endregion
    }

    public static void Range()
    {
        #region docs:parser-range
        var document = MarkupParser.ParseXml("<root/>");
        var text = document.CreateTextNode("abcd");
        document.DocumentElement!.AppendChild(text);
        var range = document.CreateRange();
        range.SelectNodeContents(new DomNodeIdentity(text));
        text.ReplaceData(1, 1, "B");
        Console.WriteLine(range.GetText()); // aBcd
        #endregion
    }

    public static void Iterator()
    {
        #region docs:parser-iterator
        var document = MarkupParser.ParseXml("<root><item/></root>");
        var iterator = new DomNodeIterator(new DomNodeIdentity(document), whatToShow: 1);
        while (iterator.Next(filter: null) is { } identity)
        {
            Console.WriteLine(((Element) identity.Node!).LocalName);
        }
        #endregion
    }

    public static void Css()
    {
        #region docs:parser-css
        CssStyleSheetSyntax sheet = MarkupParser.ParseCss("@future value; p { color: var(--theme); }");
        foreach (CssRuleSyntax rule in sheet.Rules)
        {
            Console.WriteLine(sheet.Source.Substring(rule.Span.Start, rule.Span.Length));
        }
        #endregion
    }

    public static void XPath()
    {
        #region docs:parser-xpath
        var document = MarkupParser.ParseXml("<catalog xmlns='urn:catalog'><item id='one'/></catalog>");
        var namespaces = new XmlNamespaceManager(new NameTable());
        namespaces.AddNamespace("c", "urn:catalog");
        var expression = NativeXPath.Compile("//c:item", namespaces);
        IReadOnlyList<object> items = NativeXPath.Select(document, expression);
        Console.WriteLine(items.Count);
        Console.WriteLine(NativeXPath.Evaluate(document, "count(//c:item)", namespaces).NumberValue);
        #endregion
    }

    public static void Serialization()
    {
        #region docs:parser-serialization
        var document = MarkupParser.ParseXml("<root><item/></root>");
        string xml = MarkupSerializer.ToXml(document, requireWellFormed: true,
            limits: new SerializationLimits { MaxOutputCharacters = 100_000 });
        Console.WriteLine(xml);
        #endregion
    }

    public static void ShadowSerialization()
    {
        #region docs:parser-shadow-serialization
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var shadow = host.AttachShadow(new ShadowRootInit(ShadowRootMode.Closed, Serializable: true));
        shadow.AppendChild(document.CreateTextNode("shadow content"));
        string html = MarkupSerializer.ToHtml(host,
            new HtmlSerializationOptions(shadowRoots: new[] { shadow }));
        Console.WriteLine(html);
        #endregion
    }

    public static void Limits()
    {
        #region docs:parser-limits
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var diagnostics = new ParseDiagnosticCollector(capacity: 20);
        var options = new HtmlParseOptions
        {
            Limits = new ParseLimits { MaxInputCharacters = 100_000, MaxTokenCharacters = 20_000 },
            Diagnostics = diagnostics
        };
        var document = MarkupParser.ParseHtml("<p>Hello</p>", options, cancellation.Token);
        Console.WriteLine(MarkupSerializer.ToHtmlChildren(document.DocumentElement!.LastChild!));
        #endregion
    }
}
