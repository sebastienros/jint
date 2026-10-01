#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class HtmlPublicApiTests
{
    [Test]
    public void EmptyDocumentHasImplicitStructureAndDefaultMetadata()
    {
        var options = new HtmlParseOptions();
        options.ScriptingEnabled.Should().BeFalse();
        options.Limits.Should().BeSameAs(ParseLimits.Unbounded);
        options.Diagnostics.Should().BeNull();
        var document = MarkupParser.ParseHtml("");
        document.Kind.Should().Be(DocumentKind.Html);
        document.ContentType.Should().Be("text/html");
        document.CharacterSet.Should().Be("UTF-8");
        document.Mode.Should().Be(DocumentMode.Quirks);
        document.DocumentElement!.LocalName.Should().Be("html");
        document.DocumentElement.FirstChild.As<Element>().LocalName.Should().Be("head");
        document.DocumentElement.LastChild.As<Element>().LocalName.Should().Be("body");
    }

    [Test]
    public void MalformedMarkupRecoversAndBoundsDiagnostics()
    {
        var diagnostics = new ParseDiagnosticCollector(1);
        var document = MarkupParser.ParseHtml("<p>one<p>two</unexpected></unexpected>", new() { Diagnostics = diagnostics });
        document.DocumentElement!.LastChild!.ChildCount.Should().Be(2);
        diagnostics.Items.Should().HaveCount(1);
        diagnostics.IsTruncated.Should().BeTrue();
        diagnostics.Items[0].Code.Should().StartWith("html/");
    }

    [TestCase("table", "<tr><td>x", "tbody", Namespaces.Html)]
    [TestCase("select", "<option>x<option>y", "option", Namespaces.Html)]
    [TestCase("svg", "<circle/>", "circle", Namespaces.Svg)]
    [TestCase("math", "<mi>x", "mi", Namespaces.MathMl)]
    [TestCase("div", "<template><b>x</b></template>", "template", Namespaces.Html)]
    public void ContextualFragmentKeepsOwnerAndExistingChildren(string name, string input, string childName, string childNamespace)
    {
        var owner = Document.CreateHtml();
        var context = owner.CreateElementNS(childNamespace, name);
        var old = owner.CreateComment("untouched");
        context.AppendChild(old);
        var fragment = MarkupParser.ParseHtmlFragment(input, context);
        fragment.OwnerDocument.Should().BeSameAs(owner);
        fragment.ParentNode.Should().BeNull();
        fragment.FirstChild!.OwnerDocument.Should().BeSameAs(owner);
        fragment.FirstChild.As<Element>().LocalName.Should().Be(childName);
        fragment.FirstChild.As<Element>().NamespaceUri.Should().Be(childNamespace);
        context.FirstChild.Should().BeSameAs(old);
        context.ChildCount.Should().Be(1);
    }

    [Test]
    public void TemplateContextUsesTheSuppliedOwnerInsteadOfItsContentOwner()
    {
        var owner = Document.CreateHtml();
        var context = owner.CreateElement("template");
        var fragment = MarkupParser.ParseHtmlFragment("<p>x", context);
        fragment.OwnerDocument.Should().BeSameAs(owner);
        fragment.FirstChild!.OwnerDocument.Should().BeSameAs(owner);
        context.TemplateContent!.ChildCount.Should().Be(0);
        context.ChildCount.Should().Be(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ScriptIsAlwaysInertAndShadowAttachmentRequiresInternalPermission(bool scripting)
    {
        var document = MarkupParser.ParseHtml("<script>throw new Error('executed')</script><div><template shadowrootmode=open><p>x</template></div>",
            new() { ScriptingEnabled = scripting });
        var head = document.DocumentElement!.FirstChild!;
        head.FirstChild.As<Element>().LocalName.Should().Be("script");
        head.FirstChild!.FirstChild.As<Text>().Data.Should().Be("throw new Error('executed')");
        var host = document.DocumentElement.LastChild!.FirstChild.As<Element>();
        host.AttachedShadowRoot.Should().BeNull();
        host.FirstChild.As<Element>().TemplateContent!.FirstChild.As<Element>().LocalName.Should().Be("p");
        MarkupParser.ParseHtmlFragment("<script>throw 1</script>", host, new() { ScriptingEnabled = scripting })
            .FirstChild!.FirstChild.As<Text>().Data.Should().Be("throw 1");
    }

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public void ScriptingFlagChangesNoscriptGrammarOnly(bool scripting, int children)
    {
        var context = Document.CreateHtml().CreateElement("noscript");
        var fragment = MarkupParser.ParseHtmlFragment("<b>x</b>", context, new() { ScriptingEnabled = scripting });
        fragment.FirstChild.Should().BeOfType(scripting ? typeof(Text) : typeof(Element));
        fragment.FirstChild!.ChildCount.Should().Be(children);
    }

    [Test]
    public void InclusiveInputTokenAndDepthLimitsRetainTheirExceptionTaxonomy()
    {
        MarkupParser.ParseHtml("<br>", new() { Limits = new() { MaxInputCharacters = 4, MaxTokenCharacters = 4, MaxNestingDepth = 3 } });
        foreach (var (limits, kind) in new[]
        {
            (new ParseLimits { MaxInputCharacters = 3 }, ParseLimitKind.InputCharacters),
            (new ParseLimits { MaxTokenCharacters = 3 }, ParseLimitKind.TokenCharacters),
            (new ParseLimits { MaxNestingDepth = 2 }, ParseLimitKind.NestingDepth)
        })
        {
            Assert.Throws<ParseLimitException>(() => MarkupParser.ParseHtml("<br>", new() { Limits = limits }))!.Kind.Should().Be(kind);
        }
        var context = Document.CreateHtml().CreateElement("div");
        MarkupParser.ParseHtmlFragment("<br>", context, new() { Limits = new() { MaxInputCharacters = 4 } });
        Assert.Throws<ParseLimitException>(() => MarkupParser.ParseHtmlFragment("<br>", context,
            new() { Limits = new() { MaxInputCharacters = 3 } }))!.Kind.Should().Be(ParseLimitKind.InputCharacters);
        Assert.Throws<ArgumentNullException>(() => new HtmlParseOptions { Limits = null! });
    }

    [Test]
    public void NullArgumentsAndCancellationArePreserved()
    {
        var context = Document.CreateHtml().CreateElement("div");
        Assert.Throws<ArgumentNullException>(() => MarkupParser.ParseHtml(null!));
        Assert.Throws<ArgumentNullException>(() => MarkupParser.ParseHtmlFragment(null!, context));
        Assert.Throws<ArgumentNullException>(() => MarkupParser.ParseHtmlFragment("", null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseHtml("", cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseHtmlFragment("", context, cancellationToken: cancellation.Token));
    }
}
