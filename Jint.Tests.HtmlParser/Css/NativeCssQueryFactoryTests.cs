#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssQueryFactoryTests
{
    [Test]
    public void PaintUrlsNeedAnExplicitResolverAndOnlyResolveWhenRead()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        target.SetAttribute("style", "fill:src(var(--url)) currentcolor;--url:'paint.svg#p';color:red");
        var work = new CssValueWork(default);
        var selectors = new SelectorEnvironment(document, null, null, null);
        var without = NativeCssStyleSheets.CreateQuery(document, new(), selectors, work);
        without.Query.GetProperty(target, "color", ref without.Matching).Text.Should().Be("rgb(255, 0, 0)");
        Action read = () => without.Query.GetProperty(target, "fill", ref without.Matching);
        read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:url-resolver");

        var calls = 0;
        var input = NativeCssStyleSheets.CreateQuery(document, new(), selectors, work,
            resolveUrl: (owner, url, baseUrl, _) =>
            {
                owner.Should().BeSameAs(document);
                baseUrl.Should().BeNull();
                url.Should().Be("paint.svg#p");
                calls++;
                return "https://example.test/paint.svg#p";
            });
        calls.Should().Be(0);
        input.Query.GetProperty(target, "fill", ref input.Matching).Text
            .Should().Be("src(\"https://example.test/paint.svg#p\") rgb(255, 0, 0)");
        input.Query.GetProperty(target, "fill", ref input.Matching);
        calls.Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PaintResolutionCannotPublishAfterMutationOrCancellation(bool cancel)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        target.SetAttribute("style", "fill:url(paint.svg#p)");
        using var cancellation = new CancellationTokenSource();
        var input = NativeCssStyleSheets.CreateQuery(document, new(),
            new SelectorEnvironment(document, null, null, null), new CssValueWork(cancellation.Token),
            resolveUrl: (_, _, _, _) =>
            {
                if (cancel) cancellation.Cancel();
                else target.SetAttribute("class", "changed");
                return "https://example.test/paint.svg#p";
            });
        Action read = () => input.Query.GetProperty(target, "fill", ref input.Matching);
        if (cancel) read.Should().Throw<OperationCanceledException>();
        else read.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
    }

    [Test]
    public void RealmFreeFactorySharesActualSourcesAndUaWithoutComputingUnrequestedValues()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("section");
        document.AppendChild(root);
        root.SetAttribute("class", "gone");
        root.SetAttribute("style", "white-space:pre;color:red");
        var owner = document.CreateElement("style");
        root.AppendChild(owner);
        var work = new CssValueWork(default);
        NativeCssStyleSheets.Install(document, owner, ".gone {display:none;background:red}", "", "", work);
        var resource = NativeCssStyleSheets.PrepareOwner(document, owner, work)!;
        resource.Sheet.Should().BeNull();
        var diagnostics = new NativeCssQueryDiagnostics();
        var input = NativeCssStyleSheets.CreateQuery(document, new CssMediaEnvironment { Scripting = "none" },
            new SelectorEnvironment(document, null, null, null), work, diagnostics: diagnostics);
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(0);
        record.ComputedPublications.Should().BeEmpty();
        input.Query.GetProperty(root, "display", ref input.Matching).Text.Should().Be("none");
        input.Query.GetProperty(root, "white-space-collapse", ref input.Matching).Text.Should().Be("preserve");
        input.Query.GetProperty(root, "color", ref input.Matching).Text.Should().Be("rgb(255, 0, 0)");
        record.ComputedPublications.Should().NotContainKey("background");
        record.ComputedPublications.Should().NotContainKey("background-color");
    }

    [Test]
    public void RealmFreeSelectorWorkUsesTheOperationTokenAndCheckpoint()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var name = new string('x', 20000);
        target.SetAttribute("class", name);
        var owner = document.CreateElement("style");
        target.AppendChild(owner);
        NativeCssStyleSheets.Install(document, owner, "." + name + " {display:none}", "", "", new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var input = NativeCssStyleSheets.CreateQuery(document, new CssMediaEnvironment { Scripting = "none" },
            new SelectorEnvironment(document, null, null, null), new CssValueWork(cancellation.Token), cancellation.Cancel);
        Assert.Throws<OperationCanceledException>(() => input.Query.GetProperty(target, "display", ref input.Matching));
        cancellation.IsCancellationRequested.Should().BeTrue();
    }
}
