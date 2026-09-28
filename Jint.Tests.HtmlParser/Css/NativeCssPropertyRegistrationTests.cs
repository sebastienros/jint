using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssPropertyRegistrationTests
{
    [TestCase("<length>", "2em", "20px")]
    [TestCase("<length>", "1in", "96px")]
    [TestCase("<length-percentage>", "25%", "25%")]
    [TestCase("<length-percentage>", "calc(2em + 10%)", "calc(10% + 20px)")]
    [TestCase("<percentage>", "calc(20% + 5%)", "25%")]
    [TestCase("<number>", "calc(2 + 3)", "5")]
    [TestCase("<integer>", "calc(2.5)", "3")]
    [TestCase("<angle>", "1turn", "360deg")]
    [TestCase("<time>", "1000ms", "1s")]
    [TestCase("<resolution>", "96dpi", "1dppx")]
    [TestCase("<resolution>", "calc(-1dppx)", "0dppx")]
    [TestCase("<color>", "red", "rgb(255, 0, 0)")]
    [TestCase("<color>", "currentColor", "rgb(0, 128, 0)")]
    [TestCase("red | <color>", "red", "red")]
    [TestCase("<custom-ident>", "Word", "Word")]
    [TestCase("<string>", "'word'", "\"word\"")]
    [TestCase("<number>+", "1 2 3", "1 2 3")]
    [TestCase("<length>#", "1em, 2em", "10px, 20px")]
    public void TypedValuesComputeBeforeSubstitution(string syntax, string value, string expected)
    {
        var initial = syntax switch
        {
            "<color>" or "red | <color>" => "black",
            "<custom-ident>" => "word",
            "<string>" => "\"\"",
            "<angle>" => "0deg",
            "<time>" => "0s",
            "<resolution>" => "1dppx",
            "<percentage>" => "0%",
            _ => "0"
        };
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse($$"""
            @property --x {syntax:"{{syntax}}";inherits:false;initial-value:{{initial}}}
            div { font-size:10px; color:green; --x:{{value}}; --y:var(--x) }
            """);
        var query = Query(document, sheet);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "--y", ref matching).Text.Should().Be(expected);
        query.GetProperty(target, "--x", ref matching).Text.Should().Be(expected);
    }

    [TestCase(false, "", "2px")]
    [TestCase(true, "", "8px")]
    [TestCase(false, "inherit", "8px")]
    [TestCase(true, "initial", "2px")]
    [TestCase(false, "unset", "2px")]
    [TestCase(true, "unset", "8px")]
    [TestCase(false, "red", "2px")]
    [TestCase(true, "red", "8px")]
    [TestCase(false, "var(--missing)", "2px")]
    [TestCase(true, "var(--missing)", "8px")]
    public void InheritanceAndInvalidComputedValuesUseTheRegistration(bool inherits, string value, string expected)
    {
        var document = MarkupParser.ParseHtml("<section><div id=t></div></section>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse($$"""
            @property --x {syntax:"<length>";inherits:{{inherits.ToString().ToLowerInvariant()}};initial-value:2px}
            section {font-size:4px;--x:2em}
            div {font-size:10px;--x:{{value}};width:var(--x,99px)}
            """);
        var query = Query(document, sheet);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "width", ref matching).Text.Should().Be(expected);
        query.GetProperty(target, "--x", ref matching).Text.Should().Be(expected);
    }

    [TestCase("initial")]
    [TestCase("unset")]
    [TestCase("var(--missing)")]
    [TestCase("var(--x)")]
    public void UniversalSyntaxWithoutInitialValueIsGuaranteedInvalid(string value)
    {
        var document = MarkupParser.ParseHtml("<section><div id=t></div></section>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse($$"""
            @property --x {syntax:"*";inherits:false}
            section {--x:8px} div {--x:{{value}};width:var(--x,99px)}
            """);
        var query = Query(document, sheet);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "width", ref matching).Text.Should().Be("99px");
        query.GetProperty(target, "--x", ref matching).Text.Should().BeEmpty();
    }

    [Test]
    public void RegistrationOrderAndMutationsDoNotChangeSpecifiedDeclarations()
    {
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse("""
            @layer last, first;
            @layer first {@property --x {syntax:"<length>";inherits:false;initial-value:2px}}
            @layer last {@property --x {syntax:"<length>";inherits:false;initial-value:4px}}
            @media not all {@property --x {syntax:"<length>";inherits:false;initial-value:8px}}
            div {--x:2px;--x:red;width:var(--x)}
            """);
        var query = Query(document, sheet);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "width", ref matching).Text.Should().Be("4px");
        ((CssStyleRule) sheet.Rules[4]).Style.GetPropertyValue("--x", new(default)).Should().Be("red");
        ((CssLayerBlockRule) sheet.Rules[2]).DeleteRule(0);
        Action stale = () => query.GetProperty(target, "width", ref matching);
        stale.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
        Query(document, sheet).GetProperty(target, "width", ref matching).Text.Should().Be("2px");
    }

    [Test]
    public void RegisteredCyclesUseInitialValuesRatherThanVarFallback()
    {
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse("""
            @property --x {syntax:"<length>";inherits:false;initial-value:2px}
            @property --y {syntax:"<length>";inherits:false;initial-value:4px}
            div {--x:var(--y);--y:var(--x);width:var(--x,99px)}
            """);
        var matching = new SelectorMatchWork(document, default);
        var query = Query(document, sheet);
        query.GetProperty(target, "width", ref matching).Text.Should().Be("2px");
        query.GetProperty(target, "--y", ref matching).Text.Should().Be("4px");
    }

    [TestCase("<length>", "0px", "1em", "font-size")]
    [TestCase("<color>", "black", "currentColor", "color")]
    public void RelativeOrdinaryPropertyCyclesRetainANamedCompletionBoundary(
        string syntax, string initial, string value, string dependency)
    {
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse($$"""
            @property --x {syntax:"{{syntax}}";inherits:false;initial-value:{{initial}}}
            div {--x:{{value}};{{dependency}}:var(--x)}
            """);
        var matching = new SelectorMatchWork(document, default);
        var query = Query(document, sheet);
        Action read = () => query.GetProperty(target, "--x", ref matching);
        read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:registered-property-cycle");
    }

    [Test]
    public void InheritedColorsAreComputedInTheirDefiningScopeAndNamesAreCaseSensitive()
    {
        var document = MarkupParser.ParseHtml("<section><div id=t></div></section>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse("""
            @property --C {syntax:"<color>";inherits:true;initial-value:black}
            section {color:green;--C:currentColor}
            div {color:red;--copy:var(--C);--lower:var(--c,blue)}
            """);
        var matching = new SelectorMatchWork(document, default);
        var query = Query(document, sheet);
        query.GetProperty(target, "--copy", ref matching).Text.Should().Be("rgb(0, 128, 0)");
        query.GetProperty(target, "--lower", ref matching).Text.Should().Be("blue");
    }

    [Test]
    public void ImportedRegistrationsAppearAndDisappearWithTheirSheet()
    {
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse("@import 'child'; div{width:var(--x,99px)}");
        var child = CssStyleSheet.Parse("@property --x {syntax:'<length>';inherits:false;initial-value:5px}");
        var matching = new SelectorMatchWork(document, default);
        Query(document, sheet).GetProperty(target, "width", ref matching).Text.Should().Be("99px");
        ((CssImportRule) sheet.Rules[0]).SetStyleSheet(child, null, null, new(default));
        Query(document, sheet).GetProperty(target, "width", ref matching).Text.Should().Be("5px");
        child.Disabled = true;
        Query(document, sheet).GetProperty(target, "width", ref matching).Text.Should().Be("99px");
        child.Disabled = false;
        child.DeleteRule(0);
        Query(document, sheet).GetProperty(target, "width", ref matching).Text.Should().Be("99px");
    }

    private static NativeCssQuery Query(Document document, CssStyleSheet sheet)
    {
        var work = new CssValueWork(default);
        return new(document, [new(sheet, NativeCssOrigin.Author)], [], new(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
    }

    [Test]
    public void EnumerationIncludesRegisteredDefaultsButNotGuaranteedInvalidValues()
    {
        var document = MarkupParser.ParseHtml("<div id=t></div>");
        var target = SelectorMatcher.QuerySelector(SelectorCompiler.Compile("#t", null, default), document)!;
        var sheet = CssStyleSheet.Parse("""
            @property --default {syntax:"<length>";inherits:false;initial-value:5px}
            @property --invalid {syntax:"*";inherits:false}
            @property --empty {syntax:"*";inherits:false}
            div {color:black;--empty:;--unregistered:var(--missing)}
            """);
        var matching = new SelectorMatchWork(document, default);
        var properties = Query(document, sheet).Enumerate(target, ref matching);
        properties.Where(property => property.Name.StartsWith("--", StringComparison.Ordinal))
            .Select(property => property.Name).Should().Equal("--default", "--empty");
    }
}
