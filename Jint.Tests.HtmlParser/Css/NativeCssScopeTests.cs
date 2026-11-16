using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.Tests.HtmlParser.Html.TreeConstruction;

namespace Jint.Tests.HtmlParser.Css;

// CSS Cascade 6 §3.5: https://drafts.csswg.org/css-cascade-6/#scoped-styles
public sealed class NativeCssScopeTests
{
    private const string Markup =
        "<div class=r id=root><span class=a id=a></span><div class=stop id=stop><span class=a id=inner></span></div></div>" +
        "<span class=a id=out></span>";

    [TestCase("@scope (.r) { .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) { .a { display:flex } }", "inner", "flex")]
    [TestCase("@scope (.r) { .a { display:flex } }", "out", "inline")]
    [TestCase("@scope (.r) to (.stop) { .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) to (.stop) { .a { display:flex } }", "inner", "inline")]
    [TestCase("@scope (.r) to (.stop) { div { display:flex } }", "stop", "inline")]
    [TestCase("@scope (.r) to (:scope > .stop) { .a { display:flex } }", "inner", "inline")]
    [TestCase("@scope (.r) to (:scope > .a) { .a { display:flex } }", "inner", "flex")]
    [TestCase("@scope (.r) to (.r) { .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) { .r { display:flex } }", "root", "inline")]
    [TestCase("@scope (.r) { :scope { display:flex } }", "root", "flex")]
    [TestCase("@scope (.r) { & { display:flex } }", "root", "flex")]
    [TestCase("@scope (.r) { > .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) { > .a { display:flex } }", "inner", "inline")]
    [TestCase("@scope (.r) { :scope > .a { display:flex } }", "inner", "inline")]
    [TestCase("@scope (.r) { body :scope .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) { main :scope .a { display:flex } }", "a", "inline")]
    [TestCase("@scope (.missing) { .a { display:flex } }", "a", "inline")]
    [TestCase("@scope { .a { display:flex } }", "out", "flex")]
    [TestCase("@scope to (.stop) { .a { display:flex } }", "inner", "inline")]
    [TestCase("@scope (.r) { .stop { .a { display:flex } } }", "inner", "flex")]
    [TestCase("@scope (.r) { .stop { .a { display:flex } } }", "a", "inline")]
    [TestCase("@scope (.r) { @media all { .a { display:flex } } }", "a", "flex")]
    [TestCase("@scope (.r) { @media not all { .a { display:flex } } }", "a", "inline")]
    [TestCase("@scope (.r) { @supports (display:flex) { .a { display:flex } } }", "a", "flex")]
    [TestCase("@scope (.r) { @layer x { .a { display:flex } } }", "a", "flex")]
    [TestCase("@scope (.r) { @scope (.stop) { .a { display:flex } } }", "inner", "flex")]
    [TestCase("@scope (.r) { @scope (.stop) { .a { display:flex } } }", "a", "inline")]
    [TestCase("@scope (.r) { @scope (:scope > .stop) { :scope { display:flex } } }", "stop", "flex")]
    [TestCase("@scope (.r) to (.stop) { @scope (.stop) { .a { display:flex } } }", "inner", "inline")]
    [TestCase("@scope (.r) { :is(:scope) > .a { display:flex } }", "a", "flex")]
    [TestCase("@scope (.r) { :not(:scope) > .a { display:flex } }", "inner", "flex")]
    [TestCase("@scope (.r) { :not(:scope) > .a { display:flex } }", "a", "inline")]
    [TestCase("@scope (.r) to (#inner) { @scope (.stop) { span { display:flex } } }", "inner", "inline")]
    [TestCase("@scope (.r) to (#a) { @scope (.stop) { span { display:flex } } }", "inner", "flex")]
    [TestCase("@scope (::before) { .a { display:flex } }", "a", "inline")]
    [TestCase("@scope (.r) junk { .a { display:flex } }", "a", "inline")]
    [TestCase("@scope (.r) to { .a { display:flex } }", "a", "inline")]
    [TestCase("@media all { @scope (.r) { .a { display:flex } } }", "a", "flex")]
    [TestCase(".x { @scope (.r) { .a { display:flex } } }", "a", "inline")]
    public void ScopedRulesMatchOnlySubjectsInScope(string css, string id, string expected) =>
        Display(css, id).Should().Be(expected);

    // Cascade 6 §3.6: scope proximity sits between specificity and order of appearance, and the
    // prelude does not contribute specificity.
    [TestCase("@scope (.stop) { .a { display:grid } } @scope (.r) { .a { display:flex } }", "inner", "grid")]
    [TestCase("@scope (.r) { .a { display:flex } } @scope (.stop) { .a { display:grid } }", "inner", "grid")]
    [TestCase("@scope (.stop) { span { display:grid } } @scope (.r) { .a { display:flex } }", "inner", "flex")]
    [TestCase("@scope (.r) { .a { display:flex } } .a { display:grid }", "a", "flex")]
    [TestCase("@scope (#root) { span { display:flex } } .a { display:grid }", "a", "grid")]
    [TestCase("@scope (.r) { :scope .a { display:flex } } .r .a { display:grid }", "a", "flex")]
    [TestCase("@scope (.r) { & .a { display:flex } } .a { display:grid }", "a", "flex")]
    [TestCase("@scope (div) { .a { display:flex } } @scope (div) { span { display:grid } }", "inner", "flex")]
    [TestCase(".a { display:grid !important } @scope (.r) { .a { display:flex !important } }", "a", "flex")]
    [TestCase("@scope (.r) { .a { display:flex !important } } .a { display:grid !important }", "a", "flex")]
    public void ScopeProximityOrdersEqualSpecificityBeforeAppearance(string css, string id, string expected) =>
        Display(css, id).Should().Be(expected);

    [Test]
    public void ScopeRuleExposesItsBoundariesAndSerializes()
    {
        var sheet = CssStyleSheet.Parse("@scope (.r ,  .s) to (.stop) { .a { display:flex } } @scope { b { color:red } }");
        var scope = (CssScopeRule) sheet.Rules[0];
        scope.Start.Should().Be(".r ,  .s");
        scope.End.Should().Be(".stop");
        scope.Rules.Count.Should().Be(1);
        scope.Rules[0].ParentRule.Should().BeSameAs(scope);
        var implicitScope = (CssScopeRule) sheet.Rules[1];
        implicitScope.Start.Should().BeNull();
        implicitScope.End.Should().BeNull();
        sheet.Serialize().Should().Contain("@scope (.r ,  .s) to (.stop) {").And.Contain("@scope {");
    }

    [Test]
    public void InsertedRulesInheritTheScope()
    {
        var document = MarkupParser.ParseHtml(Markup);
        var sheet = CssStyleSheet.Parse("@scope (.r) to (.stop) {}");
        ((CssScopeRule) sheet.Rules[0]).InsertRule(".a { display:flex }", 0);
        Display(document, sheet, "a").Should().Be("flex");
        Display(document, sheet, "out").Should().Be("inline");
    }

    private static string Display(string css, string id) =>
        Display(MarkupParser.ParseHtml(Markup), CssStyleSheet.Parse(css), id);

    private static string Display(Document document, CssStyleSheet sheet, string id)
    {
        var target = document.GetElementById(id)!;
        var query = new NativeCssQuery(document, [new(sheet, NativeCssOrigin.Author)], [], new CssMediaEnvironment(),
            new(document, null, null, null), new CssValueWork(default));
        var matching = new SelectorMatchWork(document, default);
        return query.GetProperty(target, "display", ref matching).Text;
    }
}
