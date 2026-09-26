using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class HtmlFragmentTreeTests
{
    // HTML Standard §13.4 and §13.2.4.1, reviewed 2026-09-25.
    [TestCase("body", "<p>a<div>b", "<p>a</p><div>b</div>")]
    [TestCase("head", "<meta><p>x", "<meta></meta><p>x</p>")]
    [TestCase("html", "<p>x", "<head></head><body><p>x</p></body>")]
    [TestCase("table", "x<tr><td>y", "x<tbody><tr><td>y</td></tr></tbody>")]
    [TestCase("tbody", "<td>x", "<tr><td>x</td></tr>")]
    [TestCase("tr", "<td>x<td>y", "<td>x</td><td>y</td>")]
    [TestCase("td", "<b>x", "<b>x</b>")]
    [TestCase("caption", "<p>x", "<p>x</p>")]
    [TestCase("colgroup", "<col>x", "<col></col>")]
    [TestCase("select", "<option>x<option>y", "<option>x</option><option>y</option>")]
    [TestCase("option", "<option>x", "<option>x</option>")]
    [TestCase("optgroup", "<option>x", "<option>x</option>")]
    [TestCase("template", "<tr><td>x", "<tr><td>x</td></tr>")]
    [TestCase("body", "", "")]
    [TestCase("body", "</body></html><!--x-->", "<!--x-->")]
    public void ContextMatrixAndEverySplit(string contextName, string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var context = document.CreateElement(contextName);
            context.SetAttribute("id", "unchanged");
            var original = context.AppendChild(document.CreateTextNode("original"));
            var session = HtmlParserSession.CreateFragment(context);
            session.AppendInput(source[..split]);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], isFinal: true);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.Complete);
            Tree(session.Fragment!).Should().Be(expected);
            context.FirstChild.Should().BeSameAs(original);
            context.LastChild.Should().BeSameAs(original);
            context.GetAttribute("id").Should().Be("unchanged");
            session.Fragment!.OwnerDocument.Should().BeSameAs(document);
            session.Fragment.ParentNode.Should().BeNull();
            session.Document.Should().NotBeSameAs(document);
            session.Document.DocumentElement!.FirstChild.Should().BeNull();
            foreach (var element in NodeTraversal.DescendantElements(session.Fragment, default))
                element.OwnerDocument.Should().BeSameAs(document);
        }
    }

    [TestCase("title", "</title><b>&amp;", "</title><b>&")]
    [TestCase("textarea", "\n</textarea><b>&amp;", "\n</textarea><b>&")]
    [TestCase("style", "</style><b>&amp;", "</style><b>&amp;")]
    [TestCase("script", "</script><b>&amp;", "</script><b>&amp;")]
    [TestCase("plaintext", "</plaintext><b>&amp;", "</plaintext><b>&amp;")]
    [TestCase("noscript", "</noscript><b>&amp;", "</noscript><b>&amp;")]
    public void InitialTextContextsHaveNoAppropriateEndTag(string name, string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        for (var split = 0; split <= source.Length; split++)
        {
            var context = Document.CreateHtml().CreateElement(name);
            var session = HtmlParserSession.CreateFragment(context, new HtmlParseOptions { ScriptingEnabled = true });
            session.AppendInput(source[..split]);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], isFinal: true);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.Complete);
            var result = session.Fragment!;
            result.FirstChild.Should().BeOfType<Text>();
            ((Text) result.FirstChild!).Data.Should().Be(expected);
            result.FirstChild.NextSibling.Should().BeNull();
        }
    }

    [Test]
    public void DisabledNoscriptUsesDataGrammar()
    {
        Tree(HtmlParserSession.ParseFragment("<b>x</b>", Document.CreateHtml().CreateElement("noscript")))
            .Should().Be("<b>x</b>");
    }

    [TestCase("NoQuirks", "<p>x</p><table></table>")]
    [TestCase("LimitedQuirks", "<p>x</p><table></table>")]
    [TestCase("Quirks", "<p>x<table></table></p>")]
    public void InheritsModeWithoutChangingExternalDocument(string mode, string expected)
    {
        var document = Document.CreateHtml();
        document.SetParserMode(Enum.Parse<DocumentMode>(mode));
        var context = document.CreateElement("body");
        Tree(HtmlParserSession.ParseFragment("<!doctype html><p>x<table>", context)).Should().Be(expected);
        document.Mode.Should().Be(Enum.Parse<DocumentMode>(mode));
        document.FirstChild.Should().BeNull();
    }

    [Test]
    public void RealTemplateTargetOwnsResultFromCreation()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var result = HtmlParserSession.ParseFragment("<p>x<script>y</script><template><b>z", template,
            new HtmlParseOptions { ScriptingEnabled = true }, target: template.TemplateContent);
        result.OwnerDocument.Should().BeSameAs(template.TemplateContent!.OwnerDocument);
        result.FirstChild!.OwnerDocument.Should().BeSameAs(result.OwnerDocument);
        template.TemplateContent.FirstChild.Should().BeNull();
        var script = (Element) result.FirstChild.FirstChild!.NextSibling!;
        script.GetHtmlState()!.Script!.AlreadyStarted.Should().BeTrue();
        script.GetHtmlState()!.Script!.ParserDocument.Should().NotBeNull();
    }

    [TestCase("Inert", true, true)]
    [TestCase("Fragment", false, false)]
    [TestCase("Disabled", false, true)]
    public void ScriptModeMetadata(string mode, bool started, bool parserDocument)
    {
        var document = Document.CreateHtml();
        var result = HtmlParserSession.ParseFragment("<script>x</script>", document.CreateElement("body"),
            scriptingMode: Enum.Parse<HtmlParserScriptingMode>(mode));
        var state = ((Element) result.FirstChild!).GetHtmlState()!.Script!;
        state.AlreadyStarted.Should().Be(started);
        (state.ParserDocument is not null).Should().Be(parserDocument);
        state.ForceAsync.Should().BeFalse();
        state.PreparationTimeDocument.Should().BeNull();
        state.ParserSourceLocation.Should().NotBeNull();
    }

    [Test]
    public void FormAncestorSuppressesNestedFormButDoesNotAssociateDetachedControls()
    {
        var document = Document.CreateHtml();
        var form = document.CreateElement("form");
        var context = document.CreateElement("div");
        form.AppendChild(context);
        var result = HtmlParserSession.ParseFragment("<form><input></form><input>", context);
        Tree(result).Should().Be("<input></input><input></input>");
        HtmlFormState.GetOwner((Element) result.FirstChild!).Should().BeNull();
        HtmlFormState.GetOwner((Element) result.LastChild!).Should().BeNull();
    }

    [Test]
    public void DocumentParserAssociatesNonAncestorFormBeforeInsertion()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table><form id=f><input><input form=other></table>", isFinal: true);
        DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) document.DocumentElement!.LastChild!;
        var first = (Element) body.FirstChild!;
        var table = (Element) body.LastChild!;
        var form = (Element) table.FirstChild!;
        HtmlFormState.GetOwner(first).Should().BeSameAs(form);
        first.FormAssociationState!.ParserInserted.Should().BeTrue();
        HtmlFormState.GetOwner((Element) first.NextSibling!).Should().BeNull();
    }

    [Test]
    public void XmlSvgContextDoesNotChangeOwnerKindAndIntegrationUsesContextAttributes()
    {
        var document = Document.CreateXml();
        var svg = document.CreateElementNS(Namespaces.Svg, "svg");
        var result = HtmlParserSession.ParseFragment("<![CDATA[x]]><lineargradient xlink:href='#x'/><p>y", svg);
        document.Kind.Should().Be(DocumentKind.Xml);
        ((Text) result.FirstChild!).Data.Should().Be("x");
        var gradient = (Element) result.FirstChild.NextSibling!;
        gradient.LocalName.Should().Be("linearGradient");
        gradient.NamespaceUri.Should().Be(Namespaces.Svg);
        gradient.GetAttributeNodeNS("http://www.w3.org/1999/xlink", "href")!.OwnerDocument.Should().BeSameAs(document);
        ((Element) gradient.NextSibling!).NamespaceUri.Should().Be(Namespaces.Html);
        var math = document.CreateElementNS(Namespaces.MathMl, "annotation-xml");
        math.SetAttribute("encoding", "TeXt/HtMl");
        var mathResult = HtmlParserSession.ParseFragment("<custom>x", math);
        ((Element) mathResult.FirstChild!).NamespaceUri.Should().Be(Namespaces.Html);
        math.GetAttribute("encoding").Should().Be("TeXt/HtMl");
    }

    [Test]
    public void DepthCountsSyntheticRootAndVoidPushButNotExternalAncestors()
    {
        var document = Document.CreateHtml();
        var context = document.CreateElement("body");
        var parent = context;
        for (var i = 0; i < 100; i++)
        {
            var ancestor = document.CreateElement("div");
            ancestor.AppendChild(parent);
            parent = ancestor;
        }
        var options = new HtmlParseOptions { Limits = new ParseLimits { MaxNestingDepth = 2 } };
        Tree(HtmlParserSession.ParseFragment("<br>", context, options)).Should().Be("<br></br>");
        Assert.Throws<ParseLimitException>(() => HtmlParserSession.ParseFragment("<div><br>", context, options))!
            .Kind.Should().Be(ParseLimitKind.NestingDepth);
    }

    [Test]
    public void BootstrapIsCooperativeAndCancellationTerminatesSession()
    {
        var document = Document.CreateHtml();
        var context = document.CreateElement("body");
        for (var i = 0; i < 500; i++) context.SetAttribute("a" + i, "v");
        var session = HtmlParserSession.CreateFragment(context);
        session.AppendInput("<p>x", isFinal: true);
        session.Drive(1, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        session.Fragment!.FirstChild.Should().BeNull();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, default));
        Assert.Throws<InvalidOperationException>(() => session.AppendInput(""));
        context.AttributeCount.Should().Be(500);
    }

    [TestCase(Namespaces.Svg, "svg", "<![CDATA[x]]><circle/><foreignObject><b>y</b></foreignObject><g/>")]
    [TestCase(Namespaces.MathMl, "mi", "<mglyph/><b>x</b><malignmark/>")]
    [TestCase(Namespaces.MathMl, "annotation-xml", "<svg><circle/></svg><custom/>")]
    [TestCase(Namespaces.Svg, "foreignObject", "<custom>x</custom><svg><circle/>")]
    public void ForeignContextEverySplitAndQuota(string namespaceUri, string name, string source)
    {
        var document = Document.CreateXml();
        var context = document.CreateElementNS(namespaceUri, name);
        var expected = new StringBuilder();
        foreach (var node in HtmlParserSession.ParseFragment(source, context).ChildNodes) NativeTree(node, expected);
        foreach (var quota in new[] { 1, 3, 100_000 })
        for (var split = 0; split <= source.Length; split++)
        {
            var session = HtmlParserSession.CreateFragment(context);
            session.AppendInput(source[..split]);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], isFinal: true);
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.Complete);
            var actual = new StringBuilder();
            foreach (var node in session.Fragment!.ChildNodes) NativeTree(node, actual);
            actual.ToString().Should().Be(expected.ToString());
        }
    }

    [Test]
    public void BootstrapDoesNotRescanAncestorsAcrossQuotaOneYields()
    {
        long Work(int depth)
        {
            var document = Document.CreateHtml();
            var context = document.CreateElement("body");
            var parent = context;
            for (var i = 0; i < depth; i++)
            {
                var ancestor = document.CreateElement("div");
                ancestor.AppendChild(parent);
                parent = ancestor;
            }
            var session = HtmlParserSession.CreateFragment(context);
            session.AppendInput("", isFinal: true);
            DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.Complete);
            return session.WorkCount;
        }
        (Work(200) - Work(100)).Should().Be(100);
    }

    [Test]
    public void InputLimitsAndInvalidInputsDoNotMutateContext()
    {
        var context = Document.CreateHtml().CreateElement("body");
        var options = new HtmlParseOptions { Limits = new ParseLimits { MaxInputCharacters = 3 } };
        Tree(HtmlParserSession.ParseFragment("abc", context, options)).Should().Be("abc");
        var session = HtmlParserSession.CreateFragment(context, options);
        Assert.Throws<ParseLimitException>(() => session.AppendInput("abcd", isFinal: true));
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, default));
        Assert.Throws<ArgumentNullException>(() => HtmlParserSession.ParseFragment(null!, context));
        Assert.Throws<ArgumentNullException>(() => HtmlParserSession.ParseFragment("", null!));
        context.FirstChild.Should().BeNull();
    }

    [Test]
    public void ShadowTargetReturnsDetachedTargetOwnedFragment()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var original = shadow.AppendChild(document.CreateTextNode("original"));
        var result = HtmlParserSession.ParseFragment("<b>x", host, target: shadow);
        Tree(result).Should().Be("<b>x</b>");
        result.OwnerDocument.Should().BeSameAs(shadow.OwnerDocument);
        result.ParentNode.Should().BeNull();
        result.Host.Should().BeNull();
        shadow.FirstChild.Should().BeSameAs(original);
        host.FirstChild.Should().BeNull();
    }

    [TestCase("<b is='fancy-b'><i>x</b>y</i>", "<b><i>x</i></b><i>y</i>")]
    [TestCase("<b is='fancy-b'><p>x</b>y", "<b></b><p><b>x</b>y</p>")]
    public void FragmentFormattingPreservesDestinationOwnerAndIsValue(string source, string expected)
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var result = HtmlParserSession.ParseFragment(source, template, target: template.TemplateContent);
        Tree(result).Should().Be(expected);
        foreach (var element in NodeTraversal.DescendantElements(result, default))
        {
            element.OwnerDocument.Should().BeSameAs(template.TemplateContent!.OwnerDocument);
            if (element.LocalName == "b") element.IsValue.Should().Be("fancy-b");
            foreach (var attribute in element.Attributes)
            {
                attribute.OwnerDocument.Should().BeSameAs(element.OwnerDocument);
                attribute.OwnerElement.Should().BeSameAs(element);
            }
        }
    }

    [Test]
    public void DeepFormAssociationYieldsAndWorkScalesWithDepthPlusControls()
    {
        long ParseWork(int depth, int controls, int quota)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<form>" + string.Concat(Enumerable.Repeat("<div>", depth)));
            DriveToBoundary(session, quota).Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(string.Concat(Enumerable.Repeat("<input>", controls)), isFinal: true);
            var maxDriveWork = 0L;
            for (var turn = 0; turn < 100_000; turn++)
            {
                var before = session.WorkCount;
                var step = session.Drive(quota, default);
                maxDriveWork = Math.Max(maxDriveWork, session.WorkCount - before);
                if (step.Kind == HtmlParseStepKind.Complete) break;
                step.Kind.Should().Be(HtmlParseStepKind.Yielded);
                if (turn == 99_999) throw new InvalidOperationException("Deep form parse stalled.");
            }
            if (quota == 1) maxDriveWork.Should().BeLessThan(32);
            var form = (Element) document.DocumentElement!.LastChild!.FirstChild!;
            var inputs = NodeTraversal.DescendantElements(form, default).Where(e => e.LocalName == "input").ToArray();
            inputs.Length.Should().Be(controls);
            foreach (var input in inputs) HtmlFormState.GetOwner(input).Should().BeSameAs(form);
            return session.WorkCount;
        }
        var small = ParseWork(100, 100, 1);
        var large = ParseWork(200, 200, 1);
        large.Should().BeLessThan(small * 3);
        ParseWork(200, 200, 100_000).Should().Be(large);
    }

    [Test]
    public void CancellationDuringSavedFormRootWalkLeavesControlUnpublished()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<form>" + string.Concat(Enumerable.Repeat("<div>", 500)));
        DriveToBoundary(session, 100_000).Should().Be(HtmlParseStepKind.NeedInput);
        session.AppendInput("<input>", isFinal: true);
        for (var i = 0; i < 40; i++) session.Drive(1, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        NodeTraversal.DescendantElements(document, default).Any(e => e.LocalName == "input").Should().BeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, default));
        NodeTraversal.DescendantElements(document, default).Any(e => e.LocalName == "input").Should().BeFalse();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HostMoveInvalidatesCachedFormRoots(bool adopt)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<form><div><input>");
        DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.NeedInput);
        var form = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var div = (Element) form.FirstChild!;
        var otherDocument = adopt ? Document.CreateHtml() : document;
        var otherRoot = otherDocument.CreateElement("div");
        otherRoot.AppendChild(div);
        session.AppendInput("<input>", isFinal: true);
        DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.Complete);
        var input = (Element) div.LastChild!;
        input.OwnerDocument.Should().BeSameAs(otherDocument);
        HtmlFormState.GetOwner(input).Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdoptionDuringSavedFormRootWalkRehomesFreshControl(bool moveForm)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<form>" + string.Concat(Enumerable.Repeat("<div>", 500)));
        DriveToBoundary(session, 100_000).Should().Be(HtmlParseStepKind.NeedInput);
        session.AppendInput("<input id=x>", isFinal: true);
        for (var i = 0; i < 40; i++) session.Drive(1, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        var form = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var parent = form;
        for (var i = 0; i < 500; i++) parent = (Element) parent.FirstChild!;
        parent.FirstChild.Should().BeNull();
        var destination = Document.CreateHtml();
        var destinationRoot = destination.CreateElement("section");
        destinationRoot.AppendChild(moveForm ? form : parent);
        DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.Complete);
        var input = (Element) parent.FirstChild!;
        input.OwnerDocument.Should().BeSameAs(destination);
        input.GetAttributeNode("id")!.OwnerDocument.Should().BeSameAs(destination);
        input.GetAttributeNode("id")!.OwnerElement.Should().BeSameAs(input);
        HtmlFormState.GetOwner(input).Should().BeSameAs(moveForm ? form : null);
    }

    [Test]
    public void SuspendedFosterInsertionRefreshesMovedReferenceChild()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<form>" + string.Concat(Enumerable.Repeat("<div>", 500)) + "<table>");
        DriveToBoundary(session, 100_000).Should().Be(HtmlParseStepKind.NeedInput);
        session.AppendInput("<input id=x>", isFinal: true);
        for (var i = 0; i < 40; i++) session.Drive(1, default).Kind.Should().Be(HtmlParseStepKind.Yielded);
        var table = NodeTraversal.DescendantElements(document, default).Single(e => e.LocalName == "table");
        var destination = Document.CreateHtml();
        var destinationRoot = destination.CreateElement("section");
        destinationRoot.AppendChild(table);
        DriveToBoundary(session, 1).Should().Be(HtmlParseStepKind.Complete);
        var input = (Element) destinationRoot.FirstChild!;
        input.LocalName.Should().Be("input");
        input.OwnerDocument.Should().BeSameAs(destination);
        input.NextSibling.Should().BeSameAs(table);
        HtmlFormState.GetOwner(input).Should().BeNull();
    }

    // Intersection cases exclude current normative select/PI/patch rules that
    // AngleSharp 1.8.2 does not implement. Compare structure, not HTML strings.
    [TestCase("body", "<b><i>x</b>y</i><!--z-->")]
    [TestCase("table", "a<tr><td>x<td>y</table>z")]
    [TestCase("tbody", "<tr><td>x<tr><th>y")]
    [TestCase("tr", "<td><b>x<td>y")]
    [TestCase("template", "<table><tr><td>x</table><template><p>y")]
    [TestCase("body", "<svg><foreignObject><p>x</p></foreignObject><circle/></svg>")]
    [TestCase("body", "<math><mi><b>x</b></mi><annotation-xml encoding='text/html'><p>y")]
    public void ConformingAngleSharpIntersection(string name, string source)
    {
        var angleParser = new AngleSharp.Html.Parser.HtmlParser();
        var angleDocument = angleParser.ParseDocument("<!doctype html><html><head></head><body></body></html>");
        var angleContext = angleDocument.CreateElement(name);
        var expected = new StringBuilder();
        foreach (var node in angleParser.ParseFragment(source, angleContext)) AngleTree(node, expected);
        var nativeDocument = Document.CreateHtml();
        var actual = new StringBuilder();
        var fragment = HtmlParserSession.ParseFragment(source, nativeDocument.CreateElement(name));
        foreach (var node in fragment.ChildNodes) NativeTree(node, actual);
        actual.ToString().Should().Be(expected.ToString());
    }

    private static void NativeTree(Node node, StringBuilder output)
    {
        if (node is Element element)
        {
            output.Append('[').Append(element.NamespaceUri).Append('|').Append(element.LocalName);
            foreach (var attribute in element.Attributes)
                output.Append(' ').Append(attribute.NamespaceUri).Append('|').Append(attribute.Name).Append('=').Append(attribute.Value);
            output.Append(']');
            foreach (var child in (element.TemplateContent ?? (Node) element).ChildNodes) NativeTree(child, output);
            output.Append("[/]");
        }
        else if (node is Text text) output.Append("{text:").Append(text.Data).Append('}');
        else if (node is Comment comment) output.Append("{comment:").Append(comment.Data).Append('}');
    }

    private static void AngleTree(AngleSharp.Dom.INode node, StringBuilder output)
    {
        if (node is AngleSharp.Dom.IElement element)
        {
            output.Append('[').Append(element.NamespaceUri).Append('|').Append(element.LocalName);
            foreach (var attribute in element.Attributes)
                output.Append(' ').Append(attribute.NamespaceUri).Append('|').Append(attribute.Name).Append('=').Append(attribute.Value);
            output.Append(']');
            var parent = element is AngleSharp.Html.Dom.IHtmlTemplateElement template ? (AngleSharp.Dom.INode) template.Content : element;
            foreach (var child in parent.ChildNodes) AngleTree(child, output);
            output.Append("[/]");
        }
        else if (node is AngleSharp.Dom.IText text) output.Append("{text:").Append(text.Data).Append('}');
        else if (node is AngleSharp.Dom.IComment comment) output.Append("{comment:").Append(comment.Data).Append('}');
    }

    private static HtmlParseStepKind DriveToBoundary(HtmlParserSession session, int quota)
    {
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(quota, default);
            if (step.Kind != HtmlParseStepKind.Yielded) return step.Kind;
        }
        throw new InvalidOperationException("Fragment parse stalled.");
    }

    private static string Tree(Node root)
    {
        var result = new StringBuilder();
        void Visit(Node node)
        {
            if (node is Element element)
            {
                result.Append('<').Append(element.LocalName).Append('>');
                for (var child = (element.TemplateContent ?? (Node) element).FirstChild; child is not null; child = child.NextSibling) Visit(child);
                result.Append("</").Append(element.LocalName).Append('>');
            }
            else if (node is Text text) result.Append(text.Data);
            else if (node is Comment comment) result.Append("<!--").Append(comment.Data).Append("-->");
            else for (var child = node.FirstChild; child is not null; child = child.NextSibling) Visit(child);
        }
        Visit(root);
        return result.ToString();
    }
}
