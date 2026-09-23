#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    private static (Document Document, HtmlParseStep Step, ParseDiagnosticCollector Diagnostics, HtmlParserSession Session) Parse(
        string source, int quota = 100_000, HtmlParseOptions? options = null, HtmlDocumentContext context = default)
    {
        var document = Document.CreateHtml();
        var diagnostics = options?.Diagnostics ?? new ParseDiagnosticCollector();
        options ??= new HtmlParseOptions { Diagnostics = diagnostics };
        var session = new HtmlParserSession(document, options, context);
        session.AppendInput(source, isFinal: true);
        HtmlParseStep step;
        var turns = 0;
        do
        {
            step = session.Drive(quota, CancellationToken.None);
            if (++turns > 1_000_000) throw new InvalidOperationException("Tree parser did not make progress.");
        } while (step.Kind == HtmlParseStepKind.Yielded);
        return (document, step, diagnostics, session);
    }

    [TestCase("", "<html><head></head><body></body></html>")]
    [TestCase("<!doctype html><p>x", "<!doctype html><html><head></head><body><p>x</p></body></html>")]
    [TestCase("<html><head><title>x</title></head><body>y</body></html>", "<html><head><title>x</title></head><body>y</body></html>")]
    [TestCase("<div><p>a<div>b", "<html><head></head><body><div><p>a</p><div>b</div></div></body></html>")]
    [TestCase("<h1>a<h2>b", "<html><head></head><body><h1>a</h1><h2>b</h2></body></html>")]
    [TestCase("<ul><li>a<li>b", "<html><head></head><body><ul><li>a</li><li>b</li></ul></body></html>")]
    [TestCase("<p>x<br>y", "<html><head></head><body><p>x<br></br>y</p></body></html>")]
    [TestCase("<p>x</br>y", "<html><head></head><body><p>x<br></br>y</p></body></html>")]
    public void SupportedTrees(string source, string expected)
    {
        var parsed = Parse(source);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(expected);
    }

    [Test]
    public void ProcessingInstructionsRetainPlacementAndCase()
    {
        var parsed = Parse("<?Prolog yes?><html><head><?InHead h?></head><body><?InBody b?></body></html><?Tail t?>");
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var document = parsed.Document;
        ((ProcessingInstruction) document.FirstChild!).Target.Should().Be("Prolog");
        ((ProcessingInstruction) document.FirstChild!).Data.Should().Be("yes");
        ((ProcessingInstruction) document.LastChild!).Target.Should().Be("Tail");
        ((ProcessingInstruction) document.DocumentElement!.FirstChild!.FirstChild!).Target.Should().Be("InHead");
        ((ProcessingInstruction) document.DocumentElement.LastChild!.FirstChild!).Target.Should().Be("InBody");
    }

    [TestCase("<table>", "Tables", "<html><head></head><body></body></html>")]
    [TestCase("<b>", "Formatting", "<html><head></head><body></body></html>")]
    [TestCase("<select>", "Select", "<html><head></head><body></body></html>")]
    [TestCase("<template>", "Templates", "<html><head></head></html>")]
    [TestCase("<frameset>", "Framesets", "<html><head></head></html>")]
    [TestCase("<svg>", "ForeignContent", "<html><head></head><body></body></html>")]
    public void UnsupportedBranchStopsBeforeItsMutation(string source, string family, string priorTree)
    {
        var parsed = Parse(source);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.MissingFeature);
        parsed.Step.MissingFeature.Should().Be(Enum.Parse<HtmlMissingFeature>(family));
        parsed.Step.Offset.Should().Be(0);
        Serialize(parsed.Document).Should().Be(priorTree);
        Assert.Throws<InvalidOperationException>(() => parsed.Session.Drive(1, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => parsed.Session.AppendInput(""));
    }

    [Test]
    public void SplitInputAndQuotaOnePreserveTreeAndTextIdentity()
    {
        const string source = "<!doctype html><p>A&amp;B<br>c</p>";
        var expected = Serialize(Parse(source).Document);
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            for (var turn = 0; turn < 100_000; turn++)
            {
                var step = session.Drive(1, CancellationToken.None);
                if (step.Kind == HtmlParseStepKind.NeedInput) break;
                if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected pre-final parse result.");
            }
            session.AppendInput(source[split..], isFinal: true);
            HtmlParseStep final;
            var turns = 0;
            do
            {
                final = session.Drive(1, CancellationToken.None);
                if (++turns > 100_000) throw new InvalidOperationException("Quota-one parse stalled.");
            } while (final.Kind == HtmlParseStepKind.Yielded);
            final.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(document).Should().Be(expected);
            var p = (Element) document.DocumentElement!.LastChild!.FirstChild!;
            p.FirstChild.Should().BeOfType<Text>();
            p.FirstChild!.NextSibling.Should().BeOfType<Element>();
        }
    }

    [Test]
    public void HtmlAndBodyRepeatedAttributesMergeWithoutReplacingNodes()
    {
        var parsed = Parse("<html id=a><head></head><body id=b><html id=c x=y><body id=d z=w>");
        var html = parsed.Document.DocumentElement!;
        var body = (Element) html.LastChild!;
        html.GetAttribute("id").Should().Be("a");
        html.GetAttribute("x").Should().Be("y");
        body.GetAttribute("id").Should().Be("b");
        body.GetAttribute("z").Should().Be("w");
    }

    [TestCase("<!doctype html>", "NoQuirks")]
    [TestCase("", "Quirks")]
    [TestCase("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN'>", "LimitedQuirks")]
    [TestCase("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN'>", "Quirks")]
    [TestCase("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN' 'x'>", "LimitedQuirks")]
    public void DoctypeMode(string source, string expected)
    {
        Parse(source).Document.Mode.Should().Be(Enum.Parse<DocumentMode>(expected));
    }

    [Test]
    public void NestingDepthCountsImplicitAndVoidElements()
    {
        var options = new HtmlParseOptions { Limits = new ParseLimits { MaxNestingDepth = 4 } };
        Parse("<br>", options: options).Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Assert.That(Assert.Throws<ParseLimitException>(() => Parse("<div><span><custom>", options: options))!.Kind,
            Is.EqualTo(ParseLimitKind.NestingDepth));
    }

    private static string Serialize(Node root)
    {
        var result = new System.Text.StringBuilder();
        void Visit(Node node)
        {
            switch (node)
            {
                case Document: foreach (var child in node.ChildNodes) Visit(child); break;
                case DocumentType doctype: result.Append("<!doctype ").Append(doctype.Name).Append('>'); break;
                case Element element:
                    result.Append('<').Append(element.LocalName).Append('>');
                    foreach (var child in element.ChildNodes) Visit(child);
                    result.Append("</").Append(element.LocalName).Append('>');
                    break;
                case Text text: result.Append(text.Data); break;
                case Comment comment: result.Append("<!--").Append(comment.Data).Append("-->"); break;
                case ProcessingInstruction instruction:
                    result.Append("<?").Append(instruction.Target).Append(' ').Append(instruction.Data).Append("?>");
                    break;
            }
        }
        Visit(root);
        return result.ToString();
    }
}
