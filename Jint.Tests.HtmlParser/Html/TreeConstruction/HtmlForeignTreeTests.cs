#nullable enable
using System.Reflection;
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // Authored algorithm fixtures, WHATWG HTML source 2f441941fc523877bd9d5cd7de3b91a81a00ca2e.
    // These are not upstream WPT cases. Namespace prefixes below describe the native tree.
    [TestCase("<svg><g><path/></g></svg><p>x", "<svg:svg><svg:g><svg:path></svg:path></svg:g></svg:svg><p>x</p>")]
    [TestCase("<math><mi><x></x><mglyph/><malignmark/>t</mi></math>", "<math:math><math:mi><x></x><math:mglyph></math:mglyph><math:malignmark></math:malignmark>t</math:mi></math:math>")]
    [TestCase("<math><annotation-xml><svg><g/></svg></annotation-xml></math>", "<math:math><math:annotation-xml><svg:svg><svg:g></svg:g></svg:svg></math:annotation-xml></math:math>")]
    [TestCase("<svg><foreignObject><div>x</div></foreignObject><title><p>x</p></title><desc><span>x</span></desc></svg>", "<svg:svg><svg:foreignObject><div>x</div></svg:foreignObject><svg:title><p>x</p></svg:title><svg:desc><span>x</span></svg:desc></svg:svg>")]
    [TestCase("<svg><g><p>x", "<svg:svg><svg:g></svg:g></svg:svg><p>x</p>")]
    [TestCase("<svg><g></p>x", "<svg:svg><svg:g></svg:g></svg:svg><p></p>x")]
    [TestCase("<svg><g></br>x", "<svg:svg><svg:g></svg:g></svg:svg><br></br>x")]
    [TestCase("<svg><g><font>x</font><font color=red>y", "<svg:svg><svg:g><svg:font>x</svg:font></svg:g></svg:svg><font>y</font>")]
    [TestCase("<svg><linearGradient><x></LINEARGRADIENT><path/>", "<svg:svg><svg:linearGradient><svg:x></svg:x></svg:linearGradient><svg:path></svg:path></svg:svg>")]
    [TestCase("<svg><script>a&amp;b<x/></script><script/><g/></svg>", "<svg:svg><svg:script>a&b<svg:x></svg:x></svg:script><svg:script></svg:script><svg:g></svg:g></svg:svg>")]
    [TestCase("<svg><!--c--><?PI d?><!doctype html><![CDATA[a\0b]]></svg>", "<svg:svg><!--c--><?PI d?>a\uFFFDb</svg:svg>")]
    [TestCase("<svg><foreignObject><![CDATA[x]]></foreignObject></svg><![CDATA[y]]>", "<svg:svg><svg:foreignObject>x</svg:foreignObject></svg:svg><!--[CDATA[y]]-->")]
    [TestCase("<svg><foreignObject><p><b></p>x<![CDATA[y]]>", "<svg:svg><svg:foreignObject><p><b></b></p><b>x<!--[CDATA[y]]--></b></svg:foreignObject></svg:svg>")]
    [TestCase("<svg><unknown MIXED=V><XYZ/></unknown></svg>", "<svg:svg><svg:unknown><svg:xyz></svg:xyz></svg:unknown></svg:svg>")]
    [TestCase("<table><svg><g/></svg></table>", "<svg:svg><svg:g></svg:g></svg:svg><table></table>")]
    [TestCase("<svg><g><![CDATA[x", "<svg:svg><svg:g>x</svg:g></svg:svg>")]
    [TestCase("<a><svg><a></a></svg></a>", "<a><svg:svg><svg:a></svg:a></svg:svg></a>")]
    [TestCase("<b><svg><g></b>x", "<b><svg:svg><svg:g></svg:g></svg:svg></b>x")]
    [TestCase("<b><svg><foreignObject></b>x", "<b><svg:svg><svg:foreignObject>x</svg:foreignObject></svg:svg></b>")]
    [TestCase("<form><svg><g></form></g></svg><p>x", "<form><svg:svg><svg:g></svg:g></svg:svg></form><p>x</p>")]
    public void ForeignTreesAtEveryShortSplitAndQuota(string source, string expectedBody)
    {
        var expected = "<html><head></head><body>" + expectedBody + "</body></html>";
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            NamespaceTree(parsed.Document).Should().Be(expected);
            for (var split = 0; split <= source.Length; split++)
            {
                var document = Document.CreateHtml();
                var session = new HtmlParserSession(document);
                session.AppendInput(source[..split]);
                DrainToNeedInput(session, quota);
                session.AppendInput(source[split..], isFinal: true);
                DrainToCompletion(session, quota);
                NamespaceTree(document).Should().Be(expected, $"split {split}, quota {quota}");
            }
        }
    }

    [TestCase("mi")]
    [TestCase("mo")]
    [TestCase("mn")]
    [TestCase("ms")]
    [TestCase("mtext")]
    public void EveryMathTextIntegrationPointHasBothStartExceptions(string name)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<math><{name}><mglyph/><malignmark/><foo></foo>x</{name}></math>", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var point = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!.FirstChild!;
            point.ChildNodes.OfType<Element>().Select(e => e.NamespaceUri).Should().Equal(Namespaces.MathMl, Namespaces.MathMl, Namespaces.Html);
            ((Text) point.LastChild!).Data.Should().Be("x");
        }
    }

    [TestCase("text/html", true)]
    [TestCase("TeXt/HtMl", true)]
    [TestCase("application/xhtml+xml", true)]
    [TestCase("APPLICATION/XHTML+XML", true)]
    [TestCase("application/xml", false)]
    [TestCase(" text/html", false)]
    [TestCase("", false)]
    public void AnnotationXmlIntegrationUsesAcceptedCreationTokenAcrossLiveEdits(string encoding, bool html)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput($"<math><annotation-xml encoding='{encoding}' ENCODING='text/html'>");
            DrainToNeedInput(session, quota);
            var annotation = (Element) document.DocumentElement!.LastChild!.FirstChild!.FirstChild!;
            annotation.SetAttribute("encoding", html ? "application/xml" : "text/html");
            session.AppendInput("<x></x>z</annotation-xml></math>", isFinal: true);
            DrainToCompletion(session, quota);
            ((Element) annotation.FirstChild!).NamespaceUri.Should().Be(html ? Namespaces.Html : Namespaces.MathMl);
        }
    }

    [Test]
    public void ForeignAttributesRetainOrderNamespacesAndIsValueWithoutXmlnsRebinding()
    {
        const string source = "<svg xmlns=wrong xmlns:xlink=wrong is='A&amp;B' viewBox='0 0' xlink:href=x xml:lang=en odd:attr=z><g/></svg><math definitionurl=u />";
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var svg = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            svg.NamespaceUri.Should().Be(Namespaces.Svg);
            ((Element) svg.FirstChild!).NamespaceUri.Should().Be(Namespaces.Svg);
            svg.IsValue.Should().Be("A&B");
            svg.Attributes.Select(a => a.Name).Should().Equal("xmlns", "xmlns:xlink", "is", "viewBox", "xlink:href", "xml:lang", "odd:attr");
            svg.GetAttributeNodeNS(Namespaces.Xmlns, "xmlns")!.Prefix.Should().BeNull();
            svg.GetAttributeNodeNS(Namespaces.Xmlns, "xlink")!.Prefix.Should().Be("xmlns");
            svg.GetAttributeNS("http://www.w3.org/1999/xlink", "href").Should().Be("x");
            svg.GetAttributeNS(Namespaces.Xml, "lang").Should().Be("en");
            svg.GetAttributeNode("odd:attr")!.NamespaceUri.Should().BeNull();
            var math = (Element) svg.NextSibling!;
            math.GetAttribute("definitionURL").Should().Be("u");
            parsed.Diagnostics.Items.Count(d => d.Code == "html/tree-unexpected-namespace-declaration").Should().Be(2);
        }
    }

    [Test]
    public void ForeignAllocationUsesInertTemplateAndFosterDestinationOwners()
    {
        var parsed = Parse("<template><table><svg is=s><g is=g /></svg></table><math is=m /></template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var template = (Element) parsed.Document.DocumentElement!.FirstChild!.FirstChild!;
        var contents = template.TemplateContent!;
        var svg = (Element) contents.FirstChild!;
        svg.NamespaceUri.Should().Be(Namespaces.Svg);
        svg.IsValue.Should().Be("s");
        svg.OwnerDocument.Should().BeSameAs(contents.OwnerDocument);
        svg.OwnerDocument.Should().NotBeSameAs(parsed.Document);
        ((Element) svg.FirstChild!).IsValue.Should().Be("g");
        svg.FirstChild!.OwnerDocument.Should().BeSameAs(contents.OwnerDocument);
        ((Element) contents.LastChild!).IsValue.Should().Be("m");
    }

    [Test]
    public void ForeignNamesDoNotPolluteHtmlNameQueriesOrImpliedEndTags()
    {
        var parsed = Parse("<p><svg><option><template><select><foreignObject></p>x", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var builder = BuilderOf(parsed.Session);
        var last = typeof(HtmlTreeBuilder).GetMethod("Last", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var name in new[] { "option", "template", "select" })
            ((int) last.Invoke(builder, [name])!).Should().Be(-1);
    }

    [Test]
    public void NamespaceQualifiedIndexesAndForeignScopesMatchLiteralStackRules()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var namespaces = new[] { Namespaces.Html, Namespaces.Svg, Namespaces.MathMl };
        var names = new[] { "div", "p", "table", "template", "select", "option", "button", "ol", "mi", "annotation-xml", "foreignObject", "title" };
        var random = new Random(731);
        for (var sample = 0; sample < 64; sample++)
        {
            var parsed = Parse("");
            var builder = BuilderOf(parsed.Session);
            var push = typeof(HtmlTreeBuilder).GetMethod("Push", flags)!;
            for (var i = 0; i < 8; i++)
                push.Invoke(builder, [parsed.Document.CreateElementNS(namespaces[random.Next(namespaces.Length)], names[random.Next(names.Length)])]);
            var stack = (List<Element>) typeof(HtmlTreeBuilder).GetField("_open", flags)!.GetValue(builder)!;
            foreach (var target in names)
            {
                var expectedLast = stack.FindLastIndex(e => e.NamespaceUri == Namespaces.Html && e.LocalName == target);
                ((int) typeof(HtmlTreeBuilder).GetMethod("Last", flags)!.Invoke(builder, [target])!).Should().Be(expectedLast);
                foreach (var scope in new[] { "InScope", "InButtonScope", "InListItemScope", "InTableScope" })
                {
                    var found = false;
                    for (var i = stack.Count - 1; i >= 0; i--)
                    {
                        var e = stack[i];
                        if (e.NamespaceUri == Namespaces.Html && e.LocalName == target) { found = true; break; }
                        var boundary = scope == "InTableScope"
                            ? e.NamespaceUri == Namespaces.Html && e.LocalName is "html" or "table" or "template"
                            : e.NamespaceUri == Namespaces.Html && e.LocalName is "applet" or "caption" or "html" or "table" or "td" or "th" or "marquee" or "object" or "select" or "template" ||
                              e.NamespaceUri == Namespaces.MathMl && e.LocalName is "mi" or "mo" or "mn" or "ms" or "mtext" or "annotation-xml" ||
                              e.NamespaceUri == Namespaces.Svg && e.LocalName is "foreignObject" or "desc" or "title";
                        if (scope == "InButtonScope" && e.NamespaceUri == Namespaces.Html && e.LocalName == "button") boundary = true;
                        if (scope == "InListItemScope" && e.NamespaceUri == Namespaces.Html && e.LocalName is "ol" or "ul") boundary = true;
                        if (boundary) break;
                    }
                    ((bool) typeof(HtmlTreeBuilder).GetMethod(scope, flags)!.Invoke(builder, [target])!).Should().Be(found);
                }
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LongForeignEndComparisonsYieldAndCancelAtSavedCharacterCursors(bool lateMismatch)
    {
        var name = new string('x', 100_000);
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<svg><" + name + ">", isFinal: false);
        DrainToNeedInput(session, 100_000);
        session.AppendInput("</" + (lateMismatch ? name[..^1] + "y" : name) + ">", isFinal: true);
        var builder = BuilderOf(session);
        var cursor = typeof(HtmlTreeBuilder).GetField("_foreignNameCursor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 500_000 && (int) cursor.GetValue(builder)! < 17; turn++)
            session.Drive(1, CancellationToken.None).Kind.Should().Be(HtmlParseStepKind.Yielded);
        ((int) cursor.GetValue(builder)!).Should().Be(17);
        var work = session.WorkCount;
        session.Drive(1, CancellationToken.None).Kind.Should().Be(HtmlParseStepKind.Yielded);
        ((int) cursor.GetValue(builder)!).Should().Be(18);
        (session.WorkCount - work).Should().Be(2);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancellation.Token));
        ((Element) document.DocumentElement!.LastChild!.FirstChild!).ChildCount.Should().Be(1);
    }

    [TestCase("color")]
    [TestCase("face")]
    [TestCase("size")]
    public void EachFontBreakoutAttributeUsesHtmlAndAcknowledgesOnlyForeignSelfClosing(string attribute)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<svg><g><font {attribute}='v'/>x", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var body = parsed.Document.DocumentElement!.LastChild!;
            var font = (Element) body.LastChild!;
            font.NamespaceUri.Should().Be(Namespaces.Html);
            ((Text) font.FirstChild!).Data.Should().Be("x");
            parsed.Diagnostics.Items.Count(d => d.Code == "html/tree-html-start-tag-in-foreign-content").Should().Be(1);
            parsed.Diagnostics.Items.Count(d => d.Code == "html/tree-unacknowledged-self-closing-flag").Should().Be(1);
            var foreign = Parse("<svg><script/><font/></svg>", quota);
            foreign.Diagnostics.Items.Should().NotContain(d => d.Code == "html/tree-unacknowledged-self-closing-flag");
        }
    }

    [Test]
    public void ForeignDiagnosticOrderingAndAnchorsStayStableAcrossQuotas()
    {
        const string source = "<!doctype html><svg><g></wrong><!doctype html><![CDATA[\0]]></g></svg>";
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            parsed.Diagnostics.Items.Where(d => d.Code.StartsWith("html/tree-", StringComparison.Ordinal))
                .Select(d => (d.Code, d.Offset)).Should().Equal(
                    ("html/tree-misnested-foreign-end-tag", (long) source.IndexOf("</wrong>", StringComparison.Ordinal)),
                    ("html/tree-unexpected-end-tag", (long) source.IndexOf("</wrong>", StringComparison.Ordinal)),
                    ("html/tree-unexpected-doctype", (long) source.LastIndexOf("<!doctype", StringComparison.Ordinal)),
                    ("html/tree-unexpected-null-character", (long) source.IndexOf('\0')));
        }
    }

    [Test]
    public void ForeignNameScanningAndTextHaveDeterministicLinearWork()
    {
        static string Source(int count)
        {
            var name = new string('x', 128) + "a";
            return "<svg>" + string.Concat(Enumerable.Repeat("<" + name + ">", count)) + "</" + new string('x', 128) + "b>" + new string('z', count);
        }
        var smaller = Parse(Source(64), 1);
        var larger = Parse(Source(128), 1);
        var repeated = Parse(Source(128), 1);
        larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
        larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LongForeignEndNameFinishesItsExactOrLateMismatchScan(bool lateMismatch)
    {
        var name = new string('x', 100_000);
        var end = lateMismatch ? name[..^1] + "y" : name;
        var parsed = Parse("<svg><" + name + "></" + end + "><g/></svg>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var svg = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        var longElement = (Element) svg.FirstChild!;
        if (lateMismatch)
        {
            longElement.ChildCount.Should().Be(1);
            ((Element) longElement.FirstChild!).LocalName.Should().Be("g");
        }
        else
        {
            longElement.ChildCount.Should().Be(0);
            ((Element) longElement.NextSibling!).LocalName.Should().Be("g");
        }
        parsed.Diagnostics.Items.Count(d => d.Code == "html/tree-misnested-foreign-end-tag")
            .Should().Be(lateMismatch ? 2 : 0);
    }

    private static string NamespaceTree(Node root)
    {
        var result = new StringBuilder();
        void Visit(Node node)
        {
            if (node is Element element)
            {
                var prefix = element.NamespaceUri switch { Namespaces.Svg => "svg:", Namespaces.MathMl => "math:", _ => "" };
                result.Append('<').Append(prefix).Append(element.LocalName).Append('>');
                foreach (var child in element.ChildNodes) Visit(child);
                result.Append("</").Append(prefix).Append(element.LocalName).Append('>');
            }
            else if (node is Text text) result.Append(text.Data);
            else if (node is Comment comment) result.Append("<!--").Append(comment.Data).Append("-->");
            else if (node is ProcessingInstruction pi) result.Append("<?").Append(pi.Target).Append(' ').Append(pi.Data).Append("?>");
            else foreach (var child in node.ChildNodes) Visit(child);
        }
        Visit(root);
        return result.ToString();
    }
}
