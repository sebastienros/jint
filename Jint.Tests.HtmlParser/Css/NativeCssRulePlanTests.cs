using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssRulePlanTests
{
    [Test]
    public void DomEditsReuseOnlyTheRulePlanAndAlwaysRematchElements()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var sheet = CssStyleSheet.Parse("div:empty {display:none} div.off{display:none} [data-off]{display:none}");
        var diagnostics = new NativeCssQueryDiagnostics();
        string Read()
            => ReadProperty(document, target, [new(sheet, NativeCssOrigin.Author)], diagnostics);
        Read().Should().Be("none");
        target.AppendChild(document.CreateTextNode("visible"));
        Read().Should().Be("block");
        target.SetAttribute("class", "off");
        Read().Should().Be("none");
        target.RemoveAttribute("class");
        target.SetAttribute("data-off", "");
        Read().Should().Be("none");
        target.RemoveAttribute("data-off");
        target.SetAttribute("style", "display:flex");
        Read().Should().Be("flex");
        diagnostics.Queries.Sum(q => q.RulePlanBuilds).Should().Be(1);
        diagnostics.Queries.Sum(q => q.RulePlanReuses).Should().Be(4);
    }

    [Test]
    public void SheetEditsOrderOriginsAndMediaRetireThePlan()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var first = CssStyleSheet.Parse("@layer a,b; @layer a{div{display:none}} @media (min-width:500px){@layer b{div{display:flex}}}");
        var second = CssStyleSheet.Parse("div{display:block}");
        var diagnostics = new NativeCssQueryDiagnostics();
        var inputs = new[] { new NativeCssSheet(first, NativeCssOrigin.Author), new NativeCssSheet(second, NativeCssOrigin.Author) };
        string Read(int width = 1024) => ReadProperty(document, target, inputs, diagnostics, new() { Width = width });
        Read().Should().Be("block");
        Read().Should().Be("block");
        second.Disabled = true;
        Read().Should().Be("flex");
        Read(100).Should().Be("none");
        second.Disabled = false;
        inputs = [new(second, NativeCssOrigin.Author), new(first, NativeCssOrigin.Author)];
        Read().Should().Be("block");
        ((CssStyleRule) second.Rules[0]).Style.SetProperty("display", "grid", "important");
        Read().Should().Be("grid");
        inputs = [new(second, NativeCssOrigin.User), new(first, NativeCssOrigin.Author)];
        Read().Should().Be("grid");
        diagnostics.Queries.Sum(q => q.RulePlanBuilds).Should().Be(6);
        diagnostics.Queries.Sum(q => q.RulePlanReuses).Should().Be(1);
    }

    [Test]
    public void ImportedSheetEditsAndReplacementRebuildThePlan()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        document.AppendChild(target);
        var sheet = CssStyleSheet.Parse("@import 'child';");
        var child = CssStyleSheet.Parse("div {display:none}");
        ((CssImportRule) sheet.Rules[0]).SetStyleSheet(child, null, null, new(default));
        var diagnostics = new NativeCssQueryDiagnostics();
        string Read() => ReadProperty(document, target, [new(sheet, NativeCssOrigin.Author)], diagnostics);
        Read().Should().Be("none");
        Read().Should().Be("none");
        ((CssStyleRule) child.Rules[0]).Style.SetProperty("display", "flex");
        Read().Should().Be("flex");
        sheet.ReplaceText("div {display:grid}");
        Read().Should().Be("grid");
        diagnostics.Queries.Sum(q => q.RulePlanBuilds).Should().Be(3);
        diagnostics.Queries.Sum(q => q.RulePlanReuses).Should().Be(1);
    }

    [Test]
    public void MovingASheetOwnerIntoAShadowTreeRetiresItsScopePlan()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("section");
        document.AppendChild(root);
        var target = document.CreateElement("p");
        var owner = document.CreateElement("style");
        var host = document.CreateElement("div");
        root.AppendChild(target);
        root.AppendChild(owner);
        root.AppendChild(host);
        var shadow = host.AttachShadow(new ShadowRootInit(ShadowRootMode.Open));
        var sheet = CssStyleSheet.Parse("p {display:none}");
        sheet.SetAttachment(new() { OwnerNode = owner });
        var diagnostics = new NativeCssQueryDiagnostics();
        string Read() => ReadProperty(document, target, [new(sheet, NativeCssOrigin.Author)], diagnostics);
        Read().Should().Be("none");
        Read().Should().Be("none");
        shadow.AppendChild(owner);
        Read().Should().Be("inline");
        Read().Should().Be("inline");
        diagnostics.Queries.Sum(q => q.RulePlanBuilds).Should().Be(2);
        diagnostics.Queries.Sum(q => q.RulePlanReuses).Should().Be(2);
    }

    [Test]
    public void IdenticalSourcesOnDifferentDocumentsDoNotShareMutableCssModels()
    {
        var diagnostics = new NativeCssQueryDiagnostics();
        for (var index = 0; index < 2; index++)
        {
            var document = Document.CreateHtml();
            var target = document.CreateElement("div");
            document.AppendChild(target);
            var sheet = CssStyleSheet.Parse("div {display:none}");
            ReadProperty(document, target, [new(sheet, NativeCssOrigin.Author)], diagnostics).Should().Be("none");
            sheet.Disabled = true;
        }
        diagnostics.Queries.Sum(q => q.RulePlanBuilds).Should().Be(2);
        diagnostics.Queries.Sum(q => q.RulePlanReuses).Should().Be(0);
    }

    private static string ReadProperty(Document document, Element element, NativeCssSheet[] sheets,
        NativeCssQueryDiagnostics diagnostics, CssMediaEnvironment? media = null)
    {
        var query = new NativeCssQuery(document, sheets, [], media ?? new(), new(document, null, null, null),
            new CssValueWork(default), readInlineAttributes: true, diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, default);
        return query.GetProperty(element, "display", ref matching).Text;
    }
}
