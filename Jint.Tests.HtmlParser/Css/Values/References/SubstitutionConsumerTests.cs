#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionConsumerTests
{
    [Test]
    public void LengthPrimitiveConsumesProjectedComponentsDirectly()
    {
        var result = SubstitutionFixture.Resolve("var(--length)", SubstitutionFixture.Specified("--length", "12px"));
        var atom = CssPrimitiveParser.ParseNumericAtom(result.Value.Components, new CssValueWork(default));
        atom.IsMatch.Should().BeTrue();
        atom.Value.Kind.Should().Be(CssNumericKind.Dimension);
        atom.Value.Unit.Should().Be(CssUnit.Px);
        atom.Value.Span.Start.Should().Be(0);
        result.Value.OriginsFor(atom.Span)[0].Source.Source.Should().Be("12px");
    }

    [Test]
    public void CalcConsumesSubstitutedLengthsAndPreservesPercentageBasisAndRange()
    {
        var result = SubstitutionFixture.Resolve("calc(var(--length) + var(--percentage))",
            SubstitutionFixture.Specified("--length", "12px"), SubstitutionFixture.Specified("--percentage", "25%"));
        var context = new CssMathContext(CssMathProduction.LengthPercentage, CssMathPercentageMode.Length,
            new CssMathRange(0, 100));
        var parsed = CssMathParser.ParseMath(result.Value.Components[0], context, new CssValueWork(default));
        parsed.Status.Should().Be(CssMathParseStatus.Match);
        parsed.Value.Context.Percentages.Should().Be(CssMathPercentageMode.Length);
        parsed.Value.Context.Range.Lower.Should().Be(0);
        parsed.Value.Context.Range.Upper.Should().Be(100);
        parsed.Value.NodeCount.Should().BeGreaterThan(1);
        CssMathParser.ParseMath(result.Value.Components[0],
            new CssMathContext(CssMathProduction.Length, CssMathPercentageMode.Forbidden), new CssValueWork(default))
            .Status.Should().Be(CssMathParseStatus.NoMatch);
    }

    [Test]
    public void IntroducedTokenErrorMapsToItsOwnerAndWholeFunctionErrorSpansMixedSources()
    {
        var result = SubstitutionFixture.Resolve("calc(var(--length) + var(--time))",
            SubstitutionFixture.Specified("--length", "12px"), SubstitutionFixture.Specified("--time", "3s"));
        var parsed = CssMathParser.ParseMath(result.Value.Components[0],
            new CssMathContext(CssMathProduction.Length, CssMathPercentageMode.Forbidden), new CssValueWork(default));
        parsed.Status.Should().Be(CssMathParseStatus.NoMatch);
        var range = result.Value.OriginsFor(result.Value.Components[0].Span);
        range.Count.Should().Be(7);
        range[0].Source.Source.Should().Be("calc(var(--length) + var(--time))");
        range[1].Source.Source.Should().Be("12px");
        range[5].Source.Source.Should().Be("3s");
        var introduced = SubstitutionFixture.Resolve("var(--bad)", SubstitutionFixture.Specified("--bad", "foo(1)"));
        var atom = CssPrimitiveParser.ParseNumericAtom(introduced.Value.Components, new CssValueWork(default));
        atom.IsMatch.Should().BeFalse();
        var origin = introduced.Value.OriginsFor(atom.Span);
        origin.Count.Should().Be(3);
        origin[0].Source.Source.Should().Be("foo(1)");
    }
}
