using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using Jint.HtmlParser;

namespace HtmlParserPackageConsumer;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Require(typeof(Program).Assembly.GetName().GetPublicKey()?.Length is 0,
                "The consumer assembly must be unsigned.");
            var parserAssembly = typeof(Document).Assembly;
            Require(parserAssembly.GetName().Name == "Jint.HtmlParser", "The parser package assembly was not loaded.");

            Require(parserAssembly.GetName().GetPublicKeyToken()?.Length > 0,
                "The parser package must remain signed.");

            CheckHtml();
            CheckXmlAndSvg();
            CheckNotationSurface();
            CheckDtdProcessingInstructions();
            CheckFragmentOwnership();
            CheckMutationSubscriptions();
            CheckLiveTraversal();
            CheckCssSyntax();
            CheckXPath();
            CheckSerialization();
#if BROWSER_PROBE
            CheckBrowserAsync().GetAwaiter().GetResult();
#endif
            Console.WriteLine("ALL PARSER PACKAGE PROBES PASSED");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

#if BROWSER_PROBE
    private static async System.Threading.Tasks.Task CheckBrowserAsync()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='probe'>before</p><script>document.querySelector('#probe').textContent = 'packed browser';</script>");
        Require(await page.EvaluateAsync<string>("document.querySelector('#probe').textContent") == "packed browser",
            "Packed Browser/parser integration failed.");
        Console.WriteLine("BROWSER PACKAGE PROBE PASSED");
    }
#endif

    private static void CheckLiveTraversal()
    {
        var doc = Document.CreateHtml();
        var parent = doc.CreateElement("div");
        doc.AppendChild(parent);
        var a = doc.CreateTextNode("abc");
        var b = doc.CreateTextNode("def");
        parent.AppendChild(a);
        parent.AppendChild(b);
        var identity = new DomNodeIdentity(a);
        Require(identity == new DomNodeIdentity(a) && identity != new DomNodeIdentity(b), "reference identity operators");
        Require(identity.IsValid && identity.Node == a && identity.Attribute is null, "node identity projection");
        var range = doc.CreateRange();
        range.SelectNodeContents(new(b));
        parent.Normalize();
        Require(range.Start == new BoundaryPoint(new(a), 3) && range.End.Offset == 6, "normalize endpoint transfer");
        var tail = a.SplitText(4);
        Require(range.End == new BoundaryPoint(new(tail), 2), "split transfer");
        Require(a.Length == 4 && a.SubstringData(1, uint.MaxValue) == "bcd", "character data public reads");
        a.ReplaceData(1, 1, "B");
        Require(range.GetText() == "def", "live UTF-16 replacement");
        var clone = range.CloneRange();
        Require(range.CompareBoundaryPoints(0, clone) == 0, "clone points");
        foreach (ushort selector in new ushort[] { 0, 1, 2, 3 }) range.CompareBoundaryPoints(selector, clone);
        Require(range.ComparePoint(new(a), 0) == -1 && range.IsPointInRange(new(a), 3), "point queries");
        Require(range.IntersectsNode(new(a)) && range.GetCommonAncestor().Node == parent, "ordinary intersection and common ancestor");
        Require(range.CloneContents().ChildCount == 2, "partial content clone");
        var staticRange = new DomStaticRange(range.Start, range.End);
        Require(staticRange.IsValid(), "static range validity");
        var invalidStatic = new DomStaticRange(new(new(a), uint.MaxValue), new(new(doc), 0));
        Require(!invalidStatic.IsValid(), "unsigned static offsets");
        range.SetStartBefore(new(a));
        range.SetEndAfter(new(tail));
        range.SetStartAfter(new(a));
        range.SetEndBefore(new(tail));
        Require(range.Collapsed, "boundary setters");
        range.SelectNode(new(tail));
        var extracted = range.ExtractContents();
        Require(extracted.FirstChild == tail && tail.ParentNode == extracted, "move identity");
        parent.AppendChild(tail);
        range.SelectNodeContents(new(a));
        range.Collapse(true);
        range.InsertNode(new(doc.CreateElement("i")));
        Require(!range.Collapsed, "collapsed insertion expansion");
        var wrapper = doc.CreateElement("span");
        range.SelectNodeContents(new(tail));
        range.SurroundContents(new(wrapper));
        Require(wrapper.FirstChild is Text && range.Start.Container.Node == parent, "surround native contents");
        range.DeleteContents();
        range.Detach();
        Require(range.Collapsed, "delete and detach");
        var iterator = new DomNodeIterator(new(parent), uint.MaxValue);
        TraversalFilter filter = _ => 1;
        Require(iterator.Root.Node == parent && iterator.WhatToShow == uint.MaxValue && iterator.PointerBeforeReference, "iterator initial state");
        Require(iterator.Next(filter)!.Value.Node == parent && iterator.Reference.Node == parent, "iterator reference identity");
        iterator.Next(null);
        iterator.Previous(null);
        iterator.Detach();
        var walker = new DomTreeWalker(new(parent), uint.MaxValue);
        Require(walker.Root.Node == parent && walker.WhatToShow == uint.MaxValue, "walker initial state");
        walker.FirstChild(null);
        walker.Parent(null);
        walker.LastChild(null);
        walker.PreviousSibling(null);
        walker.NextSibling(null);
        walker.Previous(null);
        walker.Next(null);
        walker.Current = new(doc.CreateTextNode("outside"));
        Require(walker.Next(null) is null, "out-of-root current");
        var attribute = doc.CreateAttribute("x");
        var attrIdentity = new DomNodeIdentity(attribute);
        range.SetStart(attrIdentity, 0);
        Require(range.Collapsed && range.Start.Container.Attribute == attribute, "attribute boundary identity");
        var attrIterator = new DomNodeIterator(attrIdentity, 2);
        Require(attrIterator.Next(null) == attrIdentity && attrIterator.Next(null) is null, "attribute singleton traversal");
        var other = Document.CreateHtml();
        var detached = doc.CreateTextNode("abcd");
        range.SelectNodeContents(new(detached));
        other.AdoptNode(detached);
        detached.Data = "abcd";
        Require(range.Collapsed, "adopted endpoint repair");
        try
        {
            range.SetEnd(new(detached), uint.MaxValue);
            throw new InvalidOperationException("uint overflow accepted");
        }
        catch (DomException error) when (error.Name == "IndexSizeError")
        {
        }
        var xml = Document.CreateXml();
        var cdata = xml.CreateCDataSection("abcd");
        Require(cdata.SplitText(2).Data == "cd", "public CDATA split");
        var pi = xml.CreateProcessingInstruction("x", "abc");
        pi.ReplaceData(1, 1, "z");
        Require(pi.SubstringData(0, pi.Length) == "azc", "public PI replace");
        var comment = xml.CreateComment("abc");
        comment.ReplaceData(1, 1, "z");
        Require(comment.SubstringData(0, comment.Length) == "azc", "public comment replace");
    }

    private static void CheckDtdProcessingInstructions()
    {
        const string source = "<!DOCTYPE r [<?p data?><!ENTITY % e '<?nested value?>'>%e;]><r/>";
        var document = MarkupParser.ParseXml(source);
        IReadOnlyList<XmlDtdProcessingInstruction> records = document.XmlDtdProcessingInstructions;
        Require(records.Count == 2 && records[0].Target == "p" && records[0].Data == "data" &&
            records[0].Offset == source.IndexOf("<?p", StringComparison.Ordinal) &&
            records[1].Target == "nested" && records[1].Data == "value" &&
            records[1].Offset == source.IndexOf("%e;", StringComparison.Ordinal),
            "DTD processing instruction data or provenance was lost.");
        Require(records is IList<XmlDtdProcessingInstruction> list && list.IsReadOnly &&
            ReferenceEquals(records, ((Document) document.CloneNode(true)).XmlDtdProcessingInstructions) &&
            document.ChildNodes.Count() == 2, "DTD processing instruction metadata is mutable or changed the DOM.");
    }

    private static void CheckCssSyntax()
    {
        const string source = "@future value; p {color:var(--color)}";
        var sheet = MarkupParser.ParseCss(source);
        Require(sheet.Source == source && sheet.Rules.Count == 2, "Whole-sheet CSS syntax was not preserved.");
    }

    private static void CheckSerialization()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var shadow = host.AttachShadow(new(ShadowRootMode.Closed, DelegatesFocus: true,
            Serializable: true, SlotAssignment: SlotAssignmentMode.Manual, Clonable: true));
        Require(ReferenceEquals(shadow.Host, host) && shadow.ParentNode is null &&
            ReferenceEquals(shadow.OwnerDocument, document) && shadow.Mode == ShadowRootMode.Closed &&
            shadow.DelegatesFocus && shadow.Serializable && shadow.Clonable &&
            shadow.SlotAssignment == SlotAssignmentMode.Manual && host.OpenShadowRoot is null,
            "Public shadow attachment lost native identity or metadata.");
        var openHost = document.CreateElement("span");
        Require(ReferenceEquals(openHost.AttachShadow(new(ShadowRootMode.Open)), openHost.OpenShadowRoot),
            "An open shadow root cannot be acquired.");
        shadow.AppendChild(document.CreateTextNode("shadow"));
        host.AppendChild(document.CreateTextNode("&"));
        var roots = new List<ShadowRoot> { shadow, shadow };
        var options = new HtmlSerializationOptions(shadowRoots: roots);
        roots.Clear();
        Require(options.ShadowRoots.Count == 1 && ReferenceEquals(options.ShadowRoots[0], shadow) &&
            options.ShadowRoots is IList<ShadowRoot> readOnly && readOnly.IsReadOnly &&
            !options.ScriptingEnabled && !options.SerializableShadowRoots, "Serialization options are not immutable.");
        const string selected = "<template shadowrootmode=\"closed\" shadowrootdelegatesfocus=\"\"" +
            " shadowrootserializable=\"\" shadowrootslotassignment=\"manual\" shadowrootclonable=\"\">shadow</template>";
        var expected = "<div>" + selected + "&amp;</div>";
        Require(MarkupSerializer.ToHtml(host) == "<div>&amp;</div>" &&
            MarkupSerializer.ToHtml(host, options) == expected &&
            MarkupSerializer.ToHtmlChildren(host, new(serializableShadowRoots: true)) == selected + "&amp;" &&
            MarkupSerializer.ToHtml(shadow) == "shadow", "HTML serialization or shadow selection failed.");
        Require(MarkupSerializer.ToXml(host, true) == "<div xmlns=\"http://www.w3.org/1999/xhtml\">&amp;</div>" &&
            MarkupSerializer.ToXmlChildren(host, true) == "&amp;" && MarkupSerializer.ToXml(shadow) == "shadow" &&
            MarkupSerializer.ToXml(document.CreateAttribute("a"), true) == "", "XML serialization dispatch failed.");
        Require(MarkupSerializer.ToHtml(host, options, new() { MaxOutputCharacters = expected.Length }) == expected,
            "Exact HTML output bound rejected its output.");
        try
        {
            MarkupSerializer.ToHtml(host, options, new() { MaxOutputCharacters = expected.Length - 1 });
            throw new InvalidOperationException("HTML output escaped its bound.");
        }
        catch (SerializationLimitException error)
        {
            Require(error.Limit == expected.Length - 1 && error.Observed == expected.Length,
                "Serialization bound metadata changed.");
        }
        var xml = MarkupParser.ParseXml("<r xmlns='urn:r'><x/></r>");
        Require(MarkupSerializer.ToXmlChildren(xml.DocumentElement!, true) == "<x xmlns=\"urn:r\"/>",
            "XML child output relies on out-of-range namespace declarations.");
        var template = document.CreateElement("template");
        template.TemplateContent!.AppendChild(document.CreateTextNode("<&"));
        Require(MarkupSerializer.ToHtmlChildren(template) == "&lt;&amp;" &&
            MarkupSerializer.ToXmlChildren(template) == "&lt;&amp;", "Template serialization ignored its content.");
        var noscript = document.CreateElement("noscript");
        noscript.AppendChild(document.CreateTextNode("<&"));
        Require(MarkupSerializer.ToHtml(noscript) == "<noscript>&lt;&amp;</noscript>" &&
            MarkupSerializer.ToHtml(noscript, new(scriptingEnabled: true)) == "<noscript><&</noscript>",
            "Scripting context did not select noscript escaping.");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Expect<OperationCanceledException>(() => MarkupSerializer.ToHtml(host, cancellationToken: cancellation.Token));
        Expect<OperationCanceledException>(() => MarkupSerializer.ToHtmlChildren(host, cancellationToken: cancellation.Token));
        Expect<OperationCanceledException>(() => MarkupSerializer.ToXml(host, cancellationToken: cancellation.Token));
        Expect<OperationCanceledException>(() => MarkupSerializer.ToXmlChildren(host, cancellationToken: cancellation.Token));
        Expect<OperationCanceledException>(() => MarkupSerializer.ToXml(document.CreateAttribute("a"), cancellationToken: cancellation.Token));
        Expect<ArgumentOutOfRangeException>(() => _ = new SerializationLimits { MaxOutputCharacters = -1 });
        Expect<DomException>(() => MarkupSerializer.ToXml(Document.CreateXml(), true));
        Expect<ArgumentException>(() => MarkupSerializer.ToHtmlChildren(document.CreateTextNode("x")));
        Expect<ArgumentException>(() => _ = new HtmlSerializationOptions(shadowRoots: new ShadowRoot[] { null! }));
    }

    private static void CheckXPath()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!ATTLIST item key ID #IMPLIED>]>" +
            "<r xmlns:p='urn:p'><item key='one'>a<![CDATA[b]]></item><p:next/></r>");
        var root = document.DocumentElement!;
        var item = (Element) root.FirstChild!;
        var id = item.GetAttributeNode("key")!;
        Require(ReferenceEquals(NativeXPath.Select(document, "id('one')").Single(), item), "XPath ID typing was lost.");
        var expression = NativeXPath.Compile("//item/text()");
        Require(expression.Source == "//item/text()" && expression.ReturnType == XPathResultType.NodeSet,
            "Prepared XPath metadata changed.");
        var snapshot = NativeXPath.Evaluate(document, expression);
        Require(snapshot.ResultType == XPathResultType.NodeSet && snapshot.Nodes.Count == 1 &&
            ReferenceEquals(snapshot.Nodes[0], item.FirstChild) && snapshot.FirstNodeStringValue == "ab",
            "XPath text-run identity or value changed.");
        Require(NativeXPath.Evaluate(document, "count(//item)").NumberValue == 1 &&
            NativeXPath.Evaluate(document, "boolean(//item)").BooleanValue &&
            NativeXPath.Evaluate(document, "string(//item)").StringValue == "ab", "XPath scalar kinds failed.");
        Expect<InvalidOperationException>(() => _ = snapshot.NumberValue);
        Expect<XPathException>(() => NativeXPath.Select(document, "count(//item)"));
        Require(snapshot.Nodes is IList<object> readOnly && readOnly.IsReadOnly, "XPath nodes are not read-only.");
        ((Text) item.FirstChild!).Data = "new";
        Require(snapshot.FirstNodeStringValue == "ab", "An XPath snapshot retained a live string-value.");

        var resolver = new XmlNamespaceManager(new NameTable());
        resolver.AddNamespace("p", "urn:p");
        var namespaced = NativeXPath.Compile("//p:next", resolver);
        Require(ReferenceEquals(NativeXPath.Select(document, namespaced).Single(), item.NextSibling),
            "XPath namespace resolution failed.");
        var other = MarkupParser.ParseXml("<r xmlns:p='urn:p'><p:next/></r>");
        Require(ReferenceEquals(NativeXPath.Evaluate(other, namespaced).Nodes.Single(), other.DocumentElement!.FirstChild),
            "A prepared expression retained its first document.");
        Require(ReferenceEquals(NativeXPath.Select(item, "@key").Single(), id), "XPath replaced attribute identity.");
        id.Value = "two";
        Require(NativeXPath.Select(document, "id('one')").Count == 0 &&
            ReferenceEquals(NativeXPath.Select(document, "id('two')").Single(), item), "XPath ID mutation was stale.");
        other.DocumentElement!.AppendChild(other.ImportNode(item, true));
        Require(NativeXPath.Select(other, "id('two')").Count == 1, "Import lost XPath ID typing.");
        var detached = document.CreateAttribute("detached");
        detached.Value = "value";
        var self = NativeXPath.Compile("self::node()");
        Require(ReferenceEquals(NativeXPath.Select(detached, self).Single(), detached) &&
            ReferenceEquals(NativeXPath.Evaluate(detached, self).Nodes.Single(), detached) &&
            NativeXPath.Evaluate(detached, "count(following::node()) + 1").NumberValue == 1 &&
            NativeXPath.Select(detached, "following::node()").Count == 0, "Detached XPath attribute axes failed.");

        var binding = (XPathNamespaceBinding) NativeXPath.Select(item, "namespace::p").Single();
        Require(ReferenceEquals(binding.OwnerElement, item) && binding.Prefix == "p" && binding.NamespaceUri == "urn:p",
            "XPath namespace identity was not exposed.");
        Require(NativeXPath.Evaluate(binding, "string(.)").StringValue == "urn:p" &&
            NativeXPath.Evaluate(binding, self).Nodes.Single() is XPathNamespaceBinding &&
            ReferenceEquals(NativeXPath.Select(binding, "..").Single(), item) &&
            NativeXPath.Select(binding, self).Single() is XPathNamespaceBinding, "Namespace-context overloads failed.");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:changed");
        Expect<InvalidOperationException>(() => NativeXPath.Select(binding, self));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Expect<OperationCanceledException>(() => NativeXPath.Compile(".", cancellationToken: cancelled.Token));
        Expect<OperationCanceledException>(() => NativeXPath.Evaluate(document, self, cancelled.Token));
        Expect<OperationCanceledException>(() => NativeXPath.Select(detached, self, cancelled.Token));
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
    }

    private static void CheckHtml()
    {
        var options = new HtmlParseOptions();
        Require(!options.ScriptingEnabled && options.Limits.MaxEntityExpansionCharacters == 10_000_000 &&
            options.Diagnostics is null, "HTML option defaults changed.");
        Require(new XmlParseOptions().Limits.MaxEntityExpansionCharacters == 10_000_000 &&
            new CssParseOptions().Limits.MaxEntityExpansionCharacters == 10_000_000 &&
            ParseLimits.Unbounded.MaxEntityExpansionCharacters == 0, "Entity-expansion limit defaults changed.");
        var empty = MarkupParser.ParseHtml("");
        Require(empty.Kind == DocumentKind.Html && empty.ContentType == "text/html" &&
            empty.DocumentElement?.LocalName == "html" && empty.DocumentElement.ChildCount == 2,
            "Empty HTML did not create implied structure.");
        var diagnostics = new ParseDiagnosticCollector(1);
        var recovered = MarkupParser.ParseHtml("<p>one<p>two</unexpected></unexpected>", new() { Diagnostics = diagnostics });
        Require(recovered.DocumentElement?.LastChild?.ChildCount == 2 &&
            diagnostics.Items.Count == 1 && diagnostics.IsTruncated, "HTML recovery/diagnostic bound failed.");
        var owner = Document.CreateHtml();
        var context = owner.CreateElement("table");
        var old = owner.CreateComment("unchanged");
        context.AppendChild(old);
        var fragment = MarkupParser.ParseHtmlFragment("<tr><td>x", context);
        Require(ReferenceEquals(fragment.OwnerDocument, owner) && fragment.ParentNode is null &&
            fragment.FirstChild is Element { LocalName: "tbody" } &&
            ReferenceEquals(fragment.FirstChild.OwnerDocument, owner) && ReferenceEquals(context.FirstChild, old),
            "Contextual HTML fragment ownership or recovery failed.");
        var svg = owner.CreateElementNS(Namespaces.Svg, "svg");
        Require(MarkupParser.ParseHtmlFragment("<circle/>", svg).FirstChild is Element { NamespaceUri: Namespaces.Svg },
            "Foreign HTML fragment failed.");
        var template = owner.CreateElement("template");
        var templateFragment = MarkupParser.ParseHtmlFragment("<p>x", template);
        Require(ReferenceEquals(templateFragment.OwnerDocument, owner) && template.TemplateContent?.ChildCount == 0,
            "Public template context changed the result owner or existing content.");
        foreach (var scripting in new[] { false, true })
        {
            var document = MarkupParser.ParseHtml("<script>throw 1</script><div><template shadowrootmode=open>x</template></div>",
                new() { ScriptingEnabled = scripting });
            Require(document.DocumentElement?.FirstChild?.FirstChild?.FirstChild is Text { Data: "throw 1" } &&
                document.DocumentElement.LastChild?.FirstChild?.FirstChild is Element { LocalName: "template" },
                "Public HTML changed script inertness or declarative-shadow permission.");
        }
        MarkupParser.ParseHtml("<br>", new() { Limits = new() { MaxInputCharacters = 4, MaxTokenCharacters = 4, MaxNestingDepth = 3 } });
        foreach (var (limits, kind) in new[]
        {
            (new ParseLimits { MaxInputCharacters = 3 }, ParseLimitKind.InputCharacters),
            (new ParseLimits { MaxTokenCharacters = 3 }, ParseLimitKind.TokenCharacters),
            (new ParseLimits { MaxNestingDepth = 2 }, ParseLimitKind.NestingDepth)
        })
        {
            try
            {
                MarkupParser.ParseHtml("<br>", new() { Limits = limits });
                throw new InvalidOperationException("HTML exceeded its configured bound.");
            }
            catch (ParseLimitException error) { Require(error.Kind == kind, "HTML limit taxonomy changed."); }
        }
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            MarkupParser.ParseHtml("", cancellationToken: cancellation.Token);
            throw new InvalidOperationException("HTML ignored pre-cancellation.");
        }
        catch (OperationCanceledException) { }
        try
        {
            MarkupParser.ParseHtmlFragment("", context, cancellationToken: cancellation.Token);
            throw new InvalidOperationException("HTML fragment ignored pre-cancellation.");
        }
        catch (OperationCanceledException) { }
    }

    private static void CheckNotationSurface()
    {
        var document = MarkupParser.ParseXml("<root/>");
        IReadOnlyList<XmlNotationDeclaration> notations = document.XmlNotations;
        Require(notations.Count == 0, "A notation-free document has notation records.");
        foreach (var notation in notations)
        {
            Require(notation.Name.Length != 0 && (notation.PublicId is not null || notation.SystemId is not null),
                "A notation declaration lacks its name or external identifier.");
        }
    }

    private static void CheckXmlAndSvg()
    {
        var xml = MarkupParser.ParseXml("<?xml version='1.0'?><anything xmlns='urn:test'><child/></anything>");
        Require(xml.ContentType == "application/xml" && xml.CharacterSet == "UTF-8",
            "Generic XML document metadata changed.");
        Require(xml.DocumentElement?.LocalName == "anything" &&
            xml.DocumentElement.NamespaceUri == "urn:test", "Generic XML root was not preserved.");

        var svg = MarkupParser.ParseSvg("<s:svg xmlns:s='http://www.w3.org/2000/svg'><s:path/></s:svg>");
        var svgRoot = svg.DocumentElement ?? throw new InvalidOperationException("Strict SVG has no root.");
        Require(svg.ContentType == "image/svg+xml" && svgRoot.LocalName == "svg" &&
            svgRoot.NamespaceUri == Namespaces.Svg, "Strict SVG root was not recognized.");
        Require(svgRoot.FirstChild is Element path && path.LocalName == "path" &&
            path.NamespaceUri == Namespaces.Svg, "SVG child namespace was not preserved.");

        var generic = MarkupParser.ParseXml("<svg/>");
        Require(generic.DocumentElement?.NamespaceUri is null,
            "Generic XML must not infer the SVG namespace from a root spelling.");
        try
        {
            MarkupParser.ParseSvg("<svg/>");
            throw new InvalidOperationException("Strict SVG accepted an unnamespaced root.");
        }
        catch (MarkupParseException error) when (error.Code == "xml/svg-root-required" && error.Offset == 0)
        {
        }
    }

    private static void CheckFragmentOwnership()
    {
        var owner = MarkupParser.ParseXml("<root xmlns='urn:default' xmlns:p='urn:prefix'><old/></root>");
        var context = owner.DocumentElement!;
        var before = context.ChildCount;
        var fragment = MarkupParser.ParseXmlFragment("<p:item/><local/>text", context);
        Require(ReferenceEquals(fragment.OwnerDocument, owner) && context.ChildCount == before,
            "Fragment parsing changed context ownership or children.");
        Require(fragment.ChildCount == 3 && fragment.FirstChild is Element first &&
            first.NamespaceUri == "urn:prefix" && first.LocalName == "item",
            "Fragment prefix binding was not inherited.");
        Require(fragment.FirstChild?.NextSibling is Element second &&
            second.NamespaceUri == "urn:default" && second.LocalName == "local",
            "Fragment default namespace was not inherited.");
        Require(fragment.LastChild is Text text && text.Data == "text",
            "Fragment text was not retained.");
    }

    private static void CheckMutationSubscriptions()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        using var subscription = document.ObserveMutations(root, new MutationObserverOptions
        {
            ChildList = true,
            Subtree = true,
            Attributes = true,
            AttributeOldValue = true,
            CharacterData = true,
            CharacterDataOldValue = true
        });

        var child = document.CreateElement("child");
        root.AppendChild(child);
        child.SetAttribute("id", "one");
        child.SetAttribute("id", "two");
        var text = document.CreateTextNode("before");
        child.AppendChild(text);
        text.Data = "after";

        var records = subscription.TakeRecords();
        Require(records.Count == 5, "Unexpected mutation record count.");
        Require(records[0].Kind == MutationRecordKind.ChildList &&
            ReferenceEquals(records[0].Target, root) && records[0].AddedNodes.Count == 1 &&
            ReferenceEquals(records[0].AddedNodes[0], child), "Child insertion record is incorrect.");
        Require(records[1].Kind == MutationRecordKind.Attributes && records[1].AttributeName == "id" &&
            records[1].OldValue is null, "Initial attribute record is incorrect.");
        Require(records[2].Kind == MutationRecordKind.Attributes && records[2].OldValue == "one",
            "Attribute old value was not captured.");
        Require(records[3].Kind == MutationRecordKind.ChildList &&
            ReferenceEquals(records[3].AddedNodes[0], text), "Text insertion record is incorrect.");
        Require(records[4].Kind == MutationRecordKind.CharacterData &&
            records[4].OldValue == "before", "Character-data old value was not captured.");
        Require(subscription.TakeRecords().Count == 0 && records.Count == 5,
            "Draining mutated a prior record snapshot.");
        Require(records is IList<MutationRecord> readOnly && readOnly.IsReadOnly,
            "Drained records expose a mutable collection.");

        child.SetAttribute("id", "three");
        var delivery = subscription.TakeRecordsForDelivery();
        Require(delivery.Count == 1 && delivery[0].OldValue == "two" &&
            subscription.TakeRecords().Count == 0, "Delivery drain did not return one independent record.");
        subscription.Disconnect();
        child.SetAttribute("id", "four");
        Require(subscription.TakeRecords().Count == 0, "Disconnect left an active registration.");

        try
        {
            document.ObserveMutations(root, new MutationObserverOptions());
            throw new InvalidOperationException("An empty mutation option set was accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
