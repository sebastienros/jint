#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.4.4 and §13.2.6.4.16, 2026-09-22.
    [TestCase("<template><p>x</p></template>",
        "<html><head><template>{<p>x</p>}</template></head><body></body></html>")]
    [TestCase("<body><template><p>x</p></template>after",
        "<html><head></head><body><template>{<p>x</p>}</template>after</body></html>")]
    [TestCase("<head></head><template><p>x</template><body>y",
        "<html><head><template>{<p>x</p>}</template></head><body>y</body></html>")]
    [TestCase("<table><template><tr><td>x</template></table>",
        "<html><head></head><body><table><template>{<tr><td>x</td></tr>}</template></table></body></html>")]
    [TestCase("<template><col><tr><td>x</template>",
        "<html><head><template>{<col></col>}</template></head><body></body></html>")]
    [TestCase("<template><tbody><tr><td>x</template>",
        "<html><head><template>{<tbody><tr><td>x</td></tr></tbody>}</template></head><body></body></html>")]
    [TestCase("<template><tr><td>x</template>",
        "<html><head><template>{<tr><td>x</td></tr>}</template></head><body></body></html>")]
    [TestCase("<template><td>x</template>",
        "<html><head><template>{<td>x</td>}</template></head><body></body></html>")]
    [TestCase("<template><template><p>x</p></template><p>y</p></template>",
        "<html><head><template>{<template>{<p>x</p>}</template><p>y</p>}</template></head><body></body></html>")]
    public void OrdinaryTemplateTrees(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            SerializeWithTemplateContents(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void TemplateContentsReuseNativeFragmentHostAndInertOwner()
    {
        var parsed = Parse("<template id=outer><span a=1>x</span><template id=inner><b>y</b></template></template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var outer = (Element) parsed.Document.DocumentElement!.FirstChild!.FirstChild!;
        var outerContent = outer.TemplateContent!;
        outer.ChildCount.Should().Be(0);
        outerContent.Host.Should().BeSameAs(outer);
        outerContent.ParentNode.Should().BeNull();
        var inertOwner = parsed.Document.GetTemplateContentsOwnerDocument();
        outerContent.OwnerDocument.Should().BeSameAs(inertOwner);
        outer.OwnerDocument.Should().BeSameAs(parsed.Document);
        var span = (Element) outerContent.FirstChild!;
        span.OwnerDocument.Should().BeSameAs(inertOwner);
        span.GetAttributeNode("a")!.OwnerDocument.Should().BeSameAs(inertOwner);
        span.FirstChild!.OwnerDocument.Should().BeSameAs(inertOwner);
        var inner = (Element) outerContent.LastChild!;
        inner.OwnerDocument.Should().BeSameAs(inertOwner);
        inner.ChildCount.Should().Be(0);
        inner.TemplateContent!.Host.Should().BeSameAs(inner);
        inner.TemplateContent.OwnerDocument.Should().BeSameAs(inertOwner);
        inner.TemplateContent.FirstChild!.OwnerDocument.Should().BeSameAs(inertOwner);
    }

    [Test]
    public void TemplateIgnoresHtmlBodyMergesAndKeepsFormPointerOutside()
    {
        var parsed = Parse("<html a=1><body b=2><form id=outer><template><html c=3><body d=4><form id=inner>x</form></template><input></form>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var html = parsed.Document.DocumentElement!;
        var body = (Element) html.LastChild!;
        html.GetAttribute("c").Should().BeNull();
        body.GetAttribute("d").Should().BeNull();
        var outer = (Element) body.FirstChild!;
        var template = (Element) outer.FirstChild!;
        var inner = (Element) template.TemplateContent!.FirstChild!;
        inner.LocalName.Should().Be("form");
        inner.GetAttribute("id").Should().Be("inner");
        outer.LastChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("input");
    }

    [Test]
    public void TableFormInsideTemplateIsInsertedWithoutChangingOuterFormPointer()
    {
        var parsed = Parse("<body><form id=outer><template><table><form id=inner><tr><td>x</table></template><input></form>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        var outer = (Element) body.FirstChild!;
        var template = (Element) outer.FirstChild!;
        var table = (Element) template.TemplateContent!.FirstChild!;
        var inner = (Element) table.FirstChild!;
        inner.LocalName.Should().Be("form");
        inner.GetAttribute("id").Should().Be("inner");
        outer.LastChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("input");
    }

    [Test]
    public void TemplateCommentPiAndScriptStayInInertContents()
    {
        var parsed = Parse("<template><!--c--><?Inside data?><script>window.x=1</script><p>ok</p></template>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var template = (Element) parsed.Document.DocumentElement!.FirstChild!.FirstChild!;
        var content = template.TemplateContent!;
        content.FirstChild.Should().BeOfType<Comment>();
        var pi = (ProcessingInstruction) content.FirstChild!.NextSibling!;
        pi.Target.Should().Be("Inside");
        pi.Data.Should().Be("data");
        var script = (Element) pi.NextSibling!;
        script.LocalName.Should().Be("script");
        ((Text) script.FirstChild!).Data.Should().Be("window.x=1");
        parsed.Document.DocumentElement.GetAttribute("x").Should().BeNull();
    }

    [Test]
    public void FosterPlacementUsesLastTemplateAndKeepsCommentsAndPiInTable()
    {
        var parsed = Parse("<table><template><table>A<div>B</div><!--c--><?Inside d?></table></template></table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var outerTable = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        var template = (Element) outerTable.FirstChild!;
        var content = template.TemplateContent!;
        content.FirstChild.Should().BeOfType<Text>().Which.Data.Should().Be("A");
        var fostered = (Element) content.FirstChild!.NextSibling!;
        fostered.LocalName.Should().Be("div");
        ((Text) fostered.FirstChild!).Data.Should().Be("B");
        var innerTable = (Element) content.LastChild!;
        innerTable.LocalName.Should().Be("table");
        innerTable.FirstChild.Should().BeOfType<Comment>();
        var pi = (ProcessingInstruction) innerTable.LastChild!;
        pi.Target.Should().Be("Inside");
        pi.Data.Should().Be("d");
        foreach (var child in content.ChildNodes)
            child.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
    }

    [Test]
    public void TemplateMarkerPreventsFormattingFromCrossingItsBoundary()
    {
        var parsed = Parse("<body><b>before<template><i>inside</template>after</b><p>tail", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        var bold = (Element) body.FirstChild!;
        bold.LocalName.Should().Be("b");
        var template = (Element) bold.FirstChild!.NextSibling!;
        template.LocalName.Should().Be("template");
        ((Element) template.TemplateContent!.FirstChild!).LocalName.Should().Be("i");
        bold.LastChild.Should().BeOfType<Text>().Which.Data.Should().Be("after");
        body.LastChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("p");
        var builder = typeof(HtmlParserSession).GetField("_builder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(parsed.Session)!;
        var formatting = (System.Collections.ICollection) typeof(HtmlTreeBuilder).GetField("_formatting",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(builder)!;
        formatting.Count.Should().Be(0);
    }

    [Test]
    public void TemplateStartDisarmsFramesetInBody()
    {
        var parsed = Parse("<body><template></template><frameset><p>after", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        ((Element) body.FirstChild!).LocalName.Should().Be("template");
        ((Element) body.LastChild!).LocalName.Should().Be("p");
    }

    [Test]
    public void ApplicablePatchingStopsBeforeTemplateSideEffectsWhileShadowSpellingFallsBack()
    {
        var stopped = Parse("<body><div>x</div><template for=target><p>y</p></template>", 1);
        stopped.Step.Kind.Should().Be(HtmlParseStepKind.MissingFeature);
        stopped.Step.MissingFeature.Should().Be(HtmlMissingFeature.Templates);
        stopped.Step.Offset.Should().Be(18);
        SerializeWithTemplateContents(stopped.Document).Should().Be(
            "<html><head></head><body><div>x</div></body></html>");

        var fallback = Parse("<template shadowrootmode=open for=target><p>x</p></template>", 1);
        fallback.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var template = (Element) fallback.Document.DocumentElement!.FirstChild!.FirstChild!;
        template.TemplateContent!.FirstChild.Should().BeOfType<Element>();
        template.GetAttribute("for").Should().Be("target");

        var invalidShadow = Parse("<template shadowrootmode=invalid for=target>", 1);
        invalidShadow.Step.Kind.Should().Be(HtmlParseStepKind.MissingFeature);
        invalidShadow.Step.MissingFeature.Should().Be(HtmlMissingFeature.Templates);
    }

    [Test]
    public void NestedTemplateEofIsResumableAtQuotaOne()
    {
        const int depth = 64;
        var source = string.Concat(Enumerable.Repeat("<template>", depth)) + "x";
        var parsed = Parse(source, 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        parsed.Diagnostics.Items.Count(item => item.Code == "html/tree-eof-in-template").Should().Be(depth);
        var current = (Element) parsed.Document.DocumentElement!.FirstChild!.FirstChild!;
        for (var i = 1; i < depth; i++)
            current = (Element) current.TemplateContent!.FirstChild!;
        ((Text) current.TemplateContent!.FirstChild!).Data.Should().Be("x");
    }

    [Test]
    public void TemplateDepthAndEofWorkAreBounded()
    {
        var limits = new HtmlParseOptions { Limits = new ParseLimits { MaxNestingDepth = 3 } };
        Parse("<template>x</template>", 1, limits).Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var exception = Assert.Throws<ParseLimitException>(() => Parse("<template><template>", 1, limits));
        exception!.Kind.Should().Be(ParseLimitKind.NestingDepth);

        static string Source(int count) => string.Concat(Enumerable.Repeat("<template>", count)) + "x";
        var smaller = Parse(Source(128), 1);
        var larger = Parse(Source(256), 1);
        var repeated = Parse(Source(256), 1);
        larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
        larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 3);
    }

    [Test]
    public void CancellationAfterPartialTemplateEofTerminatesSession()
    {
        var document = Document.CreateHtml();
        var diagnostics = new ParseDiagnosticCollector();
        var session = new HtmlParserSession(document, new HtmlParseOptions { Diagnostics = diagnostics });
        session.AppendInput(string.Concat(Enumerable.Repeat("<template>", 32)), isFinal: true);
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if (diagnostics.Items.Any(item => item.Code == "html/tree-eof-in-template")) break;
        }
        diagnostics.Items.Should().Contain(item => item.Code == "html/tree-eof-in-template");
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, source.Token));
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
        var template = (Element) document.DocumentElement!.FirstChild!.FirstChild!;
        template.TemplateContent.Should().NotBeNull();
    }

    [Test]
    public void TemplateShortInputSplitsMatchWholeInput()
    {
        const string source = "<template><table><tr><td>A</td></tr></table></template>z";
        var expected = SerializeWithTemplateContents(Parse(source).Document);
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            for (var turn = 0; turn < 100_000; turn++)
            {
                var step = session.Drive(1, CancellationToken.None);
                if (step.Kind == HtmlParseStepKind.NeedInput) break;
                step.Kind.Should().Be(HtmlParseStepKind.Yielded);
            }
            session.AppendInput(source[split..], isFinal: true);
            HtmlParseStep final;
            var turns = 0;
            do
            {
                final = session.Drive(1, CancellationToken.None);
                if (++turns > 100_000) throw new InvalidOperationException("Template parse stalled.");
            } while (final.Kind == HtmlParseStepKind.Yielded);
            final.Kind.Should().Be(HtmlParseStepKind.Complete);
            SerializeWithTemplateContents(document).Should().Be(expected);
        }
    }

    private static string SerializeWithTemplateContents(Node root)
    {
        var result = new StringBuilder();
        void Visit(Node node)
        {
            switch (node)
            {
                case Element element:
                    result.Append('<').Append(element.LocalName).Append('>');
                    if (element.TemplateContent is { } content)
                    {
                        result.Append('{');
                        foreach (var child in content.ChildNodes) Visit(child);
                        result.Append('}');
                    }
                    foreach (var child in element.ChildNodes) Visit(child);
                    result.Append("</").Append(element.LocalName).Append('>');
                    break;
                case Text text: result.Append(text.Data); break;
                case Comment comment: result.Append("<!--").Append(comment.Data).Append("-->"); break;
                case ProcessingInstruction pi: result.Append("<?").Append(pi.Target).Append(' ').Append(pi.Data).Append("?>"); break;
                default:
                    foreach (var child in node.ChildNodes) Visit(child);
                    break;
            }
        }
        Visit(root);
        return result.ToString();
    }
}
