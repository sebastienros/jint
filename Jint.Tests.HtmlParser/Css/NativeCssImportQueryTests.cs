using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssImportQueryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void CachedValuesRejectChildEditsEvenWhenTheImportDoesNotMatch(bool inactive)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var root = CssStyleSheet.Parse("@import 'child.css'" + (inactive ? " print" : "") + ";");
        var child = CssStyleSheet.Parse("div { opacity:.3 }");
        ((CssImportRule) root.Rules[0]).SetStyleSheet(child, null, null, new CssValueWork(default));
        var query = Query(document, [new(root, NativeCssOrigin.Author)], new CssValueWork(default));
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be(inactive ? "1" : "0.3");
        var rootStamp = root.Stamp;
        var nativeStamp = document.MutationStamp;
        ((CssStyleRule) child.Rules[0]).Style.SetProperty("opacity", ".7");
        root.Stamp.Should().Be(rootStamp, "child edits do not recursively bubble sheet revisions");
        document.MutationStamp.Should().Be(nativeStamp);
        Action cached = () => query.GetProperty(target, "opacity", ref matching);
        cached.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
    }

    [Test]
    public void ImportedRulesKeepTheirRootShadowScopeAndSourcePosition()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var light = document.CreateElement("span");
        host.AppendChild(light);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var owner = document.CreateElement("style");
        shadow.AppendChild(owner);
        var target = document.CreateElement("span");
        shadow.AppendChild(target);
        var root = CssStyleSheet.Parse("@import 'child.css'; span { opacity:.7 }");
        root.SetAttachment(new CssStyleSheetAttachment { OwnerNode = owner });
        var child = CssStyleSheet.Parse("span { opacity:.3; visibility:hidden }");
        ((CssImportRule) root.Rules[0]).SetStyleSheet(child, null, null, new CssValueWork(default));
        child.Attachment.OwnerNode.Should().BeNull();
        var query = Query(document, [new(root, NativeCssOrigin.Author)], new CssValueWork(default));
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "opacity", ref matching).Text.Should().Be("0.7");
        query.GetProperty(target, "visibility", ref matching).Text.Should().Be("hidden");
        query.GetProperty(light, "opacity", ref matching).Text.Should().Be("1");
        query.GetProperty(light, "visibility", ref matching).Text.Should().Be("visible");
    }

    [Test]
    public void TheFinalGraphCheckpointCannotInvalidateAnAlreadyAcceptedDocumentWitness()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var root = CssStyleSheet.Parse("");
        var roots = Enumerable.Repeat(new NativeCssSheet(root, NativeCssOrigin.Author), 5000).ToArray();
        var armed = false;
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (armed && ++checks == 2) target.SetAttribute("data-late", "changed");
        });
        var query = Query(document, roots, work);
        armed = true;
        Action verify = query.Verify;
        verify.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
        checks.Should().BeGreaterThanOrEqualTo(2);
        target.GetAttribute("data-late").Should().Be("changed");
    }

    private static NativeCssQuery Query(Document document, NativeCssSheet[] sheets, CssValueWork work)
        => new(document, sheets, [], new CssMediaEnvironment(), new(document, null, null, null),
            work);
}
