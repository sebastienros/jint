#nullable enable
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Styling;
using Jint.Browser.Accessibility;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Accessibility;

public sealed class InertCssQueryTests
{
    [Test]
    public void ParseRegistersCompletedStylesWithoutDemandingCssSyntaxOrValues()
    {
        var document = ContentDom.Parse("""
            <style id=sheet>.gone {display:none;background:red}</style>
            <template><style>.gone {display:block}</style></template>
            <link rel=stylesheet href=https://example.test/not-fetched.css>
            <svg><style><![CDATA[.svg {visibility:hidden}]]></style></svg>
            <div id=t class=gone style="white-space:pre">text</div>
            """);
        NativeCssStyleSheets.RealmOf(document).Should().BeNull();
        var work = new CssValueWork(default);
        var owner = ContentDom.ElementById(document, "sheet")!;
        var resource = NativeCssStyleSheets.PrepareOwner(document, owner, work)!;
        resource.Loaded.Should().BeTrue();
        resource.Associated.Should().BeTrue();
        resource.Sheet.Should().BeNull();
        var diagnostics = new NativeCssQueryDiagnostics();
        var traversal = CssCascade.Traversal.For(document, diagnostics: diagnostics)!;
        var record = diagnostics.Queries.Single();
        record.StatePublications.Should().Be(0);
        record.ComputedPublications.Should().BeEmpty();
        record.RuleAttempts.Should().Be(0);
        NativeCssStyleSheets.Get(document, work).Count.Should().Be(2);
        var target = ContentDom.ElementById(document, "t")!;
        traversal.Of(target).GetPropertyValue("display").Should().Be("none");
        traversal.Of(target).GetPropertyValue("white-space").Should().Be("pre");
        record.ComputedPublications.Should().NotContainKey("background");
        record.ComputedPublications.Should().NotContainKey("background-color");
        var template = ContentDom.First(document, "template")!;
        var templateStyle = ContentDom.First(template.TemplateContent!, "style")!;
        NativeCssStyleSheets.AssociatedOwner(templateStyle, work).Should().BeNull();
        var link = ContentDom.First(document, "link")!;
        NativeCssStyleSheets.AssociatedOwner(link, work).Should().BeNull();
    }

    [Test]
    public void InertQueryUsesNativeUaAndDisabledScriptingWithoutInventingFocus()
    {
        var document = ContentDom.Parse("""
            <style>
              @media (scripting: none) {#t {opacity:.25}}
              #t:focus {display:none}
            </style>
            <section id=t style="color:red"></section>
            """);
        var target = ContentDom.ElementById(document, "t")!;
        var traversal = CssCascade.Traversal.For(document)!;
        var style = traversal.Of(target);
        style.GetPropertyValue("display").Should().Be("block");
        style.GetPropertyValue("opacity").Should().Be("0.25");
        style.GetPropertyValue("color").Should().Be("red");
    }

    [Test]
    public void DeepCompletedStyleChainsScaleLinearlyWithDepth()
    {
        static int Checks(int depth)
        {
            var html = new System.Text.StringBuilder();
            for (var i = 0; i < depth; i++) html.Append("<div><style></style>");
            for (var i = 0; i < depth; i++) html.Append("</div>");
            var checks = 0;
            ContentDom.Parse(html.ToString(), checkpoint: () => checks++);
            return checks;
        }
        var single = Checks(512);
        var doubled = Checks(1024);
        // Fixed completion/rounding overhead is bounded; no owner-to-root scan grows with depth.
        doubled.Should().BeLessThanOrEqualTo(single * 2 + 16);
    }

    [Test]
    public void LongInertStyleSourceCopyPollsCancellationAtParserCompletion()
    {
        // The long source produces repeated bounded checks during completion registration.
        var html = "<style>/*" + new string('x', 20000) + "*/ .gone {display:none}</style>";
        var checks = 0;
        ContentDom.Parse(html, checkpoint: () => checks++);
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        // Cancel near completion registration, before its final installation/return checks.
        Assert.Throws<OperationCanceledException>(() => ContentDom.Parse(html,
            cancellationToken: cancellation.Token, checkpoint: () =>
            {
                if (++calls == checks - 3) cancellation.Cancel();
            }));
        cancellation.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public void VisibilityOperationCheckpointAndTokenReachNativeSelectorMatching()
    {
        var name = new string('x', 20000);
        var document = ContentDom.Parse("<style>." + name + " {display:none}</style><div id=t class='" + name + "'></div>");
        var target = ContentDom.ElementById(document, "t")!;
        using var cancellation = new CancellationTokenSource();
        var cancelDuringMatching = false;
        var work = new DomReadWork(_ => { if (cancelDuringMatching) cancellation.Cancel(); }, cancellation.Token);
        var visibility = new ElementVisibility(true, work);
        var traversal = visibility.CreateTraversal(document)!;
        cancelDuringMatching = true;
        Assert.Throws<OperationCanceledException>(() => visibility.Style(target, traversal));
        cancellation.IsCancellationRequested.Should().BeTrue();
    }
}
