using Jint.HtmlParser;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

[TestFixture]
public sealed class PropertyGrammarTests
{
    [TestCase("display", "INLINE flow-root", "inline-block")]
    [TestCase("display", "flow inline", "inline")]
    [TestCase("display", "list-item flow-root inline", "inline flow-root list-item")]
    [TestCase("display", "table-cell", "table-cell")]
    [TestCase("display", "block ruby", "block ruby")]
    [TestCase("display", "ruby", "ruby")]
    [TestCase("visibility", "c\\6f llapse", "collapse")]
    [TestCase("opacity", "+1.250", "1.25")]
    [TestCase("opacity", "-20%", "-0.2")]
    [TestCase("opacity", "50%", "0.5")]
    [TestCase("opacity", "120%", "1.2")]
    [TestCase("opacity", "-0%", "0")]
    [TestCase("opacity", "calc(1 + 2)", "calc(3)")]
    [TestCase("z-index", "-10", "-10")]
    [TestCase("z-index", "1000000000000001", "1000000000000001")]
    [TestCase("z-index", "1000000000000011", "1000000000000011")]
    [TestCase("z-index", "-1000000000000001", "-1000000000000001")]
    [TestCase("z-index", "+0001000000000000011", "1000000000000011")]
    [TestCase("z-index", "-0000", "0")]
    [TestCase("z-index", "9999999999999999999999999999999999999999", "9999999999999999999999999999999999999999")]
    [TestCase("z-index", "calc(1.5)", "calc(1.5)")]
    [TestCase("position", "STICKY", "sticky")]
    [TestCase("pointer-events", "visiblePainted", "visiblepainted")]
    [TestCase("box-sizing", "BORDER-box", "border-box")]
    [TestCase("overflow", "clip auto", "clip auto")]
    [TestCase("overflow", "scroll scroll", "scroll")]
    [TestCase("overflow-x", "hidden", "hidden")]
    [TestCase("overflow-y", "overlay", "auto")]
    [TestCase("overflow-y", "Revert-Layer", "revert-layer")]
    public void AcceptsCompleteGrammar(string name, string source, string expected)
    {
        var result = CssPropertyParser.Parse(name, source);
        result.Status.Should().Be(CssPropertyStatus.Valid);
        result.Value.Serialize().Should().Be(expected);
        var reparsed = CssPropertyParser.Parse(name, expected);
        reparsed.Status.Should().Be(CssPropertyStatus.Valid);
        reparsed.Value.Serialize().Should().Be(expected);
    }

    [TestCase("display", "inline inline")]
    [TestCase("display", "flex list-item")]
    [TestCase("display", "none inline")]
    [TestCase("display", "inline-block flow")]
    [TestCase("display", "block flow flow-root")]
    [TestCase("visibility", "none")]
    [TestCase("opacity", "1px")]
    [TestCase("opacity", "calc(1px)")]
    [TestCase("opacity", "1 !important")]
    [TestCase("opacity", "calc(1+2)")]
    [TestCase("z-index", "1.0")]
    [TestCase("z-index", "1e0")]
    [TestCase("z-index", "2%")]
    [TestCase("position", "center")]
    [TestCase("pointer-events", "inherit auto")]
    [TestCase("box-sizing", "padding-box")]
    [TestCase("overflow", "auto auto auto")]
    [TestCase("overflow-x", "hidden scroll")]
    
    public void RejectsCompleteInvalidGrammar(string name, string source) =>
        CssPropertyParser.Parse(name, source).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void PendingAndUnknownRemainDistinct()
    {
        CssPropertyCatalog.Obligations.Count.Should().Be(433);
        CssPropertyParser.Parse("box-shadow", "none").Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        CssPropertyParser.Parse("made-up", "red").Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        CssPropertyParser.Parse("display", "inline", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        Action read = () => _ = default(CssPropertyResult).Value;
        read.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void MetadataHasRealDefaultsAndShorthandMembership()
    {
        CssPropertyRegistry.Completed["display"].InitialValue.Should().Be("inline");
        CssPropertyRegistry.Completed["visibility"].Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["opacity"].Inherited.Should().BeFalse();
        CssPropertyRegistry.Completed["overflow"].Longhands.Should().Equal("overflow-x", "overflow-y");
        CssPropertyRegistry.Completed["overflow"].ResetOnlyLonghands.Should().BeEmpty();
    }

    [TestCase("var(--x)")]
    [TestCase("calc(1 + env(foo, var(--x)))")]
    public void DeferredValuesCarryReferenceProgram(string source)
    {
        var result = CssPropertyParser.Parse("opacity", source);
        result.Status.Should().Be(CssPropertyStatus.Deferred);
        result.Value.References.Count.Should().BeGreaterThan(0);
        result.Value.Serialize().Should().Be(source);
    }

    [TestCase("\u00a0", "\u00a0")]
    [TestCase(" \t\u00a0\r\n", "\u00a0")]
    [TestCase("a\\ ", "a\\ ")]
    [TestCase(" \ta\\  \r\n", "a\\ ")]
    [TestCase("a\\20 ", "a\\20 ")]
    [TestCase("\u2003a\u2003", "\u2003a\u2003")]
    [TestCase(" \tvar(--x, a\\ ) \n", "var(--x, a\\ )")]
    [TestCase(" \t\r\n", "")]
    public void CustomValueSerializationTrimsOnlyBoundaryWhitespaceTokens(string source, string expected)
    {
        var result = CssPropertyParser.Parse("--x", source);
        result.Status.Should().Be(CssPropertyStatus.Valid);
        result.Value.Serialize().Should().Be(expected);
        var reparsed = CssPropertyParser.Parse("--x", expected);
        reparsed.Status.Should().Be(CssPropertyStatus.Valid);
        reparsed.Value.Serialize().Should().Be(expected);
    }

    [Test]
    public void DirectComponentsKeepOriginalOffsetsAndNestedReferences()
    {
        const string source = "opacity: 1; overflow: var(--X, env(foo, var(--Y)));";
        var declarations = new CssSyntaxParser(source, null, default).ParseDeclarationList();
        var input = CssReferenceInput.FromComponents(source, declarations[1].Value, 0);
        var result = CssPropertyParser.Parse("overflow", input, CssDeclarationContext.Style, new CssValueWork(default));
        result.Status.Should().Be(CssPropertyStatus.Deferred);
        result.Value.References.Count.Should().Be(3);
        result.Value.References[0].Span.Start.Should().Be(source.IndexOf("var", StringComparison.Ordinal));
        result.Value.Serialize().Should().Be("var(--X, env(foo, var(--Y)))");
        Action negative = () => CssReferenceInput.FromComponents(source, declarations[1].Value, -1);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void CancellationIsNotInvalidCss()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action parse = () => CssPropertyParser.Parse("display", "inline", cancellationToken: cancellation.Token);
        parse.Should().Throw<OperationCanceledException>();
    }
}
