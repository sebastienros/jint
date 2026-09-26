#nullable enable
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeReflectionTests
{
    [TestCase(null, "https://example.test/main/index.html")]
    [TestCase("", "https://example.test/main/index.html")]
    [TestCase("sub/", "https://example.test/main/sub/")]
    public void BaseHrefUsesFallbackWithoutResolvingThroughAnyBase(string? value, string expected)
    {
        using var engine = new Engine();
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        DomDocumentState.Of(document).Url = "https://example.test/main/index.html";
        var html = document.CreateElement("html");
        document.AppendChild(html);
        var first = document.CreateElement("base");
        first.SetAttribute("href", "https://other.test/ignored/");
        html.AppendChild(first);
        var target = document.CreateElement("base");
        if (value is not null) target.SetAttribute("href", value);
        html.AppendChild(target);

        var realm = DomRealm.Of(engine);
        DomDocumentState.BaseHref(realm, target).AsString().Should().Be(expected);
        html.RemoveChild(first);
        DomDocumentState.BaseHref(realm, target).AsString().Should().Be(expected);
    }

    [Test]
    public void OrdinaryReflectionSkipsTheBoundedBaseWalkOnALargeDocument()
    {
        var budget = new ReadBudget();
        using var engine = new Engine(options => options.AddConstraint(budget));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        for (var i = 0; i < 8192; i++) root.AppendChild(document.CreateElement("div"));
        var target = document.CreateElement("img");
        target.SetAttribute("title", "ordinary");
        target.SetAttribute("src", "relative.png");
        root.AppendChild(target);
        DomDocumentState.Of(document).Url = "https://example.test/";
        var realm = DomRealm.Of(engine);

        ReflectedAttribute.Text("HTMLElement.title", "title").Get(realm, target).AsString().Should().Be("ordinary");
        // Only the final local attribute checkpoint runs; the document-wide base walk is still skipped.
        budget.Checks.Should().Be(1);
        var failure = Caught.Exception(() => ReflectedAttribute.Url("HTMLImageElement.src", "src").Get(realm, target));
        failure.Should().BeOfType<ReadBudgetExceededException>();
        budget.Checks.Should().Be(3);
    }

    private sealed class ReadBudget : Constraint
    {
        internal int Checks { get; private set; }
        public override void Check()
        {
            if (++Checks == 3) throw new ReadBudgetExceededException();
        }
        public override void Reset() { }
    }
    private sealed class ReadBudgetExceededException : Exception { }
}
